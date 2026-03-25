using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DataLabelProject.Application.DTOs.Exports;

public class CocoExportStrategy : IExportStrategy
{
    public string Format => "coco";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task<(Stream Stream, string ContentType, string FileName)> GenerateAsync(ExportDataset dataset, CreateExportRequest request)
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
                area = a.Bbox[2] * a.Bbox[3],
                iscrowd = 0
            }),
            categories = dataset.Categories.Select(c => new
            {
                id = c.Id,
                name = c.Name
            })
        };

        var json = JsonSerializer.Serialize(coco, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var stream = new MemoryStream(bytes);
        stream.Position = 0;
        return Task.FromResult<(Stream, string, string)>((stream, "application/json", "dataset_coco.json"));
    }
}