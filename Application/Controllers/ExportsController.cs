using DataLabelProject.Application.DTOs.Exports;
using DataLabelProject.Business.Services.Exports;
using DataLabelProject.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Application.Controllers;

[ApiController]
[Route("api/exports")]
public class ExportsController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly AppDbContext _context;

    public ExportsController(IExportService exportService, AppDbContext context)
    {
        _exportService = exportService;
        _context = context;
    }

    [HttpGet("debug/projects-with-data")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> GetProjectsWithConsensusData()
    {
        var projectsWithData = await (
            from consensus in _context.Consensuses
            join datasetItem in _context.DatasetItems on consensus.DatasetItemId equals datasetItem.DatasetItemId
            join dataset in _context.Datasets on datasetItem.DatasetId equals dataset.DatasetId
            join project in _context.Projects on dataset.ProjectId equals project.ProjectId
            group new { consensus, datasetItem } by new { project.ProjectId, project.Name } into g
            select new
            {
                ProjectId = g.Key.ProjectId,
                ProjectName = g.Key.Name,
                ConsensusCount = g.Select(x => x.consensus.ConsensusId).Distinct().Count(),
                ItemCount = g.Select(x => x.datasetItem.DatasetItemId).Distinct().Count()
            })
            .OrderByDescending(p => p.ConsensusCount)
            .ToListAsync();

        return Ok(projectsWithData);
    }

    [HttpPost("{projectId}")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> CreateExport(Guid projectId, [FromBody] CreateExportRequest request)
    {
        try
        {
            var (stream, contentType, fileName) = await _exportService.CreateExport(projectId, request);

            // Set Content-Disposition header to force download
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");

            return File(stream, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
