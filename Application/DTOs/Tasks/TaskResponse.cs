using DataLabelProject.Business.Models.Enums;

namespace DataLabelProject.Application.DTOs.Tasks;

public class TaskItemResponse
{
    public Guid TaskItemId { get; set; }
    public Guid DatasetItemId { get; set; }
    public Guid? TaskId { get; set; }
    public LabelingTaskItemStatus Status { get; set; }
}

public class TaskResponse
{
    public Guid TaskId { get; set; }
    public Guid ProjectId { get; set; }
    public LabelingTaskStatus Status { get; set; }
    public List<TaskItemResponse> TaskItems { get; set; } = new();
}
