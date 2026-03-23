using System.Text.Json;
using DataLabelProject.Application.DTOs.Annotations;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Consensus;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Models.Enums;
using DataLabelProject.Business.Services.Consensus;
using DataLabelProject.Business.Services.Shared;
using DataLabelProject.Data.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Annotations;

public class AnnotationService : IAnnotationService
{
    private const double DefaultIouThreshold = 0.7;

    private readonly IAnnotationRepository _annotationRepository;
    private readonly ILabelingTaskItemRepository _taskItemRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IProjectConfigRepository _projectConfigRepository;
    private readonly IConsensusRepository _consensusRepository;
    private readonly IClusteringService _clusteringService;
    private readonly IAgreementService _agreementService;

    public AnnotationService(
        IAnnotationRepository annotationRepository,
        ILabelingTaskItemRepository taskItemRepository,
        IAssignmentRepository assignmentRepository,
        IProjectConfigRepository projectConfigRepository,
        IConsensusRepository consensusRepository,
        IClusteringService clusteringService,
        IAgreementService agreementService)
    {
        _annotationRepository = annotationRepository;
        _taskItemRepository = taskItemRepository;
        _assignmentRepository = assignmentRepository;
        _projectConfigRepository = projectConfigRepository;
        _consensusRepository = consensusRepository;
        _clusteringService = clusteringService;
        _agreementService = agreementService;
    }

    // 2.1 Get annotations by task item
    public async Task<PagedResponse<AnnotationResponse>> GetAnnotationsByTaskItemAsync(
        Guid itemId, AnnotationQueryParameters parameters, Guid currentUserId, string currentUserRole)
    {
        var annotations = (await _annotationRepository.GetByTaskItemIdAsync(itemId)).ToList();

        // Annotators can only see their own annotations
        if (currentUserRole == "annotator")
            annotations = annotations.Where(a => a.AnnotatorId == currentUserId).ToList();

        if (parameters.Status.HasValue)
            annotations = annotations.Where(a => a.Status == parameters.Status.Value).ToList();

        var totalItems = annotations.Count;
        var paged = annotations
            .Skip(parameters.Offset)
            .Take(parameters.PageSize)
            .Select(MapToResponse)
            .ToList();

        return new PagedResponse<AnnotationResponse>
        {
            Items = paged,
            TotalItems = totalItems,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
        };
    }

    // 2.2 Get annotations of a task
    public async Task<PagedResponse<TaskItemAnnotationsResponse>> GetAnnotationsByTaskAsync(
        Guid taskId, AnnotationQueryParameters parameters, Guid currentUserId, string currentUserRole)
    {
        var taskItems = await _taskItemRepository.GetByTaskIdAsync(taskId);

        var totalItems = taskItems.Count;
        var paged = taskItems
            .Skip(parameters.Offset)
            .Take(parameters.PageSize)
            .ToList();

        var result = paged.Select(ti =>
        {
            var annotations = ti.Annotations?.AsEnumerable() ?? Enumerable.Empty<Annotation>();

            // Annotators can only see their own annotations
            if (currentUserRole == "annotator")
                annotations = annotations.Where(a => a.AnnotatorId == currentUserId);

            return new TaskItemAnnotationsResponse
            {
                TaskItemId = ti.TaskItemId,
                DatasetItemId = ti.DatasetItemId,
                Status = ti.Status.ToString(),
                RevisionCount = ti.RevisionCount,
                Annotations = annotations.Select(MapToResponse).ToList()
            };
        }).ToList();

        return new PagedResponse<TaskItemAnnotationsResponse>
        {
            Items = result,
            TotalItems = totalItems,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
        };
    }

