namespace DataLabelProject.Application.DTOs.ActivityLogs
{
    public record ActivityLogResponse
    {
        public Guid ActivityLogId { get; init; }
        public Guid? ProjectId { get; init; }
        public string? ProjectName { get; init; }
        public Guid? UserId { get; init; }
        public string? Username { get; init; }
        public string? UserRole { get; init; }
        public string EventType { get; init; } = null!;
        public string? TargetEntity { get; init; }
        public Guid? TargetId { get; init; }
        public string? Details { get; init; }
        public string? Message { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
