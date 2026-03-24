using DataLabelProject.Application.DTOs.Projects;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Business.Models;
using DataLabelProject.Data.Repositories.Abstractions;
using DataLabelProject.Shared.Extensions;
using Microsoft.EntityFrameworkCore;
using DataLabelProject.Business.Models.Enums;
using DataLabelProject.Application.DTOs.Users;
using DataLabelProject.Business.Services.ActivityLogs;

namespace DataLabelProject.Business.Services.Projects;

public class ProjectMemberService : IProjectMemberService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _projectMemberRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IActivityLogService _activityLog;

    public ProjectMemberService(
        IProjectRepository projectRepository,
        IProjectMemberRepository projectMemberRepository,
        IAssignmentRepository assignmentRepository,
        IActivityLogService activityLog)
    {
        _projectRepository = projectRepository;
        _projectMemberRepository = projectMemberRepository;
        _assignmentRepository = assignmentRepository;
        _activityLog = activityLog;
    }

    public async Task AddUserToProject(Guid userId, Guid projectId)
    {
        var existing = await _projectMemberRepository.GetByIdAsync(projectId, userId);
        if (existing != null)
            throw new InvalidOperationException("User already in project");

        var member = new ProjectMember
        {
            ProjectId = projectId,
            MemberId = userId,
            JoinedAt = DateTime.UtcNow
        };

        await _projectMemberRepository.CreateAsync(member);
        await _projectMemberRepository.SaveChangesAsync();

        await _activityLog.LogAsync(projectId, null, "MEMBER_ADDED", "ProjectMember", userId, new { memberId = userId });
    }

    public async Task RemoveUserFromProject(Guid userId, Guid projectId)
    {
        var member = await _projectMemberRepository.GetByIdAsync(projectId, userId);
        if (member == null)
            throw new KeyNotFoundException("Project member not found");

        await _projectMemberRepository.DeleteAsync(member);
        await _projectMemberRepository.SaveChangesAsync();

        await _activityLog.LogAsync(projectId, null, "MEMBER_REMOVED", "ProjectMember", userId, new { memberId = userId });
    }

    public async Task<PagedResponse<UserResponse>> GetUserFromProject(Guid projectId, ProjectMemberQueryParameters @params)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new InvalidOperationException("Project not found");

        IQueryable<ProjectMember> members = _projectMemberRepository.Query()
            .AsNoTracking()
            .Where(pm => pm.ProjectId == projectId)
            .OrderByDescending(pm => pm.JoinedAt);
        
        IQueryable<Assignment> assignments = _assignmentRepository.Query();

        if (@params.IsAvailable.HasValue)
        {
            members = members.Where(pm =>
                assignments.Any(a =>
                    a.AssignedTo == pm.MemberId &&
                    a.AssignmentTask.ProjectId == projectId &&
                    a.AssignmentTask.Status == LabelingTaskStatus.Opened) != @params.IsAvailable.Value);
        }           // HasOpenedTask                                      != IsAvailable

        if (!string.IsNullOrEmpty(@params.Username))
            members = members.Where(pm => EF.Functions.ILike(pm.ProjectMemberUser.Username, $"%{@params.Username.Trim()}%"));

        if (!string.IsNullOrEmpty(@params.DisplayName))
            members = members.Where(pm => EF.Functions.ILike(pm.ProjectMemberUser.DisplayName, $"%{@params.DisplayName.Trim()}%"));

        if (!string.IsNullOrEmpty(@params.Email))
            members = members.Where(pm => EF.Functions.ILike(pm.ProjectMemberUser.Email ?? "", $"%{@params.Email.Trim()}%"));

        if (!string.IsNullOrEmpty(@params.PhoneNumber))
            members = members.Where(pm => EF.Functions.ILike(pm.ProjectMemberUser.PhoneNumber ?? "", $"%{@params.PhoneNumber.Trim()}%"));

        if (@params.IsActive.HasValue)
            members = members.Where(pm => pm.ProjectMemberUser.IsActive == @params.IsActive.Value);

        var userIds = members.Select(pm => pm.MemberId).Distinct();
        var projectCounts = await _projectMemberRepository.Query()
            .Where(pm => userIds.Contains(pm.MemberId))
            .GroupBy(pm => pm.MemberId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        return await members.ToPagedResponseAsync(
            @params,
            pm => MapToResponse(pm, projectCounts)
        );
    }

    private static UserResponse MapToResponse(
        ProjectMember p,
        Dictionary<Guid, int> projectCounts)
    {
        return new UserResponse
        {
            UserId = p.ProjectMemberUser.UserId,
            Username = p.ProjectMemberUser.Username,
            DisplayName = p.ProjectMemberUser.DisplayName,
            Email = p.ProjectMemberUser.Email,
            PhoneNumber = p.ProjectMemberUser.PhoneNumber,
            RoleId = p.ProjectMemberUser.UserRole.RoleId,
            RoleName = p.ProjectMemberUser.UserRole.RoleName,
            IsActive = p.ProjectMemberUser.IsActive,
            JoinedProjectCount = projectCounts.TryGetValue(p.MemberId, out var count)
                ? count
                : 0
        };
    }
}