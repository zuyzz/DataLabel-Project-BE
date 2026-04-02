using DataLabelProject.Application.DTOs.Datasets;
using DataLabelProject.Business.Services.ActivityLogs.Constant;
using DataLabelProject.Business.Models;
using DataLabelProject.Data.Repositories.Abstractions;
using DataLabelProject.Business.Services.FileUpload;
using DataLabelProject.Business.Services.Storage;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Business.Services.ActivityLogs;
using DataLabelProject.Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using DataLabelProject.Shared.Extensions;
using System.Text.Json;

namespace DataLabelProject.Business.Services.Datasets;

public class DatasetService : IDatasetService
{
    private readonly IDatasetRepository _datasetRepository;
    private readonly IDatasetItemRepository _datasetItemRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly ILabelingTaskItemRepository _taskItemRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorage _fileStorage;
    private readonly IActivityLogService _activityLog;

    public DatasetService(
        IDatasetRepository datasetRepository,
        IDatasetItemRepository datasetItemRepository,
        IProjectRepository projectRepository,
        IProjectMemberRepository memberRepository,
        ILabelingTaskItemRepository taskItemRepository,
        ICurrentUserService currentUserService,
        IFileStorage fileStorage,
        IActivityLogService activityLog)
    {
        _datasetRepository = datasetRepository;
        _datasetItemRepository = datasetItemRepository;
        _projectRepository = projectRepository;
        _memberRepository = memberRepository;
        _taskItemRepository = taskItemRepository;
        _currentUserService = currentUserService;
        _fileStorage = fileStorage;
        _activityLog = activityLog;
    }

    public async Task<DatasetResponse> CreateDataset(CreateDatasetRequest request)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var datasetId = Guid.NewGuid();
        var datasetName = request.Name.Trim();

        var exists = await _datasetRepository.GetByNameAndCreatorAsync(datasetName, currentUserId);
        if (exists != null)
            throw new InvalidOperationException("You already have a dataset with the same name");

        var dataset = new Dataset
        {
            DatasetId = datasetId,
            Name = datasetName,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };

        await _datasetRepository.CreateAsync(dataset);
        await _datasetRepository.SaveChangesAsync();

        // Log dataset creation
        await _activityLog.LogAsync(null, currentUserId, ActivityEvents.DatasetCreated, ActivityTargets.Dataset, dataset.DatasetId, new DatasetCreatedDetails { DatasetName = dataset.Name });

