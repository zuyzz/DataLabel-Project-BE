using System.IO;
using System.Threading.Tasks;
using DataLabelProject.Application.DTOs.Exports;

public interface IExportStrategy
{
    string Format { get; }

    Task<(Stream Stream, string ContentType, string FileName)> GenerateAsync(ExportDataset dataset, CreateExportRequest request);
}