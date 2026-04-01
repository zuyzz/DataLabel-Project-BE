using System.Text.Json;
using DataLabelProject.Application.DTOs.ActivityLogs;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Business.Models;
using DataLabelProject.Data;
using DataLabelProject.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.ActivityLogs;

public class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _context;
    private readonly IActivityLogMessageBuilder _messageBuilder;

    public ActivityLogService(AppDbContext context, IActivityLogMessageBuilder messageBuilder)
    {
        _context = context;
        _messageBuilder = messageBuilder;
    }

    public async Task<PagedResponse<ActivityLogResponse>> GetActivityLogs(ActivityLogQueryParameters @params)
    {
        var query = _context.ActivityLogs
            .AsNoTracking()
            .Include(a => a.Project)
            .Include(a => a.ActivityLogUser)
                .ThenInclude(u => u.UserRole)
            .OrderByDescending(a => a.CreatedAt)
            .AsQueryable();

        if (@params.ProjectId.HasValue)
            query = query.Where(a => a.ProjectId == @params.ProjectId.Value);

        if (!string.IsNullOrWhiteSpace(@params.EventType))
            query = query.Where(a => a.EventType == @params.EventType);

        if (!string.IsNullOrWhiteSpace(@params.TargetEntity))
            query = query.Where(a => a.TargetEntity == @params.TargetEntity);

        return await query.ToPagedResponseAsync(@params, a => new ActivityLogResponse
        {
            ActivityLogId = a.ActivityLogId,
            ProjectId = a.ProjectId,
            ProjectName = a.Project?.Name,
            UserId = a.UserId,
            Username = a.ActivityLogUser?.Username,
            UserRole = a.ActivityLogUser?.UserRole?.RoleName,
            EventType = a.EventType,
            TargetEntity = a.TargetEntity,
            TargetId = a.TargetId,
            Details = a.Details,
            Message = _messageBuilder.Build(a),
            CreatedAt = a.CreatedAt
        });
    }

    public async Task LogAsync<TDetails>(
        Guid? projectId,
        Guid? userId,
        string eventType,
        string targetEntity,
        Guid? targetId,
        TDetails? details = default) where TDetails : class
    {
        var log = new ActivityLog
        {
            ProjectId = projectId,
            UserId = userId,
            EventType = eventType,
            TargetEntity = targetEntity,
            TargetId = targetId,
            Details = details != null ? JsonSerializer.Serialize(details) : null,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ActivityLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }
}
