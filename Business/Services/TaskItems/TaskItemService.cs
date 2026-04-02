using System.Text.Json;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Datasets;
using DataLabelProject.Application.DTOs.Tasks;
using DataLabelProject.Business.Services.Assignments;
using DataLabelProject.Data.Repositories.Abstractions;

namespace DataLabelProject.Business.Services.TaskItems;

public class TaskItemService : ITaskItemService
{
    private readonly ILabelingTaskRepository _taskRepo;
    private readonly ILabelingTaskItemRepository _taskItemRepo;
    private readonly IAssignmentRepository _assignmentRepo;
    private readonly IAssignmentService _assignmentService;

    public TaskItemService(
        ILabelingTaskRepository taskRepo,
        ILabelingTaskItemRepository taskItemRepo,
        IAssignmentRepository assignmentRepo,
        IAssignmentService assignmentService)
    {
        _taskRepo = taskRepo;
        _taskItemRepo = taskItemRepo;
        _assignmentRepo = assignmentRepo;
        _assignmentService = assignmentService;
    }

    public async Task<PagedResponse<TaskItemDetailResponse>> GetTaskItemsByTaskAsync(Guid taskId, TaskItemQueryParameters @params)
    {
        // Validate task exists
        var task = await _taskRepo.GetByIdAsync(taskId);
        if (task == null)
            throw new Exception("Task not found");

        // Get task items
        var taskItems = await _taskItemRepo.GetByTaskIdAsync(taskId);

        // Get assignment to compute deadline
        var assignment = await _assignmentRepo.GetByTaskIdAsync(taskId);
        if (assignment == null)
            throw new Exception("Assignment not found for this task");

        var now = DateTime.UtcNow;

        var result = new List<TaskItemDetailResponse>();

        foreach (var item in taskItems)
        {
            // Filter by expiration status
            if (@params.IsAvailable.HasValue)
            {
                if (@params.IsAvailable.Value == true)
                {
                    if (now > assignment.DeadlineAt && now < assignment.StartedAt)
                        continue;
                }
                else
                {
                    if (now < assignment.DeadlineAt && now > assignment.StartedAt)
                        continue;
                }
            }

            // Filter by status
            if (@params.Status.HasValue && item.Status != @params.Status.Value)
                continue;

            ImageMetadata? metadata = null;

            if (!string.IsNullOrWhiteSpace(item.DatasetItem.Metadata))
            {
                try
                {
                    metadata = JsonSerializer.Deserialize<ImageMetadata>(item.DatasetItem.Metadata);
                }
                catch (JsonException)
                {
                    // log invalid metadata if needed
                }
            }

            result.Add(new TaskItemDetailResponse
            {
                TaskItemId = item.TaskItemId,
                ProjectId = item.ProjectId,
                DatasetItem = new DatasetItemResponse
                {
                    ItemId = item.DatasetItem.DatasetItemId,
                    StorageUri = item.DatasetItem.StorageUri,
                    Metadata = metadata,
                    DatasetId = item.DatasetItem.DatasetId,
                    MediaType = item.DatasetItem.MediaType,
                    CreatedAt = item.DatasetItem.CreatedAt
                },
                Status = item.Status.ToString()
            });
        }

        // Pagination
        var totalCount = result.Count;
        var paginatedItems = result
            .Skip((@params.Page - 1) * @params.PageSize)
            .Take(@params.PageSize)
            .ToList();

        return new PagedResponse<TaskItemDetailResponse>
        {
            Items = paginatedItems,
            Page = @params.Page,
            PageSize = @params.PageSize,
            TotalItems = totalCount,
        };
    }
}
