using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DataLabelProject.Business.Services.Statistics;

namespace DataLabelProject.Application.Controllers;

[ApiController]
[Route("api/statistics")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _statisticsService;

    public StatisticsController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    private async Task<IActionResult?> ValidateProjectAccessAsync(Guid projectId)
    {
        if (!await _statisticsService.ProjectExistsAsync(projectId))
            return NotFound(new { message = "Project not found." });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Invalid user identity." });

        var role = GetCurrentUserRole();
        if (role == "admin")
            return null;

        if (!await _statisticsService.IsProjectMemberAsync(projectId, userId.Value))
            return Forbid();

        return null;
    }

    // =============== Project Statistics ===============

    [HttpGet("projects/{projectId}/overview")]
    [Authorize(Roles = "admin,manager,annotator,reviewer")]
    public async Task<IActionResult> GetProjectOverview(Guid projectId)
    {
        var accessCheck = await ValidateProjectAccessAsync(projectId);
        if (accessCheck != null) return accessCheck;

        var result = await _statisticsService.GetProjectOverviewAsync(projectId);
        return Ok(result);
    }

    // =============== Role-based Statistics ===============

    [HttpGet("reviewer/{reviewerId:guid}")]
    [Authorize(Roles = "admin,manager,reviewer")]
    public async Task<IActionResult> GetReviewerStats(Guid reviewerId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Invalid user identity." });

        var role = GetCurrentUserRole();
        if (role == "reviewer" && userId.Value != reviewerId)
            return Forbid();

        var result = await _statisticsService.GetReviewerStatsAsync(reviewerId);
        return Ok(result);
    }

    [HttpGet("annotator/{annotatorId:guid}")]
    [Authorize(Roles = "admin,manager,annotator")]
    public async Task<IActionResult> GetAnnotatorStats(Guid annotatorId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Invalid user identity." });

        var role = GetCurrentUserRole();
        if (role == "annotator" && userId.Value != annotatorId)
            return Forbid();

        var result = await _statisticsService.GetAnnotatorStatsAsync(annotatorId);
        return Ok(result);
    }

    [HttpGet("manager")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> GetManagerStats()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Invalid user identity." });

        var result = await _statisticsService.GetManagerStatsAsync(userId.Value);
        return Ok(result);
    }

    // =============== System Statistics ===============

    [HttpGet("system/overview")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetSystemOverview()
    {
        var result = await _statisticsService.GetSystemOverviewAsync();
        return Ok(result);
    }

    [HttpGet("system/projects-active")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetActiveProjects()
    {
        var result = await _statisticsService.GetActiveProjectsAsync();
        return Ok(result);
    }

    [HttpGet("system/activity")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetActivityTimeline([FromQuery] int days = 30)
    {
        if (days < 1 || days > 365)
            return BadRequest(new { message = "Days must be between 1 and 365." });

        var result = await _statisticsService.GetActivityTimelineAsync(days);
        return Ok(result);
    }
}
