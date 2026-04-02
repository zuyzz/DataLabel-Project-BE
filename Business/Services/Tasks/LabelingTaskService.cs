using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Tasks;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Models.Enums;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Data.Repositories.Abstractions;
using DataLabelProject.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Tasks;

public class LabelingTaskService : ILabelingTaskService
{
    private readonly ILabelingTaskRepository _taskRepo;
    private readonly IAssignmentRepository _assignmentRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRoleRepository _roleRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IProjectMemberRepository _projectMemberRepo;
    private readonly IDatasetItemRepository _datasetItemRepo;
    private readonly ILabelingTaskItemRepository _taskItemRepo;
    private readonly ICurrentUserService _currentUserService;

    public LabelingTaskService(
        ILabelingTaskRepository taskRepo,
        IAssignmentRepository assignmentRepo,
        IUserRepository userRepo,
        IRoleRepository roleRepo,
        IProjectRepository projectRepo,
        IProjectMemberRepository projectMemberRepo,
        IDatasetItemRepository datasetItemRepo,
        ILabelingTaskItemRepository taskItemRepo,
        ICurrentUserService currentUserService)
    {
        _taskRepo = taskRepo;
        _assignmentRepo = assignmentRepo;
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _datasetItemRepo = datasetItemRepo;
        _taskItemRepo = taskItemRepo;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResponse<TaskAssignmentResponse>> GetTasksAsync(
        Guid userId, string userRole, TaskQueryParameters @params)
    {
        var now = DateTime.UtcNow;

        List<Assignment> assignments;

        // Role-based assignment loading
        if (userRole == "reviewer" || userRole == "annotator")
            assignments = await _assignmentRepo.GetByAssignedToAsync(userId);
        else
            assignments = await _assignmentRepo.GetAllAsync();

        // Filter assignments first
        var filteredAssignments = assignments
            .Where(x =>
            {
                if (@params.IsAvailable == true)
                    return now > x.StartedAt && now < x.DeadlineAt;
                else if (@params.IsAvailable == false)
                    return now < x.DeadlineAt || now > x.DeadlineAt;
                else
                    return true;
            })
            .ToList();

        if (!filteredAssignments.Any())
        {
            return new PagedResponse<TaskAssignmentResponse>
            {
                Items = new List<TaskAssignmentResponse>(),
                Page = @params.Page,
                PageSize = @params.PageSize,
                TotalItems = 0,
            };
        }

        // Batch load tasks (same efficiency as function 1)
        var taskIds = filteredAssignments
            .Select(x => x.TaskId)
            .Distinct()
            .ToList();

        var tasks = await _taskRepo.GetByIdsAsync(taskIds);

        // Optional status filtering
        if (@params.Status.HasValue)
            tasks = tasks.Where(t => t.Status == @params.Status.Value).ToList();

        var taskDict = tasks.ToDictionary(t => t.TaskId);

        var results = new List<TaskAssignmentResponse>();

        foreach (var item in filteredAssignments)
        {
            if (!taskDict.TryGetValue(item.TaskId, out var task))
                continue;

            results.Add(new TaskAssignmentResponse
            {
                TaskId = task.TaskId,
                ProjectId = task.ProjectId,
                AssignedTo = item.AssignedTo,
                AssignedBy = item.AssignedBy,
                AssignedAt = item.AssignedAt,
                DeadlineAt = item.DeadlineAt,
                Status = task.Status
            });
        }

        // Sort by deadline DESC
        results = results
            .OrderByDescending(x => x.DeadlineAt)
            .ToList();

        // Pagination
        var totalCount = results.Count;

        var paginatedItems = results
            .Skip((@params.Page - 1) * @params.PageSize)
            .Take(@params.PageSize)
            .ToList();

        return new PagedResponse<TaskAssignmentResponse>
        {
            Items = paginatedItems,
            Page = @params.Page,
            PageSize = @params.PageSize,
            TotalItems = totalCount,
        };
    }

    public async Task<LabelingTask?> GetTaskByIdForUserAsync(Guid taskId, Guid userId, string userRole)
    {
        if (userRole == "admin" && userRole == "manager")
        {
            return await _taskRepo.GetByIdAsync(taskId);
        }

        var assignment = await _assignmentRepo.GetByTaskIdAndUserAsync(taskId, userId);
        if (assignment == null)
            return null;

        var now = DateTime.UtcNow;

        // Ignore unavailable assignments
        if (now > assignment.DeadlineAt || now < assignment.StartedAt)
            return null;

        // Fetch task only after assignment validation
        var task = await _taskRepo.GetByIdAsync(taskId);
        return task;
    }

    public async Task<List<LabelingTaskItem>> AssignTaskItemsToTaskAsync(Guid taskId, IEnumerable<Guid> taskItemIds)
    {
        var task = await _taskRepo.GetByIdAsync(taskId);
        if (task == null)
            throw new Exception("Task not found");

        var taskItems = await _taskItemRepo.GetByIdsAsync(taskItemIds);
        if (taskItems == null || taskItems.Count == 0)
            throw new Exception("No task items found");

        // Ensure all task items belong to the same project as the task
        if (taskItems.Any(ti => ti.ProjectId != task.ProjectId))
            throw new Exception("All task items must belong to the same project as the task");

        foreach (var taskItem in taskItems)
        {
            taskItem.TaskId = taskId;
            taskItem.Status = LabelingTaskItemStatus.Assigned;
        }

        await _taskItemRepo.UpdateRangeAsync(taskItems);
        await _taskItemRepo.SaveChangesAsync();

        return taskItems;
    }

    public async Task<PagedResponse<TaskItemResponse>> GetTaskItemsByProjectIdAsync(Guid projectId, TaskItemQueryParameters @params)
    {
        var currentUserId = _currentUserService.UserId;
        var currentUserRoles = _currentUserService.Roles;

        // Start with base query
        IQueryable<LabelingTaskItem> query = _taskItemRepo.Query()
            .Where(ti => ti.ProjectId == projectId);

        // Apply role-based filtering
        if (!currentUserRoles.Contains("admin") && !currentUserRoles.Contains("manager"))
        {
            // Reviewer/Annotator: only see task-items in tasks assigned to them
            query = query
                .Include(ti => ti.Task)
                    .ThenInclude(t => t!.Assignments)
                .Where(ti =>
                    ti.TaskId != null &&
                    ti.Task!.Assignments.Any(a => a.AssignedTo == currentUserId));
        }

        // Apply status filter
        if (@params.Status.HasValue)
        {
            query = query.Where(ti => ti.Status == @params.Status.Value);
        }

        // Apply isAvailable filter if needed
        // (Currently TaskItemQueryParameters has IsAvailable but TaskItem doesn't have deadline concept)
        // If you need this filter, you would need to join with Assignment and check deadline

        // Use ToPagedResponseAsync for pagination
        return await query.ToPagedResponseAsync(@params, MapToTaskItemResponse);
    }

    private TaskItemResponse MapToTaskItemResponse(LabelingTaskItem taskItem)
    {
        return new TaskItemResponse
        {
            TaskItemId = taskItem.TaskItemId,
            DatasetItemId = taskItem.DatasetItemId,
            TaskId = taskItem.TaskId,
            Status = taskItem.Status
        };
    }
}
