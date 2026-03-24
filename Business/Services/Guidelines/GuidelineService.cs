using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Guidelines;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Business.Services.ActivityLogs;
using DataLabelProject.Data.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Guidelines;

public class GuidelineService : IGuidelineService
{
    private readonly IGuidelineRepository _guidelineRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IActivityLogService _activityLog;

    public GuidelineService(
        IGuidelineRepository guidelineRepository,
        IProjectMemberRepository memberRepository,
        ICurrentUserService currentUserService,
        IActivityLogService activityLog)
    {
        _guidelineRepository = guidelineRepository;
        _memberRepository = memberRepository;
        _currentUserService = currentUserService;
        _activityLog = activityLog;
    }

    public async Task<PagedResponse<GuidelineResponse>> GetGuidelines(GuidelineQueryParameters @params)
    {
        var (items, totalCount) = await _guidelineRepository.GetAllAsync(@params);

        return new PagedResponse<GuidelineResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalItems = totalCount,
            Page = @params.Page,
            PageSize = @params.PageSize,
        };
    }

    public async Task<GuidelineResponse?> GetProjectGuideline(Guid projectId)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var member = await _memberRepository.GetByIdAsync(projectId, currentUserId);
        if (member == null) 
            throw new InvalidOperationException("Current user is not a member of this project");

        var guideline = await _guidelineRepository.GetByProjectIdAsync(projectId);
        if (guideline == null) return null;

        return MapToResponse(guideline);
    }

    public async Task<GuidelineResponse?> GetGuidelineById(Guid id)
    {
        var guideline = await _guidelineRepository.GetByIdAsync(id);
        if (guideline == null) return null;

        return MapToResponse(guideline);
    }

    public async Task<GuidelineResponse> CreateGuideline(CreateGuidelineRequest request)
    {
        var guideline = new Guideline
        {
            GuidelineId = Guid.NewGuid(),
            Content = request.Content,
            CreatedAt = DateTime.UtcNow,
            ProjectId = request.ProjectId
        };

        await _guidelineRepository.CreateAsync(guideline);
        await _guidelineRepository.SaveChangesAsync();

        return MapToResponse(guideline);
    }

    public async Task<GuidelineResponse?> UpdateGuideline(Guid id, UpdateGuidelineRequest request)
    {
        var guideline = await _guidelineRepository.GetByIdAsync(id);
        if (guideline == null) return null;

        if (!string.IsNullOrWhiteSpace(request.Content))
            guideline.Content = request.Content;

        await _guidelineRepository.UpdateAsync(guideline);
        await _guidelineRepository.SaveChangesAsync();

        await _activityLog.LogAsync(guideline.ProjectId, _currentUserService.UserId, "GUIDELINE_UPDATED", "Guideline", guideline.GuidelineId, null);

        return MapToResponse(guideline);
    }

    public async Task<bool> DeleteGuideline(Guid id)
    {
        var guideline = await _guidelineRepository.GetByIdAsync(id);
        if (guideline == null) return false;

        await _guidelineRepository.DeleteAsync(guideline);
        await _guidelineRepository.SaveChangesAsync();

        return true;
    }

    private GuidelineResponse MapToResponse(Guideline guideline)
    {
        return new GuidelineResponse
        {
            GuidelineId = guideline.GuidelineId,
            Content = guideline.Content,
            CreatedAt = guideline.CreatedAt
        };
    }
}
