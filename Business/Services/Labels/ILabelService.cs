using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Labels;

namespace DataLabelProject.Business.Services.Labels;

public interface ILabelService
{
    Task<PagedResponse<LabelResponse>> GetLabels(LabelQueryParameters @params);
    Task<PagedResponse<LabelResponse>> GetProjectLabels(Guid projectId, LabelQueryParameters @params);
    Task<PagedResponse<LabelResponse>> GetCategoryLabels(Guid categoryId, LabelQueryParameters @params);
    Task<LabelResponse?> GetLabelById(Guid id);
    Task<LabelResponse> CreateLabel(CreateLabelRequest request);
    Task<LabelResponse?> UpdateLabel(Guid id, UpdateLabelRequest request);
    Task DeactivateLabel(Guid id);
    Task AddLabelToProject(Guid labelId, Guid projectId);
    Task RemoveLabelFromProject(Guid labelId, Guid projectId);
}
