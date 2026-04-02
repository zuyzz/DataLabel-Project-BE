using DataLabelProject.Application.DTOs.Categories;
using DataLabelProject.Application.DTOs.Common;

namespace DataLabelProject.Business.Services.Categories
{
    public interface ICategoryService
    {
        Task<PagedResponse<CategoryResponse>> GetCategories(CategoryQueryParameters @params);
        Task<CategoryResponse?> GetCategoryById(Guid id);
        Task<CategoryResponse> CreateCategory(CreateCategoryRequest request);
        Task<CategoryResponse?> UpdateCategory(Guid id, UpdateCategoryRequest request);
        Task DeactivateCategory(Guid id);
    }
}
