using DataLabelProject.Application.DTOs.Categories;
using DataLabelProject.Business.Models;

namespace DataLabelProject.Data.Repositories.Abstractions;

public interface ICategoryRepository
{
    Task<(IEnumerable<Category> Items, int TotalCount)> GetAllAsync(CategoryQueryParameters @params);
    Task<Category?> GetByIdAsync(Guid id);
    Task<Category?> GetByNameAsync(string name);
    Task CreateAsync(Category category);
    Task UpdateAsync(Category category);
    Task SaveChangesAsync();
}
