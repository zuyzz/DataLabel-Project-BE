using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataLabelProject.Application.DTOs.Exports;
using DataLabelProject.Business.Services.Storage;
using DataLabelProject.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataLabelProject.Business.Services.Exports;

public class ExportService : IExportService
{
    private readonly AppDbContext _context;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<ExportService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExportService(
        AppDbContext context,
        IFileStorage fileStorage,
        ILogger<ExportService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> CreateExport(Guid projectId, CreateExportRequest request)
    {
        var format = request.Format.ToLowerInvariant();
        if (format != "json" && format != "coco" && format != "yolo")
            throw new ArgumentException("Format must be one of: json, coco, yolo");

        var project = await _context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == projectId);
        if (project == null)
            throw new InvalidOperationException("Project not found");

        // Build the unified dataset from consensus annotations
        var dataset = await BuildDatasetFromConsensus(projectId);

        // Generate file based on format and return directly
        return format switch
        {
            "coco" => GenerateCoco(dataset),
            "yolo" => await GenerateYolo(dataset, request),
            _ => GenerateJson(dataset)
        };
    }

    // ─── Dataset Building ─────────────────────────────────────────────

    private async Task<ExportDataset> BuildDatasetFromConsensus(Guid projectId)
    {
        // Get all dataset items belonging to the project
        var datasetItemIds = await _context.DatasetItems
            .AsNoTracking()
            .Where(di => di.ItemDataset != null && di.ItemDataset.ProjectId == projectId)
            .Select(di => di.DatasetItemId)
            .ToListAsync();

        // Get the latest consensus per dataset item
        var consensuses = await _context.Consensuses
            .AsNoTracking()
            .Where(c => datasetItemIds.Contains(c.DatasetItemId))
            .ToListAsync();

        var consensusByDatasetItemId = consensuses
            .GroupBy(c => c.DatasetItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAt).First());

        // Load dataset items for the consensus entries
        var datasetItems = await _context.DatasetItems
            .AsNoTracking()
            .Where(di => consensusByDatasetItemId.Keys.Contains(di.DatasetItemId))
            .ToDictionaryAsync(di => di.DatasetItemId);

        var dataset = new ExportDataset();
        var categoriesMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int imageIdCounter = 1;
        int annotationIdCounter = 1;

        foreach (var kvp in consensusByDatasetItemId)
        {
            var datasetItemId = kvp.Key;
            var consensus = kvp.Value;

            if (!datasetItems.TryGetValue(datasetItemId, out var datasetItem))
                continue;

            // Parse consensus payload
            var objects = ParseConsensusPayload(consensus.Payload);
            if (objects == null || objects.Count == 0)
                continue;

            var imageId = imageIdCounter++;

            // Parse metadata for image dimensions
            var (width, height) = ParseImageDimensions(datasetItem.Metadata);

            var fileName = ExtractFileName(datasetItem.StorageUri);

            dataset.Images.Add(new ExportImage
            {
                Id = imageId,
                FileName = fileName,
                Width = width,
                Height = height,
                StorageUri = datasetItem.StorageUri
            });

            foreach (var obj in objects)
            {
                // Get or create category
                if (!categoriesMap.TryGetValue(obj.Label, out var categoryId))
                {
                    categoryId = categoriesMap.Count + 1;
                    categoriesMap[obj.Label] = categoryId;
                    dataset.Categories.Add(new ExportCategory
                    {
                        Id = categoryId,
                        Name = obj.Label
                    });
                }

                dataset.Annotations.Add(new ExportAnnotation
                {
                    Id = annotationIdCounter++,
                    ImageId = imageId,
                    CategoryId = categoryId,
                    Bbox = new double[] { obj.X, obj.Y, obj.W, obj.H }
                });
            }
        }

