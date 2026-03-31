using DataLabelProject.Business.Models.Enums;

namespace DataLabelProject.Application.DTOs.Tasks;

public class TaskAssignmentResponse
{
    public Guid TaskId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid AssignedTo { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public LabelingTaskStatus Status { get; set; }
}

public class BulkTaskAssignmentResponse
{
    public Guid TaskId { get; set; }
    public Guid ProjectId { get; set; }
    public LabelingTaskStatus Status { get; set; }
    public List<AssignmentResponse> Assignments { get; set; } = new();
}
