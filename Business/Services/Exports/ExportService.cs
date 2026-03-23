using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataLabelProject.Application.DTOs.Exports;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Models.Enums;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Data;
using DataLabelProject.Data.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Exports;

public class ExportService : IExportService
{
    private readonly IExportJobRepository _exportJobRepository;
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExportService(
        IExportJobRepository exportJobRepository,
        AppDbContext context,
        ICurrentUserService currentUserService)
    {
        _exportJobRepository = exportJobRepository;
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<ExportJobResponse>> GetExports()
    {
        var exports = await _exportJobRepository.GetAllAsync();
        return exports.Select(MapToResponse);
    }

    public async Task<ExportJobResponse?> GetExportById(Guid exportId)
    {
        var export = await _exportJobRepository.GetByIdAsync(exportId);
        return export == null ? null : MapToResponse(export);
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

        var userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        // Generate a new export ID for tracking
        var exportId = Guid.NewGuid();

        try
        {
            // Build the unified dataset from consensus annotations
            var dataset = await BuildDatasetFromConsensus(projectId);

            // Generate file based on format and return directly
            (Stream stream, string contentType, string fileName) result;
            switch (format)
            {
                case "coco":
                    result = GenerateCoco(dataset);
                    break;
                case "yolo":
                    result = await GenerateYolo(dataset);
                    break;
                default: // json
                    result = GenerateJson(dataset);
                    break;
            }

            // Create export job record for tracking (without FileUri)
            var exportJob = new ExportJob
            {
                ExportId = exportId,
                InitiatorId = userId,
                ProjectId = projectId,
                Format = format,
                Status = ExportJobStatus.Completed,
                FileUri = null // No longer storing in cloud storage
            };

            await _exportJobRepository.CreateAsync(exportJob);
            await _exportJobRepository.SaveChangesAsync();

            return result;
        }
        catch (Exception)
        {
            // Create export job record with failed status
            var exportJob = new ExportJob
            {
                ExportId = exportId,
                InitiatorId = userId,
                ProjectId = projectId,
                Format = format,
                Status = ExportJobStatus.Failed
            };

            await _exportJobRepository.CreateAsync(exportJob);
            await _exportJobRepository.SaveChangesAsync();

            throw;
        }
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadExport(Guid exportId)
    {
        var export = await _exportJobRepository.GetByIdAsync(exportId);
        if (export == null)
            throw new InvalidOperationException("Export not found");

        if (export.Status != ExportJobStatus.Completed)
            throw new InvalidOperationException($"Export is not completed. Current status: {export.Status}");

        // Verify user has access to this export
        var userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        // Check if user is the initiator or a manager of the project
        var project = await _context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == export.ProjectId);

        if (project == null)
            throw new InvalidOperationException("Project not found");

        // Check if user is initiator or manager of the project
        if (export.InitiatorId != userId)
        {
            var isMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == export.ProjectId && pm.MemberId == userId);

            if (!isMember)
                throw new UnauthorizedAccessException("You do not have permission to download this export");
        }

        // Regenerate file on-demand since we no longer store in cloud storage
        var dataset = await BuildDatasetFromConsensus(export.ProjectId);

        return export.Format.ToLowerInvariant() switch
        {
            "coco" => GenerateCoco(dataset),
            "yolo" => await GenerateYolo(dataset),
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
                Height = height
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
        var json = JsonSerializer.Serialize(dataset, JsonOptions);
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

    private async Task<(Stream Stream, string ContentType, string FileName)> GenerateYolo(ExportDataset dataset)
    {
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Group annotations by image
            var annotationsByImage = dataset.Annotations
                .GroupBy(a => a.ImageId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var image in dataset.Images)
            {
                var lines = new List<string>();

                if (annotationsByImage.TryGetValue(image.Id, out var annotations))
                {
                    foreach (var ann in annotations)
                    {
                        // Convert to YOLO normalized format
                        // YOLO: category_id is 0-indexed
                        var classId = ann.CategoryId - 1;

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
                            // Fallback: use absolute values (not normalized)
                            xCenter = ann.Bbox[0] + ann.Bbox[2] / 2.0;
                            yCenter = ann.Bbox[1] + ann.Bbox[3] / 2.0;
                            wNorm = ann.Bbox[2];
                            hNorm = ann.Bbox[3];
                        }

                        lines.Add($"{classId} {xCenter:F6} {yCenter:F6} {wNorm:F6} {hNorm:F6}");
                    }
                }

                // Create txt file with same name as image but .txt extension
                var txtFileName = Path.GetFileNameWithoutExtension(image.FileName) + ".txt";
                var entry = archive.CreateEntry(txtFileName);

                using var writer = new StreamWriter(entry.Open());
                foreach (var line in lines)
                {
                    await writer.WriteLineAsync(line);
                }
            }

            // Add classes.txt file
            var classesEntry = archive.CreateEntry("classes.txt");
            using var classesWriter = new StreamWriter(classesEntry.Open());
            foreach (var category in dataset.Categories.OrderBy(c => c.Id))
            {
                await classesWriter.WriteLineAsync(category.Name);
            }
        }

        zipStream.Position = 0;
        return (zipStream, "application/zip", "dataset_yolo.zip");
    }

    // ─── Helpers ──────────────────────────────────────────────────────

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

    private static ExportJobResponse MapToResponse(ExportJob export) => new()
    {
        ExportId = export.ExportId,
        ProjectId = export.ProjectId,
        InitiatorId = export.InitiatorId,
        Format = export.Format,
        Status = export.Status.ToString(),
        FileUri = export.FileUri,
        CreatedAt = export.CreatedAt
    };

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