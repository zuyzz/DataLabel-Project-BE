using DataLabelProject.Business.Models.Enums;

namespace DataLabelProject.Application.DTOs.Annotations;

public class AnnotationResponse
{
    public Guid AnnotationId { get; set; }
    public Guid TaskItemId { get; set; }
    public Guid AnnotatorId { get; set; }
    public object? Payload { get; set; } = null!;
    public AnnotationStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public class TaskItemAnnotationsResponse
{
    public Guid TaskItemId { get; set; }
    public Guid DatasetItemId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<AnnotationResponse> Annotations { get; set; } = new();
}
