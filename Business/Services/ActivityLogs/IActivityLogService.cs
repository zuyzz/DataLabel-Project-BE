using DataLabelProject.Application.DTOs.ActivityLogs;
using DataLabelProject.Application.DTOs.Common;

namespace DataLabelProject.Business.Services.ActivityLogs;

public interface IActivityLogService
{
    Task LogAsync<TDetails>(
        Guid? projectId,
        Guid? userId,
        string eventType,
        string targetEntity,
        Guid? targetId,
        TDetails? details = default) where TDetails : class;

    Task<PagedResponse<ActivityLogResponse>> GetActivityLogs(ActivityLogQueryParameters @params);
}
