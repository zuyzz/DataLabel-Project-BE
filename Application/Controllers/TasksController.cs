using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DataLabelProject.Application.DTOs.Tasks;
using DataLabelProject.Business.Services.Tasks;
using DataLabelProject.Business.Services.Assignments;
using DataLabelProject.Business.Services.TaskItems;

namespace DataLabelProject.Application.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ILabelingTaskService _taskService;
    private readonly IAssignmentService _assignmentService;
    private readonly ITaskItemService _taskItemService;

    public TasksController(
        ILabelingTaskService taskService,
        IAssignmentService assignmentService,
        ITaskItemService taskItemService)
    {
        _taskService = taskService;
        _assignmentService = assignmentService;
        _taskItemService = taskItemService;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    /// <summary>Get tasks (filter by status/isExpired)</summary>
    [HttpGet]
    public async Task<IActionResult> GetTasks([FromQuery] TaskQueryParameters @params)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();
            var result = await _taskService.GetTasksAsync(userId, userRole, @params);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get items of a task (filter by status/isExpired)</summary>
    [HttpGet("{taskId:guid}/items")]
    [Authorize]
    public async Task<IActionResult> GetTaskItems(Guid taskId, [FromQuery] TaskItemQueryParameters @params)
    {
        try
        {
            var result = await _taskItemService.GetTaskItemsByTaskAsync(taskId, @params);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Assign dataset to a user (manager only)</summary>
    [HttpPost("assign")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> AssignTask([FromBody] BulkAssignTaskRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid input data", errors = ModelState });

        try
        {
            var assignedBy = GetCurrentUserId();
            var result = await _assignmentService.AssignTaskAsync(request, assignedBy);

            return StatusCode(201, result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get task by ID</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetTaskById(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();
            var task = await _taskService.GetTaskByIdForUserAsync(id, userId, userRole);

            if (task == null)
                return NotFound(new { message = "Task not found or not accessible" });

            var response = new TaskResponse
            {
                TaskId = task.TaskId,
                ProjectId = task.ProjectId,
                Status = task.Status,
                TaskItems = task.TaskItems?.Select(i => new TaskItemResponse
                {
                    TaskItemId = i.TaskItemId,
                    DatasetItemId = i.DatasetItemId,
                    TaskId = i.TaskId,
                    Status = i.Status
                }).ToList() ?? new List<TaskItemResponse>()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get task by project ID</summary>
    [HttpGet("projects/{projectId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetTaskByProjectId(Guid projectId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();
            var task = await _taskService.GetTaskByIdForUserAsync(projectId, userId, userRole);

            if (task == null)
                return NotFound(new { message = "Task not found or not accessible" });

            var response = new TaskResponse
            {
                TaskId = task.TaskId,
                ProjectId = task.ProjectId,
                Status = task.Status,
                TaskItems = task.TaskItems?.Select(i => new TaskItemResponse
                {
                    TaskItemId = i.TaskItemId,
                    DatasetItemId = i.DatasetItemId,
                    TaskId = i.TaskId,
                    Status = i.Status
                }).ToList() ?? new List<TaskItemResponse>()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Assign task items to a task (manager only)</summary>
    [HttpPost("{id:guid}/assign-items")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> AssignTaskItems(Guid id, [FromBody] AssignTaskItemsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid input data", errors = ModelState });

        try
        {
            var taskItems = await _taskService.AssignTaskItemsToTaskAsync(id, request.TaskItemIds);

            var response = taskItems.Select(i => new TaskItemResponse
            {
                TaskItemId = i.TaskItemId,
                DatasetItemId = i.DatasetItemId,
                TaskId = i.TaskId,
                Status = i.Status
            }).ToList();

            return Ok(new { message = "Task items assigned successfully", data = response });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
