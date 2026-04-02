using DataLabelProject.Application.DTOs.Projects;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Business.Services.ActivityLogs.Constant;
using DataLabelProject.Business.Models;
using DataLabelProject.Data.Repositories.Abstractions;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Business.Events.Abstraction;
using DataLabelProject.Business.Events.DomainEvents.Project;
using DataLabelProject.Business.Services.ActivityLogs;
using Microsoft.EntityFrameworkCore;
using DataLabelProject.Shared.Extensions;

namespace DataLabelProject.Business.Services.Projects;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProjectConfigRepository _configRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IActivityLogService _activityLog;

    public ProjectService (
        IProjectRepository projectRepository,
        IProjectMemberRepository memberRepository,
        ICategoryRepository categoryRepository,
        IProjectConfigRepository configRepository,
        ICurrentUserService currentUserService,
        IEventDispatcher eventDispatcher,
        IActivityLogService activityLog)
    {
        _projectRepository = projectRepository;
        _memberRepository = memberRepository;
        _categoryRepository = categoryRepository;
        _configRepository = configRepository;
        _currentUserService = currentUserService;
        _eventDispatcher = eventDispatcher;
        _activityLog = activityLog;
    }

    public async Task<PagedResponse<ProjectResponse>> GetProjects(
        ProjectQueryParameters @params)
    {
        IQueryable<Project> query = _projectRepository.Query()
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Include(p => p.ProjectCategory);

        query = ApplyUserFilter(query);
        query = ApplyParamFilters(query, @params);

        return await query.ToPagedResponseAsync(@params, MapToResponse);
    }

    public async Task<PagedResponse<ProjectResponse>> GetCategoryProjects(
        Guid categoryId,
        ProjectQueryParameters @params)
    {
        IQueryable<Project> query = _projectRepository.Query()
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId)
            .OrderByDescending(p => p.CreatedAt)
            .Include(p => p.ProjectCategory);

        query = ApplyParamFilters(query, @params);

        return await query.ToPagedResponseAsync(@params, MapToResponse);
    }

    private IQueryable<Project> ApplyUserFilter(
        IQueryable<Project> query)
    {
        var currentUserId = _currentUserService.UserId;
        var currentUserRoles = _currentUserService.Roles;

        if (!currentUserRoles.Contains("admin") && currentUserId.HasValue)
        {
            query = query.Where(p => 
                p.ProjectMembers.Any(pm => 
                    pm.MemberId == currentUserId.Value));
        }

        return query;
    }

    private IQueryable<Project> ApplyParamFilters(
        IQueryable<Project> query,
        ProjectQueryParameters @params)
    {
        if (!string.IsNullOrWhiteSpace(@params.Name))
        {
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{@params.Name}%"));
        }

        if (@params.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == @params.IsActive);
        }

        return query;
    }

    public async Task<ProjectResponse?> GetProjectById(Guid id)
    {
        var currentUserId = _currentUserService.UserId;
        var currentUserRoles = _currentUserService.Roles;

        var project = await _projectRepository.GetByIdAsync(id);

        if (project == null)
            return null;

        if (!currentUserRoles.Contains("admin") && currentUserId.HasValue)
        {
            var member = await _memberRepository.GetByIdAsync(id, currentUserId.Value);

            if (member == null)
                throw new UnauthorizedAccessException("You are not allowed to access this project");
        }

        return MapToResponse(project);
    }

    public async Task<ProjectResponse> CreateProject(CreateProjectRequest request)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        if (category == null)
            throw new InvalidOperationException("Category not found");

        var currentUserId = _currentUserService.UserId!.Value;

        var exists = await _projectRepository.GetByNameAndCreatorAsync(request.Name, currentUserId);
        if (exists != null)
            throw new InvalidOperationException("You already have a project with the same name");

        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CategoryId = request.CategoryId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };

        await _projectRepository.CreateAsync(project);
        await _projectRepository.SaveChangesAsync();

        await _eventDispatcher.DispatchAsync(new ProjectCreatedEvent(project.ProjectId));

        await _activityLog.LogAsync(project.ProjectId, currentUserId, ActivityEvents.ProjectCreated, ActivityTargets.Project, project.ProjectId, new ProjectCreatedDetails { ProjectName = project.Name });

        project = await _projectRepository.GetByIdAsync(project.ProjectId);

        return MapToResponse(project!);
    }

    public async Task<ProjectResponse?> UpdateProject(Guid id, UpdateProjectRequest request)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return null;

        var currentUserId = _currentUserService.UserId!.Value;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var exists = _projectRepository.GetByNameAndCreatorAsync(request.Name, currentUserId);
            if (exists != null)
                throw new InvalidOperationException("You already have a project with the same name");
            project.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            project.Description = request.Description;
        }

        if (request.IsActive.HasValue)
        {
            project.IsActive = request.IsActive.Value;
        }

        await _projectRepository.UpdateAsync(project);
        await _projectRepository.SaveChangesAsync();

        if (request.IsActive.HasValue && !request.IsActive.Value)
            await _activityLog.LogAsync<object>(project.ProjectId, currentUserId, ActivityEvents.ProjectDeactivated, ActivityTargets.Project, project.ProjectId, null);
        else
            await _activityLog.LogAsync<object>(project.ProjectId, currentUserId, ActivityEvents.ProjectUpdated, ActivityTargets.Project, project.ProjectId, null);

        return MapToResponse(project);
    }

    public async Task DeleteProject(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            throw new KeyNotFoundException("Project not found");

        await _projectRepository.DeleteAsync(project);
        await _projectRepository.SaveChangesAsync();

        await _eventDispatcher.DispatchAsync(new ProjectDeletedEvent(id));
    }

    private static ProjectResponse MapToResponse(Project p) =>
        new ProjectResponse
        {
            ProjectId = p.ProjectId,
            Name = p.Name,
            Description = p.Description,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            CreatedBy = p.CreatedBy,
            Category = new()
            {
                CategoryId = p.CategoryId,
                Name = p.ProjectCategory.Name,
                Description = p.ProjectCategory.Description,
                CreatedAt = p.ProjectCategory.CreatedAt,
                IsActive = p.ProjectCategory.IsActive
            }
        };
}