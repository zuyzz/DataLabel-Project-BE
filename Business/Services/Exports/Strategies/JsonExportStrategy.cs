using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DataLabelProject.Application.DTOs.Exports;

public class JsonExportStrategy : IExportStrategy
{
    public string Format => "json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task<(Stream Stream, string ContentType, string FileName)> GenerateAsync(ExportDataset dataset, CreateExportRequest request)
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
                labels = new System.Collections.Generic.List<object>();
            }

            return new
            {
                image = img.FileName,
                labels
            };
        }).ToList();

        var json = JsonSerializer.Serialize(output, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var stream = new MemoryStream(bytes);
        stream.Position = 0;
        return Task.FromResult<(Stream, string, string)>((stream, "application/json", "dataset.json"));
    }
}