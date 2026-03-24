using DataLabelProject.Application.DTOs.Common;

namespace DataLabelProject.Application.DTOs.ActivityLogs
{
    public class ActivityLogQueryParameters : PaginationParameters
    {
        public Guid? ProjectId { get; set; }
        public string? EventType { get; set; }
        public string? TargetEntity { get; set; }
    }
}
