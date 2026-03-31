using DataLabelProject.Application.DTOs.Tasks;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Models.Enums;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Business.Services.ActivityLogs;
using DataLabelProject.Data.Repositories.Abstractions;

namespace DataLabelProject.Business.Services.Assignments;

public class AssignmentService : IAssignmentService
{
    private readonly ILabelingTaskRepository _taskRepo;
    private readonly ILabelingTaskItemRepository _taskItemRepo;
    private readonly IAssignmentRepository _assignmentRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRoleRepository _roleRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IProjectMemberRepository _projectMemberRepo;
    private readonly IDatasetRepository _datasetRepo;
    private readonly IDatasetItemRepository _datasetItemRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly IActivityLogService _activityLog;

    public AssignmentService(
        ILabelingTaskRepository taskRepo,
        ILabelingTaskItemRepository taskItemRepo,
        IAssignmentRepository assignmentRepo,
        IUserRepository userRepo,
        IRoleRepository roleRepo,
        IProjectRepository projectRepo,
        IProjectMemberRepository projectMemberRepo,
        IDatasetRepository datasetRepo,
        IDatasetItemRepository datasetItemRepo,
        ICurrentUserService currentUserService,
        IActivityLogService activityLog)
    {
        _taskRepo = taskRepo;
        _taskItemRepo = taskItemRepo;
        _assignmentRepo = assignmentRepo;
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _datasetRepo = datasetRepo;
        _datasetItemRepo = datasetItemRepo;
        _currentUserService = currentUserService;
        _activityLog = activityLog;
    }

    public async Task<BulkTaskAssignmentResponse> AssignTaskAsync(
        BulkAssignTaskRequest request, 
        Guid assignedBy)
    {
        // ===== 1. Validate project =====
        var project = await _projectRepo.GetByIdAsync(request.ProjectId)
            ?? throw new Exception("Project not found");

        // ===== 2. Validate dataset =====
        var dataset = await _datasetRepo.GetByIdAsync(request.DatasetId)
            ?? throw new Exception("Dataset not found");

        if (dataset.ProjectId != request.ProjectId)
            throw new Exception("Dataset does not belong to the specified project");

        // ===== 3. Validate request assignments =====
        if (request.Assigments == null || !request.Assigments.Any())
            throw new Exception("Assignments cannot be empty");

        foreach (var asm in request.Assigments)
        {
            if (asm.StartedAt >= asm.DeadlineAt)
                throw new Exception("StartedAt must be before DeadlineAt");
        }

        // ===== 4. Get dataset items =====
        var datasetItems = await _datasetItemRepo.GetAllByDatasetIdAsync(request.DatasetId);
        var datasetItemIds = datasetItems.Select(x => x.DatasetItemId).ToList();

        if (!datasetItemIds.Any())
            throw new Exception("No dataset items found");

        // ===== 5. Get unassigned task items =====
        var taskItems = await _taskItemRepo.GetUnassignedByDatasetItemIdsAsync(datasetItemIds);

        if (!taskItems.Any())
            throw new Exception("No unassigned task items found");

        // ===== 6. Validate users in batch (NO N+1) =====
        var userIds = request.Assigments.Select(x => x.AssignedTo).Distinct().ToList();

        var users = await _userRepo.GetByIdsAsync(userIds);
        if (users.Count != userIds.Count)
            throw new Exception("One or more users not found");

        var projectMembers = await _projectMemberRepo
            .GetByProjectIdAsync(request.ProjectId);

        var memberIds = projectMembers.Select(x => x.MemberId).ToHashSet();

        var roles = await _roleRepo.GetAllAsync();
        var validRoleIds = roles
            .Where(r => r.RoleName == "reviewer" || r.RoleName == "annotator")
            .Select(r => r.RoleId)
            .ToHashSet();

        foreach (var user in users)
        {
            if (!memberIds.Contains(user.UserId))
                throw new Exception($"User {user.UserId} is not in project");

            if (!validRoleIds.Contains(user.RoleId))
                throw new Exception($"User {user.UserId} must be reviewer or annotator");
        }

        // ===== 7. Create task =====
        var task = new LabelingTask
        {
            TaskId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            Status = LabelingTaskStatus.Opened
        };

        await _taskRepo.AddAsync(task);

        // ===== 8. Assign task items =====
        foreach (var item in taskItems)
        {
            item.TaskId = task.TaskId;
            item.Status = LabelingTaskItemStatus.Assigned;
        }

        await _taskItemRepo.UpdateRangeAsync(taskItems);

        // ===== 9. Create assignments =====
        var assignments = request.Assigments.Select(a => new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            TaskId = task.TaskId,
            AssignedTo = a.AssignedTo,
            AssignedBy = assignedBy,
            AssignedAt = DateTime.UtcNow,
            StartedAt = a.StartedAt,
            DeadlineAt = a.DeadlineAt
        }).ToList();

        await _assignmentRepo.AddRangeAsync(assignments);

        // ===== 10. Deactivate dataset (AFTER all validations pass) =====
        dataset.IsActive = false;
        await _datasetRepo.UpdateAsync(dataset);

        await _taskRepo.SaveChangesAsync();
        await _taskItemRepo.SaveChangesAsync();
        await _assignmentRepo.SaveChangesAsync();
        await _datasetRepo.SaveChangesAsync();

        await _activityLog.LogAsync(request.ProjectId, assignedBy, "TASK_CREATED", "LabelingTask", task.TaskId, null);

        foreach (var a in assignments)
            await _activityLog.LogAsync(request.ProjectId, assignedBy, "ASSIGNMENT_CREATED", "Assignment", a.AssignmentId, new { assignedTo = a.AssignedTo });

        return new BulkTaskAssignmentResponse
        {
            TaskId = task.TaskId,
            ProjectId = task.ProjectId,
            Status = task.Status,
            Assignments = assignments.Select(a => new AssignmentResponse
            {
                AssignmentId = a.AssignmentId,
                TaskId = a.TaskId,
                AssignedTo = a.AssignedTo,
                AssignedBy = a.AssignedBy,
                AssignedAt = a.AssignedAt,
                StartedAt = a.StartedAt,
                DeadlineAt = a.DeadlineAt
            }).ToList()
        };
    }
}