    // 2.3 Submit Annotation
    public async Task<List<AnnotationResponse>> SubmitAnnotationsAsync(List<SubmitAnnotationRequest> requests, Guid currentUserId)
    {
        var duplicateTaskItemIds = requests
            .GroupBy(request => request.TaskItemId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateTaskItemIds.Count > 0)
            throw new InvalidOperationException("Each task item can only be submitted once per request");

        var responses = new List<AnnotationResponse>(requests.Count);

        foreach (var request in requests)
        {
            var taskItem = await _taskItemRepository.GetByIdAsync(request.TaskItemId)
                ?? throw new KeyNotFoundException("Task item not found");

            ValidateTaskItemStatus(taskItem);

            var existing = await _annotationRepository.GetByTaskItemIdAndAnnotatorIdAsync(taskItem.TaskItemId, currentUserId);
            if (existing != null)
                throw new InvalidOperationException("Annotation already exists for this task item");

            var annotation = new Annotation
            {
                AnnotationId = Guid.NewGuid(),
                TaskItemId = taskItem.TaskItemId,
                AnnotatorId = currentUserId,
                Payload = JsonSerializer.Serialize(request.Payload),
                Status = AnnotationStatus.Submitted,
                SubmittedAt = DateTime.UtcNow
            };

            await _annotationRepository.AddAsync(annotation);
            await _annotationRepository.SaveChangesAsync();

            await CheckConsensusAsync(taskItem);
            await _annotationRepository.SaveChangesAsync();

            responses.Add(MapToResponse(annotation));
        }

        return responses;
    }

    // 4. Update Annotation (Conflict Resolution)
    public async Task<AnnotationResponse> UpdateAnnotationAsync(Guid annotationId, UpdateAnnotationRequest request, Guid currentUserId)
    {
        var annotation = await _annotationRepository.GetByIdAsync(annotationId)
            ?? throw new KeyNotFoundException("Annotation not found");

        if (annotation.AnnotatorId != currentUserId)
            throw new UnauthorizedAccessException("You can only update your own annotations");

        if (annotation.Status != AnnotationStatus.Conflicted)
            throw new InvalidOperationException("Only conflicted annotations can be updated");

        annotation.Payload = JsonSerializer.Serialize(request.Payload);
        annotation.Status = AnnotationStatus.Submitted;
        annotation.SubmittedAt = DateTime.UtcNow;

        await _annotationRepository.UpdateAsync(annotation);
        await _annotationRepository.SaveChangesAsync();

        var taskItem = annotation.AnnotationTaskItem;
        System.Console.WriteLine("startcheck consensus");
        await CheckConsensusAsync(taskItem);
        await _annotationRepository.SaveChangesAsync();

        return MapToResponse(annotation);
    }

    // 5. Skip Annotation
    public async Task<AnnotationResponse> SkipAnnotationAsync(SkipAnnotationRequest request, Guid currentUserId)
    {
        var taskItem = await _taskItemRepository.GetByIdAsync(request.TaskItemId)
            ?? throw new KeyNotFoundException("Task item not found");

        ValidateTaskItemStatus(taskItem);

        var annotation = new Annotation
        {
            AnnotationId = Guid.NewGuid(),
            TaskItemId = taskItem.TaskItemId,
            AnnotatorId = currentUserId,
            Status = AnnotationStatus.Skipped,
            Payload = null,
            Note = request.Note,
            SubmittedAt = DateTime.UtcNow
        };

        await _annotationRepository.AddAsync(annotation);
        await _annotationRepository.SaveChangesAsync();

        return MapToResponse(annotation);
    }

    // Validate task item status
    private static void ValidateTaskItemStatus(LabelingTaskItem taskItem)
    {
        if (taskItem.Status == LabelingTaskItemStatus.Unassigned)
            throw new InvalidOperationException("Task item is not assigned");

        if (taskItem.Status == LabelingTaskItemStatus.Locked)
            throw new InvalidOperationException("Task item is locked");
    }