        return dataset;
    }

    // ─── Format Generators ────────────────────────────────────────────

    private (Stream Stream, string ContentType, string FileName) GenerateJson(ExportDataset dataset)
    {
        // Group annotations by image
        var annotationsByImage = dataset.Annotations
            .GroupBy(a => a.ImageId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Create category lookup
        var categoryLookup = dataset.Categories.ToDictionary(c => c.Id, c => c.Name);

        // Build simplified format
        var output = dataset.Images.Select(img =>
        {
            object labels;
            if (annotationsByImage.TryGetValue(img.Id, out var annotations))
            {
                labels = annotations.Select(a => new
                {
                    @class = categoryLookup[a.CategoryId],
                    bbox = new[] { (int)a.Bbox[0], (int)a.Bbox[1], (int)a.Bbox[2], (int)a.Bbox[3] }
                }).ToList();
            }
            else
            {
                labels = new List<object>();
            }

            return new
            {
                image = img.FileName,
                labels
            };
        }).ToList();

        var json = JsonSerializer.Serialize(output, JsonOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var stream = new MemoryStream(bytes);
        stream.Position = 0;

        return (stream, "application/json", "dataset.json");
    }

    private (Stream Stream, string ContentType, string FileName) GenerateCoco(ExportDataset dataset)
    {
        var coco = new
        {
            images = dataset.Images.Select(i => new
            {
                id = i.Id,
                file_name = i.FileName,
                width = i.Width,
                height = i.Height
            }),
            annotations = dataset.Annotations.Select(a => new
            {
                id = a.Id,
                image_id = a.ImageId,
                category_id = a.CategoryId,
                bbox = a.Bbox,
                area = a.Bbox[2] * a.Bbox[3], // width * height
                iscrowd = 0
            }),
            categories = dataset.Categories.Select(c => new
            {
                id = c.Id,
                name = c.Name
            })
        };

        var json = JsonSerializer.Serialize(coco, JsonOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var stream = new MemoryStream(bytes);
        stream.Position = 0;

        return (stream, "application/json", "dataset_coco.json");
    }

    private async Task<(Stream Stream, string ContentType, string FileName)> GenerateYolo(
        ExportDataset dataset,
        CreateExportRequest request)
    {
        // Split images into train/val
        var trainSplitRatio = request.TrainSplitRatio ?? 0.8;
        var (trainImages, valImages) = SplitTrainVal(dataset.Images, trainSplitRatio, request.RandomSeed);

        _logger.LogInformation(
            "YOLO Export: {Total} images split into {Train} train / {Val} val",
            dataset.Images.Count, trainImages.Count, valImages.Count);

        // Validate split results
        if (trainImages.Count == 0)
        {
            throw new InvalidOperationException(
                "Train set is empty after split. Ensure the project has annotated images.");
        }

        if (valImages.Count == 0)
        {
            _logger.LogWarning("Validation set is empty. Consider using a larger dataset.");
        }

        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Group annotations by image
            var annotationsByImage = dataset.Annotations
                .GroupBy(a => a.ImageId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Process train set
            await ProcessImageSet(archive, trainImages, annotationsByImage, dataset.Categories, "train");

            // Process val set
            await ProcessImageSet(archive, valImages, annotationsByImage, dataset.Categories, "val");

            // Generate data.yaml
            await GenerateDataYaml(archive, dataset.Categories, trainImages.Count, valImages.Count);
        }

        zipStream.Position = 0;
        return (zipStream, "application/zip", "dataset_yolo.zip");
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private async Task ProcessImageSet(
        ZipArchive archive,
        List<ExportImage> images,
        Dictionary<int, List<ExportAnnotation>> annotationsByImage,
        List<ExportCategory> categories,
        string setName)
    {
        foreach (var image in images)
        {
            // Download image from Supabase Storage
            try
            {
                var (imageStream, contentType, _) = await _fileStorage.GetFileStreamAsync(image.StorageUri);

                // Add image to ZIP: images/train/filename.jpg or images/val/filename.jpg
                var imageEntry = archive.CreateEntry($"images/{setName}/{image.FileName}");
                using (var imageEntryStream = imageEntry.Open())
                {
                    await imageStream.CopyToAsync(imageEntryStream);
                }

                // Dispose the stream from storage
                await imageStream.DisposeAsync();

                _logger.LogDebug("Added image to {Set}: {FileName}", setName, image.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download image {FileName} from {Uri}. Skipping.", image.FileName, image.StorageUri);
                // Continue processing other images
                continue;
            }

            // Generate label file: labels/train/filename.txt or labels/val/filename.txt
            var lines = new List<string>();

            if (annotationsByImage.TryGetValue(image.Id, out var annotations))
            {
                foreach (var ann in annotations)
                {
                    // Convert to YOLO normalized format
                    var classId = ann.CategoryId - 1; // YOLO uses 0-indexed classes

                    double xCenter, yCenter, wNorm, hNorm;

                    if (image.Width > 0 && image.Height > 0)
                    {
                        xCenter = (ann.Bbox[0] + ann.Bbox[2] / 2.0) / image.Width;
                        yCenter = (ann.Bbox[1] + ann.Bbox[3] / 2.0) / image.Height;
                        wNorm = ann.Bbox[2] / image.Width;
                        hNorm = ann.Bbox[3] / image.Height;
                    }
                    else
                    {
                        // Skip annotation if image dimensions are invalid
                        _logger.LogWarning("Invalid image dimensions for {FileName}, skipping annotation", image.FileName);
                        continue;
                    }

                    lines.Add($"{classId} {xCenter:F6} {yCenter:F6} {wNorm:F6} {hNorm:F6}");
                }
            }

            // Create label file (even if empty - YOLO expects one .txt per image)
            var txtFileName = Path.GetFileNameWithoutExtension(image.FileName) + ".txt";
            var labelEntry = archive.CreateEntry($"labels/{setName}/{txtFileName}");

            using var writer = new StreamWriter(labelEntry.Open());
            foreach (var line in lines)
            {
                await writer.WriteLineAsync(line);
            }
        }
    }

    private async Task GenerateDataYaml(
        ZipArchive archive,
        List<ExportCategory> categories,
        int trainCount,
        int valCount)
    {
        var yamlContent = new StringBuilder();

        // YOLO expects relative paths from data.yaml location
        yamlContent.AppendLine("train: images/train");
        yamlContent.AppendLine("val: images/val");
        yamlContent.AppendLine();
        yamlContent.AppendLine($"nc: {categories.Count}");
        yamlContent.AppendLine();

        // Build names list
        var orderedCategories = categories.OrderBy(c => c.Id).Select(c => c.Name).ToList();
        yamlContent.AppendLine("names:");
        for (int i = 0; i < orderedCategories.Count; i++)
        {
            yamlContent.AppendLine($"  {i}: {orderedCategories[i]}");
        }

        yamlContent.AppendLine();
        yamlContent.AppendLine("# Dataset Statistics");
        yamlContent.AppendLine($"# Train images: {trainCount}");
        yamlContent.AppendLine($"# Val images: {valCount}");
        yamlContent.AppendLine($"# Total: {trainCount + valCount}");

        // Create data.yaml at root of ZIP
        var yamlEntry = archive.CreateEntry("data.yaml");
        using var writer = new StreamWriter(yamlEntry.Open());
        await writer.WriteAsync(yamlContent.ToString());

        _logger.LogInformation("Generated data.yaml with {Count} classes", categories.Count);
    }

    private (List<ExportImage> TrainImages, List<ExportImage> ValImages) SplitTrainVal(
        List<ExportImage> images,
        double trainRatio,
        int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var shuffled = images.OrderBy(x => random.Next()).ToList();

        var trainCount = (int)(shuffled.Count * trainRatio);
        var trainImages = shuffled.Take(trainCount).ToList();
        var valImages = shuffled.Skip(trainCount).ToList();

        return (trainImages, valImages);
    }

    private static List<ConsensusObject>? ParseConsensusPayload(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("bboxes", out var bboxesElement) ||
                bboxesElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var objects = new List<ConsensusObject>();

            foreach (var obj in bboxesElement.EnumerateArray())
            {
                objects.Add(new ConsensusObject
                {
                    Label = obj.GetProperty("Label").GetString() ?? "",
                    X = obj.GetProperty("X").GetDouble(),
                    Y = obj.GetProperty("Y").GetDouble(),
                    W = obj.GetProperty("Width").GetDouble(),
                    H = obj.GetProperty("Height").GetDouble()
                });
            }

            return objects;
        }
        catch (JsonException)
        {
            // Invalid JSON
            return null;
        }
        catch (Exception)
        {
            // Missing fields or wrong format
            return null;
        }
    }

    private static (int Width, int Height) ParseImageDimensions(string metadata)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadata);
            var width = 0;
            var height = 0;

            if (doc.RootElement.TryGetProperty("width", out var w))
                width = w.GetInt32();
            if (doc.RootElement.TryGetProperty("height", out var h))
                height = h.GetInt32();

            return (width, height);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static string ExtractFileName(string storageUri)
    {
        try
        {
            var uri = new Uri(storageUri);
            return Path.GetFileName(uri.LocalPath);
        }
        catch
        {
            return storageUri;
        }
    }

    // ─── Internal Models ──────────────────────────────────────────────

    private class ExportDataset
    {
        public List<ExportImage> Images { get; set; } = new();
        public List<ExportAnnotation> Annotations { get; set; } = new();
        public List<ExportCategory> Categories { get; set; } = new();
    }

    private class ExportImage
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public int Width { get; set; }
        public int Height { get; set; }
        public string StorageUri { get; set; } = null!;
    }

    private class ExportAnnotation
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public int CategoryId { get; set; }
        public double[] Bbox { get; set; } = null!;
    }

    private class ExportCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class ConsensusObject
    {
        public string Label { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
    }
}
