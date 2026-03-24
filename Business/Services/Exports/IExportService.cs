using DataLabelProject.Application.DTOs.Exports;

namespace DataLabelProject.Business.Services.Exports;

public interface IExportService
{
    Task<(Stream Stream, string ContentType, string FileName)> CreateExport(Guid projectId, CreateExportRequest request);
}
