using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataLabelProject.Application.DTOs.Exports;
using DataLabelProject.Business.Services.Storage;

public class YoloExportStrategy : IExportStrategy
{
    private readonly IFileStorage _fileStorage;

    public YoloExportStrategy(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    public string Format => "yolo";

    public async Task<(Stream Stream, string ContentType, string FileName)> GenerateAsync(ExportDataset dataset, CreateExportRequest request)
    {
        // Split images into train/val
        var trainSplitRatio = request.TrainSplitRatio ?? 0.8;
        var (trainImages, valImages) = SplitTrainVal(dataset.Images, trainSplitRatio, request.RandomSeed);

        if (trainImages.Count == 0)
        {
            throw new InvalidOperationException(
                "Train set is empty after split. Ensure the project has annotated images.");
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

    private async Task ProcessImageSet(
        ZipArchive archive,
        System.Collections.Generic.List<ExportImage> images,
        System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<ExportAnnotation>> annotationsByImage,
        System.Collections.Generic.List<ExportCategory> categories,
        string setName)
    {
        foreach (var image in images)
        {
            // Download and add image to ZIP
            try
            {
                var (imageStream, contentType, fileName) = await _fileStorage.GetFileStreamAsync(image.StorageUri);
                var imageEntry = archive.CreateEntry($"images/{setName}/{image.FileName}");

                using var imageEntryStream = imageEntry.Open();
                await imageStream.CopyToAsync(imageEntryStream);
                await imageStream.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Log or skip if image cannot be downloaded
                // Continue processing other images
                System.Console.WriteLine($"Warning: Could not download image {image.FileName}: {ex.Message}");
            }

            // Generate label file: labels/train/filename.txt or labels/val/filename.txt
            var lines = new System.Collections.Generic.List<string>();

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
                        continue;
                    }

                    lines.Add($"{classId} {xCenter:F6} {yCenter:F6} {wNorm:F6} {hNorm:F6}");
                }
            }

            // Create label file (even if empty - YOLO expects one .txt per image)
            var txtFileName = System.IO.Path.GetFileNameWithoutExtension(image.FileName) + ".txt";
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
        System.Collections.Generic.List<ExportCategory> categories,
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
    }

    private (System.Collections.Generic.List<ExportImage> TrainImages, System.Collections.Generic.List<ExportImage> ValImages) SplitTrainVal(
        System.Collections.Generic.List<ExportImage> images,
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
}