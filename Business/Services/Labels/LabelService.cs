using DataLabelProject.Application.DTOs.Categories;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Labels;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Business.Services.ActivityLogs;
using DataLabelProject.Data.Repositories.Abstractions;
using DataLabelProject.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Labels;

public class LabelService : ILabelService
{
    private readonly ILabelRepository _labelRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectLabelRepository _projectLabelRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IActivityLogService _activityLog;

    public LabelService(
        ILabelRepository labelRepository,
        IProjectRepository projectRepository,
        IProjectLabelRepository projectLabelRepository,
        ICurrentUserService currentUserService,
        IActivityLogService activityLog)
    {
        _labelRepository = labelRepository;
        _projectRepository = projectRepository;
        _projectLabelRepository = projectLabelRepository;
        _currentUserService = currentUserService;
        _activityLog = activityLog;
    }

    public async Task<PagedResponse<LabelResponse>> GetLabels(LabelQueryParameters @params)
    {
        IQueryable<Label> query = _labelRepository.Query()
            .Include(l => l.LabelCategory);

        query = ApplyParamFilters(query, @params);

        return await query.ToPagedResponseAsync(@params, MapToResponse);
    }

    public async Task<PagedResponse<LabelResponse>> GetProjectLabels(Guid projectId, LabelQueryParameters @params)
    {
        IQueryable<Label> query = _labelRepository.Query()
            .Where(l => l.ProjectLabels.Any(pl => pl.ProjectId == projectId))
            .Include(l => l.LabelCategory);

        query = ApplyUserFilter(query);
        query = ApplyParamFilters(query, @params);

        return await query.ToPagedResponseAsync(@params, MapToResponse);
    }

    public async Task<PagedResponse<LabelResponse>> GetCategoryLabels(Guid categoryId, LabelQueryParameters @params)
    {
        IQueryable<Label> query = _labelRepository.Query()
            .Where(l => l.IsActive)
            .Where(l => l.CategoryId == categoryId)
            .Include(l => l.LabelCategory);

        query = ApplyParamFilters(query, @params);

        return await query.ToPagedResponseAsync(@params, MapToResponse);
    }

    private IQueryable<Label> ApplyUserFilter(
        IQueryable<Label> query)
    {
        var currentUserId = _currentUserService.UserId;
        var currentUserRoles = _currentUserService.Roles;

        if (!currentUserRoles.Contains("admin") && currentUserId.HasValue)
        {
            query = query.Where(l =>
                l.ProjectLabels.Any(pl => 
                    pl.Project.ProjectMembers.Any(pm =>
                        pm.MemberId == currentUserId.Value)));
        }

        return query;
    }

    private IQueryable<Label> ApplyParamFilters(
        IQueryable<Label> query,
        LabelQueryParameters @params)
    {
        if (!string.IsNullOrWhiteSpace(@params.Name))
        {
            query = query.Where(l => l.Name.ToLower() == @params.Name.ToLower());
        }

        if (@params.IsActive.HasValue)
        {
            query = query.Where(l => l.IsActive == @params.IsActive.Value);
        }

        return query;
    }

    public async Task<LabelResponse?> GetLabelById(Guid id)
    {
        var label = await _labelRepository.GetByIdAsync(id);
        if (label == null) return null;

        return MapToResponse(label);
    }

    public async Task<LabelResponse> CreateLabel(CreateLabelRequest request)
    {
        var name = request.Name.Trim();

        var duplicateExists = await _labelRepository.Query()
            .AnyAsync(l => 
                l.CategoryId == request.CategoryId &&
                l.Name.ToLower() == name.ToLower()
            );

        if (duplicateExists)
            throw new InvalidOperationException(
                "A label with the same name already exists in this category.");

        var label = new Label
        {
            LabelId = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            Name = name,
            IsActive = true,
            CreatedBy = _currentUserService.UserId!.Value
        };

        await _labelRepository.CreateAsync(label);
        await _labelRepository.SaveChangesAsync();

        var createdLabel = await _labelRepository.Query()
            .Where(l => l.LabelId == label.LabelId)
            .Include(l => l.LabelCategory)
            .FirstAsync();

        return MapToResponse(createdLabel);
    }

    public async Task<LabelResponse?> UpdateLabel(Guid id, UpdateLabelRequest request)
    {
        var label = await _labelRepository.GetByIdAsync(id);
        if (label == null) return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();

            var duplicateExists = await _labelRepository.Query()
                .AnyAsync(l =>
                    l.LabelId != id &&
                    l.CategoryId == label.CategoryId &&
                    l.Name.ToLower() == name.ToLower()
                );

            if (duplicateExists)
                throw new InvalidOperationException(
                    "A label with the same name already exists in this category.");

            label.Name = name;
        }

        if (request.IsActive.HasValue)
        {
            label.IsActive = request.IsActive.Value;
        }

        await _labelRepository.UpdateAsync(label);
        await _labelRepository.SaveChangesAsync();

        var updatedLabel = await _labelRepository.Query()
            .Where(l => l.LabelId == id)
            .Include(l => l.LabelCategory)
            .FirstAsync();

        return MapToResponse(updatedLabel);
    }

    public async Task AddLabelToProject(Guid labelId, Guid projectId)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var currentUserRoles = _currentUserService.Roles;

        var existing = await _projectLabelRepository.GetByIdAsync(projectId, labelId);
        if (existing != null)
            throw new InvalidOperationException("Label already exists in this project");

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found");

        var label = await _labelRepository.GetByIdAsync(labelId);
        if (label == null)
            throw new KeyNotFoundException("Label not found");

        if (currentUserRoles.Contains("manager") &&
            project.CreatedBy != currentUserId)
        {
            throw new UnauthorizedAccessException(
                "Managers can only add labels to their own projects");
        }

        var projectLabel = new ProjectLabel
        {
            ProjectId = projectId,
            LabelId = labelId,
            AttachedBy = currentUserId
        };

        await _projectLabelRepository.CreateAsync(projectLabel);
        await _projectLabelRepository.SaveChangesAsync();

        await _activityLog.LogAsync(projectId, currentUserId, "LABEL_ATTACHED", "ProjectLabel", labelId, new { labelId });
    }

    public async Task RemoveLabelFromProject(Guid labelId, Guid projectId)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var currentUserRoles = _currentUserService.Roles;

        var projectLabel = await _projectLabelRepository.GetByIdAsync(projectId, labelId);

        if (projectLabel == null)
            throw new KeyNotFoundException("Project or label not found");

        if (currentUserRoles.Contains("manager") &&
            projectLabel.Project.CreatedBy != currentUserId)
            throw new UnauthorizedAccessException(
                "Managers can only remove labels from their own projects");

        await _projectLabelRepository.DeleteAsync(projectLabel);
        await _projectLabelRepository.SaveChangesAsync();

        await _activityLog.LogAsync(projectId, currentUserId, "LABEL_DETACHED", "ProjectLabel", labelId, new { labelId });
    }

    private LabelResponse MapToResponse(Label label)
    {
        return new LabelResponse
        {
            LabelId = label.LabelId,
            Name = label.Name,
            IsActive = label.IsActive,
            CreatedBy = label.CreatedBy,
            Category = new CategoryResponse
            {
                CategoryId = label.CategoryId,
                Name = label.LabelCategory.Name
            }
        };
    }
}
