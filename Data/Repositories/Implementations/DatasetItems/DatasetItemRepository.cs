using DataLabelProject.Business.Models;
using DataLabelProject.Data.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using DataLabelProject.Application.DTOs.Datasets;

namespace DataLabelProject.Data.Repositories.Implementations.DatasetItems;

public class DatasetItemRepository : IDatasetItemRepository
{
    private readonly AppDbContext _context;

    public DatasetItemRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IEnumerable<DatasetItem> Items, int TotalCount)> GetAllAsync(DatasetItemQueryParameters @params)
    {
        var query = _context.DatasetItems
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt);
        
        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(@params.Offset)
            .Take(@params.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(IEnumerable<DatasetItem> Items, int TotalCount)> GetAllByDatasetIdAsync(Guid datasetId, DatasetItemQueryParameters @params)
    {
        var query = _context.DatasetItems
            .AsNoTracking()
            .Where(i => i.DatasetId == datasetId)
            .OrderByDescending(i => i.CreatedAt);
        
        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(@params.Offset)
            .Take(@params.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<DatasetItem>> GetAllByDatasetIdAsync(Guid datasetId)
    {
        return await _context.DatasetItems
            .Where(i => i.DatasetId == datasetId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<DatasetItem?> GetByIdAsync(Guid id)
    {
        return await _context.DatasetItems
            .Include(i => i.ItemDataset)
            .FirstOrDefaultAsync(i => i.DatasetItemId == id);
    }

    public async Task CreateAsync(DatasetItem item)
    {
        await _context.DatasetItems.AddAsync(item);
    }

    public async Task CreateRangeAsync(IEnumerable<DatasetItem> items)
    {
        await _context.DatasetItems.AddRangeAsync(items);
    }

    public async Task DeleteAsync(DatasetItem item)
    {
        _context.DatasetItems.Remove(item);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
