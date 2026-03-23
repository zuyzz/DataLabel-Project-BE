using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataLabelProject.Application.DTOs.Guidelines;
using DataLabelProject.Business.Services.Guidelines;

namespace DataLabelProject.Application.Controllers;

[ApiController]
[Route("api/guidelines")]
public class GuidelinesController : ControllerBase
{
    private readonly IGuidelineService _guidelineService;

    public GuidelinesController(IGuidelineService guidelineService)
    {
        _guidelineService = guidelineService;
    }

    /// <summary>
    /// Get all guidelines
    /// </summary>
    /// <response code="200">List of guidelines</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet]
    [Authorize(Roles = "manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllGuidelines([FromQuery] GuidelineQueryParameters @params)
    {
        var result = await _guidelineService.GetGuidelines(@params);
        return Ok(result);
    }

    /// <summary>
    /// Get guideline by ID
    /// </summary>
    /// <param name="id">Guideline ID</param>
    /// <response code="200">Guideline details</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="404">Guideline not found</response>
    [HttpGet("{id}")]
    [Authorize(Roles = "manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuidelineResponse>> GetGuidelineById(Guid id)
    {
        var result = await _guidelineService.GetGuidelineById(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Create new guideline
    /// </summary>
    /// <param name="request">Guideline details</param>
    /// <response code="201">Guideline created successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden - Admin or Manager only</response>
    [HttpPost]
    [Authorize(Roles = "admin,manager")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GuidelineResponse>> CreateGuideline([FromBody] CreateGuidelineRequest request)
    {
        var result = await _guidelineService.CreateGuideline(request);
        return CreatedAtAction(nameof(GetGuidelineById), new { id = result.GuidelineId }, result);
    }

    /// <summary>
    /// Update guideline
    /// </summary>
    /// <param name="id">Guideline ID</param>
    /// <param name="request">Updated guideline details</param>
    /// <response code="200">Guideline updated successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden - Admin or Manager only</response>
    /// <response code="404">Guideline not found</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "admin,manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuidelineResponse>> UpdateGuideline(Guid id, [FromBody] UpdateGuidelineRequest request)
    {
        var result = await _guidelineService.UpdateGuideline(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Delete guideline
    /// </summary>
    /// <param name="id">Guideline ID</param>
    /// <response code="200">Guideline deleted successfully</response>
    /// <response code="400">Cannot delete guideline in use</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden - Admin or Manager only</response>
    /// <response code="404">Guideline not found</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin,manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteGuideline(Guid id)
    {
        var result = await _guidelineService.DeleteGuideline(id);
        if (!result) return NotFound();
        return NoContent();
    }
}
