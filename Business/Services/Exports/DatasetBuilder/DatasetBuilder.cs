using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataLabelProject.Data;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Exports.DatasetBuilder;

public class DatasetBuilder : IDatasetBuilder
{
    private readonly AppDbContext _context;

    public DatasetBuilder(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ExportDataset> BuildFromConsensus(Guid projectId)
    {
        // Get all dataset items belonging to the project
        var datasetItemIds = await _context.DatasetItems
            .AsNoTracking()
            .Where(di => di.ItemDataset != null && di.ItemDataset.ProjectId == projectId)
            .Select(di => di.DatasetItemId)
            .ToListAsync();

        // Get the latest consensus per dataset item
        var consensuses = await _context.Consensuses
            .AsNoTracking()
            .Where(c => datasetItemIds.Contains(c.DatasetItemId))
            .ToListAsync();

        var consensusByDatasetItemId = consensuses
            .GroupBy(c => c.DatasetItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAt).First());

        // Load dataset items for the consensus entries
        var datasetItems = await _context.DatasetItems
            .AsNoTracking()
            .Where(di => consensusByDatasetItemId.Keys.Contains(di.DatasetItemId))
            .ToDictionaryAsync(di => di.DatasetItemId);

        var dataset = new ExportDataset();
        var categoriesMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int imageIdCounter = 1;
        int annotationIdCounter = 1;

        foreach (var kvp in consensusByDatasetItemId)
        {
            var datasetItemId = kvp.Key;
            var consensus = kvp.Value;

            if (!datasetItems.TryGetValue(datasetItemId, out var datasetItem))
                continue;

            // Parse consensus payload
            var objects = ExportHelpers.ParseConsensusPayload(consensus.Payload);
            if (objects == null || objects.Count == 0)
                continue;

            var imageId = imageIdCounter++;

            // Parse metadata for image dimensions
            var (width, height) = ExportHelpers.ParseImageDimensions(datasetItem.Metadata);

            var fileName = ExportHelpers.ExtractFileName(datasetItem.StorageUri);

            dataset.Images.Add(new ExportImage
            {
                Id = imageId,
                FileName = fileName,
                Width = width,
                Height = height,
                StorageUri = datasetItem.StorageUri
            });

            foreach (var obj in objects)
            {
                // Get or create category
                if (!categoriesMap.TryGetValue(obj.Label, out var categoryId))
                {
                    categoryId = categoriesMap.Count + 1;
                    categoriesMap[obj.Label] = categoryId;
                    dataset.Categories.Add(new ExportCategory
                    {
                        Id = categoryId,
                        Name = obj.Label
                    });
                }

                dataset.Annotations.Add(new ExportAnnotation
                {
                    Id = annotationIdCounter++,
                    ImageId = imageId,
                    CategoryId = categoryId,
                    Bbox = new double[] { obj.X, obj.Y, obj.W, obj.H }
                });
            }
        }

        return dataset;
    }
}