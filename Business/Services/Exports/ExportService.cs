using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataLabelProject.Application.DTOs.Exports;
using DataLabelProject.Business.Services.Exports.DatasetBuilder;
using DataLabelProject.Business.Services.Storage;
using DataLabelProject.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataLabelProject.Business.Services.Exports;

public class ExportService : IExportService
{
    private readonly IDatasetBuilder _datasetBuilder;
    private readonly IEnumerable<IExportStrategy> _strategies;

    public ExportService(IDatasetBuilder datasetBuilder, IEnumerable<IExportStrategy> strategies)
    {
        _datasetBuilder = datasetBuilder;
        _strategies = strategies;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> CreateExport(Guid projectId, CreateExportRequest request)
    {
        var format = request.Format.ToLowerInvariant();
        var dataset = await _datasetBuilder.BuildFromConsensus(projectId);
        var strategy = _strategies.FirstOrDefault(s => s.Format == format);
        if (strategy == null)
            throw new ArgumentException($"Unsupported export format: {format}");
        return await strategy.GenerateAsync(dataset, request);
    }

    // ...existing code...

    // ...existing code...
}
