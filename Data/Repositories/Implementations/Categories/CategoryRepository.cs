using DataLabelProject.Business.Models;
using DataLabelProject.Data.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using DataLabelProject.Application.DTOs.Categories;

namespace DataLabelProject.Data.Repositories.Implementations.Categories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;

    public CategoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IEnumerable<Category> Items, int TotalCount)> GetAllAsync(CategoryQueryParameters @params)
    {
        var query = _context.Categories
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .AsQueryable();

        if (@params.IsActive.HasValue)
            query = query.Where(c => c.IsActive == @params.IsActive.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(@params.Offset)
            .Take(@params.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Category?> GetByIdAsync(Guid id)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == id);
    }

    public async Task<Category?> GetByNameAsync(string name)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Name, $"%{name.Trim()}%"));
    }

    public async Task CreateAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
    }

    public async Task UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