        return MapToResponse(dataset);
    }

    public async Task<DatasetResponse> UpdateDataset(Guid id, UpdateDatasetRequest request)
    {
        var dataset = await _datasetRepository.GetByIdAsync(id);
        if (dataset == null)
            throw new KeyNotFoundException($"Dataset with ID {id} not found");

        var currentUserId = _currentUserService.UserId!.Value;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var newName = request.Name!.Trim();
            var existing = await _datasetRepository.GetByNameAndCreatorAsync(newName, currentUserId);
            if (existing != null && existing.DatasetId != id)
                throw new InvalidOperationException("You already have a dataset with the same name");

            dataset.Name = newName;
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
            dataset.Description = request.Description;

        await _datasetRepository.UpdateAsync(dataset);
        await _datasetRepository.SaveChangesAsync();

        return MapToResponse(dataset);
    }

    public async Task DeleteDataset(Guid id)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var currentUserRole = _currentUserService.Roles;

        var dataset = await _datasetRepository.GetByIdAsync(id);
        if (dataset == null)
            throw new KeyNotFoundException($"Dataset with ID {id} not found");
        if (dataset.CreatedBy != currentUserId && currentUserRole.Contains("manager"))
            throw new UnauthorizedAccessException("Managers can only delete their own datasets");
        if (dataset.DatasetProject != null)
            throw new InvalidOperationException("Cannot delete dataset that is associated with a project. Please remove the dataset from that project first.");

        var storagePrefix = $"datasets/{id}/";
        await _fileStorage.DeleteFolderAsync(storagePrefix);

        await _datasetRepository.DeleteAsync(dataset);
        await _datasetRepository.SaveChangesAsync();
    }

    public async Task<DatasetResponse?> GetDatasetById(Guid id)
    {
        var dataset = await _datasetRepository.GetByIdAsync(id);
        if (dataset == null) return null;

        return MapToResponse(dataset);
    }

    public async Task<PagedResponse<DatasetResponse>> GetDatasets(
        DatasetQueryParameters @params)
    {
        IQueryable<Dataset> query = _datasetRepository.Query()
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Include(d => d.DatasetItems);

        query = ApplyUserFilter(query);
        query = ApplyParamFilters(query, @params);

        return await query.ToPagedResponseAsync(@params, MapToResponse);
    }

    public async Task<PagedResponse<DatasetResponse>> GetProjectDatasets(
        Guid projectId, 
        DatasetQueryParameters @params)
    {
        IQueryable<Dataset> query = _datasetRepository.Query()
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.CreatedAt)
            .Include(d => d.DatasetItems);

        query = ApplyMemberFilter(query);
        query = ApplyParamFilters(query, @params);

        return await query.ToPagedResponseAsync(@params, MapToResponse);
    }

    private IQueryable<Dataset> ApplyMemberFilter(
        IQueryable<Dataset> query)
    {
        var currentUserId = _currentUserService.UserId;
        var currentUserRoles = _currentUserService.Roles;

        if (!currentUserRoles.Contains("admin") && currentUserId.HasValue)
        {
            query = query.Where(d =>
                d.DatasetProject != null &&
                d.DatasetProject.ProjectMembers.Any(pm =>
                    pm.MemberId == currentUserId.Value));
        }

        return query;
    }

    private IQueryable<Dataset> ApplyUserFilter(
        IQueryable<Dataset> query)
    {
        var currentUserId = _currentUserService.UserId;
        var currentUserRoles = _currentUserService.Roles;

        if (currentUserRoles.Contains("manager") && currentUserId.HasValue)
        {
            query = query.Where(d => d.CreatedBy == currentUserId);
        }

        return query;
    }

    private IQueryable<Dataset> ApplyParamFilters(
        IQueryable<Dataset> query,
        DatasetQueryParameters @params)
    {
        if (!string.IsNullOrWhiteSpace(@params.Name))
        {
            query = query.Where(d => EF.Functions.ILike(d.Name, $"%{@params.Name.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(@params.Description))
        {
            query = query.Where(d => EF.Functions.ILike(d.Description ?? "", $"%{@params.Description.Trim()}%"));
        }

        if (@params.IsActive.HasValue)
        {
            query = query.Where(d => d.IsActive == @params.IsActive);
        }

        return query;
    }

    public async Task AddDatasetToProject(Guid datasetId, Guid projectId)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var currentUserRole = _currentUserService.Roles;

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found");

        var dataset = await _datasetRepository.GetByIdAsync(datasetId);
        if (dataset == null)
            throw new KeyNotFoundException("Dataset not found");

        if (dataset.DatasetProject != null) 
            throw new InvalidOperationException("Dataset is already associated with a project. Please remove the dataset from that project first.");

        if (currentUserRole.Contains("manager") &&
            (project.CreatedBy != currentUserId || dataset.CreatedBy != currentUserId))
        {
            throw new UnauthorizedAccessException(
                "Managers can only add their datasets to their own projects");
        }

        dataset.ProjectId = projectId;

        var items = await _datasetItemRepository.GetAllByDatasetIdAsync(datasetId);
        var taskItems = items.Select(i => new LabelingTaskItem
        {
            TaskItemId = Guid.NewGuid(),
            TaskId = null,
            ProjectId = projectId,
            DatasetItemId = i.DatasetItemId,
            Status = Models.Enums.LabelingTaskItemStatus.Unassigned
        });

        await _taskItemRepository.AddRangeAsync(taskItems);
        
        await _datasetRepository.SaveChangesAsync();
        await _taskItemRepository.SaveChangesAsync();

        await _activityLog.LogAsync(projectId, currentUserId, ActivityEvents.DatasetAttached, ActivityTargets.Dataset, datasetId, new DatasetAttachedDetails
        {
            DatasetId = datasetId,
            DatasetName = dataset?.Name ?? "Unknown",
            ProjectId = projectId,
            ProjectName = project?.Name ?? "Unknown"
        });
    }

    public async Task RemoveDatasetFromProject(Guid datasetId, Guid projectId)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var currentUserRoles = _currentUserService.Roles;

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found");

        var dataset = await _datasetRepository.GetByIdAsync(datasetId);
        if (dataset == null)
            throw new KeyNotFoundException("Dataset not found");

        if (dataset.DatasetProject == null)
            throw new InvalidOperationException("This dataset doesnt associated with any project");

        if (dataset.DatasetProject != project)
            throw new InvalidOperationException("This dataset is associated with other project");

        if (currentUserRoles.Contains("manager") &&
            (project.CreatedBy != currentUserId ||
            dataset.CreatedBy != currentUserId))
        {
            throw new UnauthorizedAccessException(
                "Managers can only remove their datasets from their own projects");
        }

        dataset.ProjectId = null;

        await _datasetRepository.SaveChangesAsync();

        await _activityLog.LogAsync(projectId, currentUserId, ActivityEvents.DatasetDetached, ActivityTargets.Dataset, datasetId, new DatasetDetachedDetails
        {
            DatasetId = datasetId,
            DatasetName = dataset.Name,
            ProjectId = projectId,
            ProjectName = project.Name
        });
    }

    private DatasetResponse MapToResponse(Dataset dataset)
    {
        long totalSize = dataset.DatasetItems?
            .Sum(i =>
            {
                if (string.IsNullOrEmpty(i.Metadata)) return 0;

                try
                {
                    var meta = JsonSerializer.Deserialize<ImageMetadata>(i.Metadata);
                    return meta?.FileSize ?? 0;
                }
                catch
                {
                    return 0;
                }
            }) ?? 0;

        return new DatasetResponse
        {
            DatasetId = dataset.DatasetId,
            Name = dataset.Name,
            Description = dataset.Description,
            CreatedAt = dataset.CreatedAt,
            CreatedBy = dataset.CreatedBy,
            AttachedTo = dataset.ProjectId,
            IsActive = dataset.IsActive,
            SampleCount = dataset.DatasetItems?.Count ?? 0,
            TotalSize = totalSize
        };
    }
}