    // 3. Consensus check after annotation submission
    private async Task CheckConsensusAsync(LabelingTaskItem taskItem)
    {
        var annotations = (await _annotationRepository.GetByTaskItemIdAsync(taskItem.TaskItemId))
            .Where(a => a.Status == AnnotationStatus.Submitted)
            .ToList();

        if (!taskItem.TaskId.HasValue)
            return;

        var assignments = await _assignmentRepository.Query()
            .Where(a => a.TaskId == taskItem.TaskId && a.AssignmentUser.UserRole.RoleName == "annotator")
            .Include(a => a.AssignmentUser)
                .ThenInclude(a => a.UserRole)
            .ToListAsync();
        var annotatorAssignmentCount = assignments.Count;

        if (annotations.Count < annotatorAssignmentCount)
        {
            System.Console.WriteLine($"Consensus cannot be calculated: {annotations.Count}/{annotatorAssignmentCount} annotations");
            return;
        }

        var boxes = BoxConversionHelper.FlattenBoxes(annotations);
        if (boxes.Count == 0)
            throw new InvalidOperationException("No bounding boxes found in submitted annotations");

        var distinctAnnotators = annotations.Select(a => a.AnnotatorId).Distinct().Count();
        var clusters = _clusteringService.ClusterByIoU(boxes, DefaultIouThreshold);
        var consensusBboxes = _agreementService.BuildConsensusBboxes(clusters);

        if (consensusBboxes == null)
        {
            System.Console.WriteLine("Consensus conflict detected");
            foreach (var a in annotations)
                a.Status = AnnotationStatus.Conflicted;

            await _annotationRepository.UpdateRangeAsync(annotations);
            await _annotationRepository.SaveChangesAsync();

            taskItem.RevisionCount++;
            if (taskItem.RevisionCount >= 3)
                taskItem.Status = LabelingTaskItemStatus.Locked;

            return;
        }

        var agreement = _agreementService.CalculateOverallScore(clusters, distinctAnnotators);
        System.Console.WriteLine($"Agreement: {agreement}");

        foreach (var a in annotations)
            a.Status = AnnotationStatus.Resolved;

        await _annotationRepository.UpdateRangeAsync(annotations);
        await _annotationRepository.SaveChangesAsync();

        await _consensusRepository.CreateAsync(new Models.Consensus
        {
            ConsensusId = Guid.NewGuid(),
            DatasetItemId = taskItem.DatasetItemId,
            Payload = BuildConsensusPayload(consensusBboxes, agreement),
            CreatedAt = DateTime.UtcNow
        });
    }

    // 6. Build consensus payload with calculated agreement score.
    private static string BuildConsensusPayload(List<ConsensusBboxDto> consensusBboxes, double agreementScore)
    {
        return JsonSerializer.Serialize(new
        {
            bboxes = consensusBboxes,
            agreementScore
        });
    }

    // 2.0 Get annotation by ID
    public async Task<AnnotationResponse?> GetAnnotationByIdAsync(Guid annotationId, Guid currentUserId, string currentUserRole)
    {
        var annotation = await _annotationRepository.GetByIdAsync(annotationId);
        if (annotation == null)
            return null;

        // Annotators can only see their own annotations
        if (currentUserRole == "annotator" && annotation.AnnotatorId != currentUserId)
            throw new UnauthorizedAccessException("You do not have permission to view this annotation");

        return MapToResponse(annotation);
    }

    private static AnnotationResponse MapToResponse(Annotation annotation)
    {
        object? parsedPayload = null;
        if (!string.IsNullOrWhiteSpace(annotation.Payload))
        {
            try { parsedPayload = JsonSerializer.Deserialize<AnnotationPayload>(annotation.Payload); }
            catch { parsedPayload = annotation.Payload; }
        }

        return new AnnotationResponse
        {
            AnnotationId = annotation.AnnotationId,
            TaskItemId = annotation.TaskItemId,
            AnnotatorId = annotation.AnnotatorId,
            Payload = parsedPayload,
            Status = annotation.Status,
            Note = annotation.Note,
            SubmittedAt = annotation.SubmittedAt
        };
    }
}
