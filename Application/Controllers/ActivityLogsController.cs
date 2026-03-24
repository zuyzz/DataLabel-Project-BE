using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DataLabelProject.Business.Services.ActivityLogs;
using DataLabelProject.Application.DTOs.ActivityLogs;

namespace DataLabelProject.Application.Controllers;

[ApiController]
[Route("api/activity-logs")]
[Authorize(Roles = "admin")]
public class ActivityLogsController : ControllerBase
{
    private readonly IActivityLogService _activityLogService;

    public ActivityLogsController(IActivityLogService activityLogService)
    {
        _activityLogService = activityLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActivityLogs(
        [FromQuery] ActivityLogQueryParameters @params)
    {
        var result = await _activityLogService.GetActivityLogs(@params);
        return Ok(result);
    }
}
