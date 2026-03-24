using System.Text.Json;
using DataLabelProject.Business.Models;
using DataLabelProject.Data;

namespace DataLabelProject.Business.Services.ActivityLogs;

public class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _context;

    public ActivityLogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        Guid projectId,
        Guid? userId,
        string eventType,
        string targetEntity,
        Guid? targetId,
        object? details = null)
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
