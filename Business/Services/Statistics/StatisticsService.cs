using System.Text.Json;
using DataLabelProject.Application.DTOs.Statistics;
using DataLabelProject.Business.Models.Enums;
using DataLabelProject.Data;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Statistics;

public class StatisticsService : IStatisticsService
{
    private readonly AppDbContext _context;

    public StatisticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ProjectExistsAsync(Guid projectId)
    {
        return await _context.Projects.AnyAsync(p => p.ProjectId == projectId);
    }

    public async Task<bool> IsProjectMemberAsync(Guid projectId, Guid userId)
    {
        return await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.MemberId == userId);
    }

    // =============== Project Overview ===============

    public async Task<ProjectOverviewDto> GetProjectOverviewAsync(Guid projectId)
    {
        var totalMembers = await _context.ProjectMembers
            .AsNoTracking()
            .CountAsync(pm => pm.ProjectId == projectId);

        var totalLabels = await _context.ProjectLabels
            .AsNoTracking()
            .CountAsync(pl => pl.ProjectId == projectId);

        var totalDatasets = await _context.Datasets
            .AsNoTracking()
            .CountAsync(d => d.ProjectId == projectId);

        var totalDatasetItems = await _context.DatasetItems
            .AsNoTracking()
            .CountAsync(di => 
                di.ItemDataset.DatasetProject != null && 
                di.ItemDataset.DatasetProject.ProjectId == projectId
            );

        var totalTaskItems = await _context.LabelingTaskItems
            .AsNoTracking()
            .CountAsync(ti => ti.ProjectId == projectId);

        var completedTaskItems = await _context.LabelingTaskItems
            .AsNoTracking()
            .CountAsync(ti => ti.ProjectId == projectId && ti.Status == LabelingTaskItemStatus.Completed);

        var progress = totalTaskItems == 0 ? 0f : completedTaskItems / (float)totalTaskItems;

        return new ProjectOverviewDto
        {
            TotalMembers = totalMembers,
            TotalLabels = totalLabels,
            TotalDatasets = totalDatasets,
            TotalDatasetItems = totalDatasetItems,
            CompletedTaskItems = completedTaskItems,
            TotalTaskItems = totalTaskItems,
            Progress = progress
        };
    }

    // =============== Reviewer Stats ===============

    public async Task<ReviewerStatsDto> GetReviewerStatsAsync(Guid currentUserId)
    {
        var assignedTaskItemIds = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.AssignedTo == currentUserId)
            .Join(_context.LabelingTasks, a => a.TaskId, t => t.TaskId, (a, t) => t)
            .SelectMany(t => t.TaskItems.Select(ti => ti.TaskItemId))
            .Distinct()
            .ToListAsync();

        var totalItems = assignedTaskItemIds.Count;

        var reviews = await _context.Reviews
            .AsNoTracking()
            .Where(r => assignedTaskItemIds.Contains(r.TaskItemId) && r.ReviewerId == currentUserId)
            .ToListAsync();

        var approvedItems = reviews.Count(r => r.Result == ReviewResult.Approved);
        var rejectedItems = reviews.Count(r => r.Result == ReviewResult.Rejected);
        var incompletedItems = totalItems - reviews.Count;

        var today = DateTime.UtcNow.Date;
        var todayReviews = reviews.Count(r => r.ReviewedAt.Date == today);

        return new ReviewerStatsDto
        {
            TotalItems = totalItems,
            ApprovedItems = approvedItems,
            RejectedItems = rejectedItems,
            IncompletedItems = incompletedItems,
            TodayReviews = todayReviews
        };
    }

    // =============== Annotator Stats ===============

    public async Task<AnnotatorStatsDto> GetAnnotatorStatsAsync(Guid currentUserId)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        var assignedTaskItemsQuery = _context.Assignments
            .AsNoTracking()
            .Where(a => a.AssignedTo == currentUserId)
            .Join(
                _context.LabelingTasks,
                a => a.TaskId,
                t => t.TaskId,
                (a, t) => new { Assignment = a, Task = t }
            )
            .SelectMany(
                x => x.Task.TaskItems,
                (x, ti) => new
                {
                    TaskItem = ti,
                    DeadlineAt = x.Assignment.DeadlineAt
                }
            );

        var annotationsQuery = _context.Annotations
            .AsNoTracking()
            .Where(a =>
                a.AnnotatorId == currentUserId &&
                assignedTaskItemsQuery
                    .Select(ti => ti.TaskItem.TaskItemId)
                    .Contains(a.TaskItemId)
            );

        var totalItems = await assignedTaskItemsQuery
            .Select(x => x.TaskItem.TaskItemId)
            .Distinct()
            .CountAsync();

        var submittedItems = await annotationsQuery
            .CountAsync(a => a.Status == AnnotationStatus.Submitted);

        var completedItems = await annotationsQuery
            .CountAsync(a => 
                a.Status == AnnotationStatus.Conflicted ||
                a.Status == AnnotationStatus.Resolved
            );

        var skippedItems = await annotationsQuery
            .CountAsync(a => a.Status == AnnotationStatus.Skipped);

        var expiredItems = await assignedTaskItemsQuery
            .Where(x => 
                x.DeadlineAt < now
                && !_context.Annotations.Any(a =>
                    a.TaskItemId == x.TaskItem.TaskItemId &&
                    a.AnnotatorId == currentUserId)
            )
            .CountAsync();

        var incompletedItems = totalItems - expiredItems - submittedItems - completedItems - skippedItems;

        var todayCount = await annotationsQuery.CountAsync(a =>
            a.SubmittedAt.HasValue &&
            a.SubmittedAt.Value.Date == today
        );

        return new AnnotatorStatsDto
        {
            TotalItems = totalItems,
            SubmittedItems = submittedItems,
            CompletedItems = completedItems,
            SkippedItems = skippedItems,
            IncompletedItems = incompletedItems,
            ExpiredItems = expiredItems,
            TodayAnnotationCount = todayCount
        };
    }

    // =============== Manager Stats ===============

    public async Task<ManagerStatsDto> GetManagerStatsAsync(Guid currentUserId)
    {
        var now = DateTime.UtcNow;
        var todayDate = DateOnly.FromDateTime(now);

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.CreatedBy == currentUserId)
            .Select(p => new { p.ProjectId, p.IsActive })
            .ToListAsync();

        var projectIds = projects.Select(p => p.ProjectId).ToList();

        var totalProjects = projects.Count;
        var activeProjects = projects.Count(p => p.IsActive);

        var tasksByProject = await _context.LabelingTasks
            .AsNoTracking()
            .Where(t => projectIds.Contains(t.ProjectId))
            .GroupBy(t => t.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                HasOpened = g.Any(t => t.Status == LabelingTaskStatus.Opened),
                AllClosed = g.All(t => t.Status == LabelingTaskStatus.Closed),
                HasAny = g.Any()
            })
            .ToListAsync();

        var incompletedProjects = tasksByProject.Count(t => t.HasOpened);
        var completedProjects = tasksByProject.Count(t => t.HasAny && t.AllClosed);

        var last7Days = Enumerable.Range(0, 7)
            .Select(i => todayDate.AddDays(-6 + i))
            .ToList();

        var startDate = last7Days.First().ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var taskItemIds = await _context.LabelingTaskItems
            .AsNoTracking()
            .Where(ti => projectIds.Contains(ti.ProjectId))
            .Select(ti => ti.TaskItemId)
            .ToListAsync();

        var annotationsByDate = await _context.Annotations
            .AsNoTracking()
            .Where(a =>
                taskItemIds.Contains(a.TaskItemId) &&
                a.SubmittedAt != null &&
                a.SubmittedAt >= startDate
            )
            .GroupBy(a => a.SubmittedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var reviewsByDate = await _context.Reviews
            .AsNoTracking()
            .Where(r =>
                taskItemIds.Contains(r.TaskItemId) &&
                r.ReviewedAt >= startDate
            )
            .GroupBy(r => r.ReviewedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var annotationDict = annotationsByDate
            .ToDictionary(a => DateOnly.FromDateTime(a.Date), a => a.Count);

        var reviewDict = reviewsByDate
            .ToDictionary(r => DateOnly.FromDateTime(r.Date), r => r.Count);

        var weeklyAnnotations = annotationDict.Values.Sum();
        var weeklyReviews = reviewDict.Values.Sum();

        var todayAnnotations = annotationDict.GetValueOrDefault(todayDate, 0);
        var todayReviews = reviewDict.GetValueOrDefault(todayDate, 0);

        var weeklyPerformance = last7Days.Select(day =>
        {
            var annotationCount = annotationDict.GetValueOrDefault(day, 0);
            var reviewCount = reviewDict.GetValueOrDefault(day, 0);

            return new WeeklyPerformanceDto
            {
                Date = day,

                Annotations = annotationCount,
                AnnotationRate = weeklyAnnotations > 0
                    ? (int)Math.Round(annotationCount * 100.0 / weeklyAnnotations)
                    : 0,

                Reviews = reviewCount,
                ReviewRate = weeklyReviews > 0
                    ? (int)Math.Round(reviewCount * 100.0 / weeklyReviews)
                    : 0
            };
        }).ToList();

        return new ManagerStatsDto
        {
            TotalProjects = totalProjects,
            ActiveProjects = activeProjects,
            IncompletedProjects = incompletedProjects,
            CompletedProjects = completedProjects,

            WeeklyAnnotations = weeklyAnnotations,
            WeeklyReviews = weeklyReviews,
            TodayAnnotations = todayAnnotations,
            TodayReviews = todayReviews,

            WeeklyPerformance = weeklyPerformance
        };
    }

    // =============== System Statistics ===============

    public async Task<SystemOverviewResponse> GetSystemOverviewAsync()
    {
        return new SystemOverviewResponse
        {
            Users = await _context.Users.CountAsync(),
            Projects = await _context.Projects.CountAsync(),
            Datasets = await _context.Datasets.CountAsync(),
            DatasetItems = await _context.DatasetItems.CountAsync(),
            Annotations = await _context.Annotations.CountAsync(),
            ConsensusGenerated = await _context.Consensuses.CountAsync()
        };
    }

    public async Task<List<ActiveProjectResponse>> GetActiveProjectsAsync()
    {
        var today = DateTime.UtcNow.Date;

        return await (
            from a in _context.Annotations.AsNoTracking()
            join ti in _context.LabelingTaskItems on a.TaskItemId equals ti.TaskItemId
            join p in _context.Projects on ti.ProjectId equals p.ProjectId
            where a.SubmittedAt >= today
            group a by new { p.ProjectId, p.Name } into g
            select new ActiveProjectResponse
            {
                ProjectId = g.Key.ProjectId,
                Name = g.Key.Name,
                AnnotationsToday = g.Count(),
                ActiveAnnotators = g.Select(a => a.AnnotatorId).Distinct().Count()
            }
        ).ToListAsync();
    }

    public async Task<List<ActivityTimelineResponse>> GetActivityTimelineAsync(int days)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days);

        var data = await _context.Annotations
            .AsNoTracking()
            .Where(a => a.SubmittedAt != null && a.SubmittedAt >= startDate)
            .GroupBy(a => a.SubmittedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return data.Select(d => new ActivityTimelineResponse
        {
            Date = DateOnly.FromDateTime(d.Date),
            Annotations = d.Count
        }).ToList();
    }
}
