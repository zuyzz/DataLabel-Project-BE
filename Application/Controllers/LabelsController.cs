using DataLabelProject.Business.Services.Labels;
using DataLabelProject.Application.DTOs.Labels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DataLabelProject.Application.Controllers;

[ApiController]
[Route("api/labels")]
public class LabelsController : ControllerBase
{
    private readonly ILabelService _labelService;

    public LabelsController(ILabelService labelService)
    {
        _labelService = labelService;
    }

    [HttpGet]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetAll([FromQuery] LabelQueryParameters @params)
    {
        var result = await _labelService.GetLabels(@params);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> CreateLabel(
        [FromBody] CreateLabelRequest request)
    {
        var result = await _labelService.CreateLabel(request);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> UpdateLabel(
        Guid id,
        [FromBody] UpdateLabelRequest request)
    {
        var result = await _labelService.UpdateLabel(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> DeleteLabel(
        Guid id)
    {
        await _labelService.DeactivateLabel(id);
        return NoContent();
    }

    [HttpPost("add/{projectId}")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> AddLabel(
        [FromRoute] Guid projectId,
        [FromBody] AddLabelRequest request)
    {
        await _labelService.AddLabelToProject(request.LabelId, projectId);
        return NoContent();
    }

    [HttpPost("remove/{projectId}")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> RemoveLabel(
        [FromRoute] Guid projectId,
        [FromBody] RemoveLabelRequest request)
    {
        await _labelService.RemoveLabelFromProject(request.LabelId, projectId);
        return NoContent();
    }
}

