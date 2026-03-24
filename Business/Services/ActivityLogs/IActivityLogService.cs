using DataLabelProject.Application.DTOs.ActivityLogs;
using DataLabelProject.Application.DTOs.Common;

namespace DataLabelProject.Business.Services.ActivityLogs;

public interface IActivityLogService
{
    Task LogAsync(
        Guid projectId,
        Guid? userId,
        string eventType,
        string targetEntity,
        Guid? targetId,
        object? details = null);

    Task<PagedResponse<ActivityLogResponse>> GetActivityLogs(ActivityLogQueryParameters @params);
}
