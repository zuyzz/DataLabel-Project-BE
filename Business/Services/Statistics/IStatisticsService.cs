using DataLabelProject.Application.DTOs.Statistics;

namespace DataLabelProject.Business.Services.Statistics;

public interface IStatisticsService
{
    // System-scoped
    Task<SystemOverviewResponse> GetSystemOverviewAsync();
    Task<List<ActiveProjectResponse>> GetActiveProjectsAsync();
    Task<List<ActivityTimelineResponse>> GetActivityTimelineAsync(int days);

    // New role-based methods
    Task<ProjectOverviewDto> GetProjectOverviewAsync(Guid projectId);
    Task<ReviewerStatsDto> GetReviewerStatsAsync(Guid currentUserId);
    Task<AnnotatorStatsDto> GetAnnotatorStatsAsync(Guid currentUserId);
    Task<ManagerStatsDto> GetManagerStatsAsync(Guid currentUserId);

    // Authorization helpers
    Task<bool> ProjectExistsAsync(Guid projectId);
    Task<bool> IsProjectMemberAsync(Guid projectId, Guid userId);
}
