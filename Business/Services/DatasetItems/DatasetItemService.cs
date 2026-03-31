using System.Text.Json;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Datasets;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Services.FileUpload;
using DataLabelProject.Business.Services.Storage;
using DataLabelProject.Data.Repositories.Abstractions;

namespace DataLabelProject.Business.Services.DatasetItems
{
    public class DatasetItemService : IDatasetItemService
    {
        private readonly IDatasetItemRepository _datasetItemRepository;
        private readonly IDatasetRepository _datasetRepository;
        private readonly ILabelingTaskItemRepository _taskItemRepository;
        private readonly IFileStorage _fileStorage;
        private readonly IEnumerable<IFileUploadStrategy> _uploadStrategies;

        public DatasetItemService(
            IDatasetItemRepository datasetItemRepository, 
            IDatasetRepository datasetRepository,
            ILabelingTaskItemRepository taskItemRepository,
            IFileStorage fileStorage,
            IEnumerable<IFileUploadStrategy> uploadStrategies)
        {
            _datasetItemRepository = datasetItemRepository;
            _datasetRepository = datasetRepository;
            _taskItemRepository = taskItemRepository;
            _fileStorage = fileStorage;
            _uploadStrategies = uploadStrategies;
        }

        public async Task<PagedResponse<DatasetItemResponse>> GetDataItems(DatasetItemQueryParameters @params)
        {
            var (items, totalCount) = await _datasetItemRepository.GetAllAsync(@params);

            return new PagedResponse<DatasetItemResponse> 
            {
                Items = items.Select(MapToResponse).ToList(),
                TotalItems = totalCount,
                Page = @params.Page,
                PageSize = @params.PageSize
            };
        }

        public async Task<PagedResponse<DatasetItemResponse>> GetDataItemsByDatasetId(Guid datasetId, DatasetItemQueryParameters @params)
        {
            var (items, totalCount) = await _datasetItemRepository.GetAllByDatasetIdAsync(datasetId, @params);

            return new PagedResponse<DatasetItemResponse> 
            {
                Items = items.Select(MapToResponse).ToList(),
                TotalItems = totalCount,
                Page = @params.Page,
                PageSize = @params.PageSize
            };
        }

        public async Task<DatasetItemResponse?> GetDataItemById(Guid id)
        {
            var item = await _datasetItemRepository.GetByIdAsync(id);
            if (item == null) return null;

            return MapToResponse(item);
        }

        public async Task CreateDataItems(Guid datasetId, CreateDatasetItemRequest request)
        {
            var dataset = await _datasetRepository.GetByIdAsync(datasetId);
            if (dataset == null) 
                throw new InvalidOperationException("Dataset not found");
            if (!dataset.IsActive)
                throw new InvalidOperationException("Cannot add sample to inactive dataset");

            var storageKey = $"datasets/{datasetId}";

            var fileUpload = _uploadStrategies.FirstOrDefault(s => s.CanHandle(request.File))
                ?? throw new InvalidOperationException("No strategy found");

            var uploaded = await fileUpload.ProcessAsync(request.File, storageKey);

            var hashes = uploaded.Select(u => u.Hash);
            var existingHashes = await _datasetItemRepository.GetExistingHashes(datasetId, hashes);

            var finalUploads = uploaded
                .Where(u => !existingHashes.Contains(u.Hash))
                .ToList();

            if (finalUploads.Count == 0) return;

            var items = finalUploads.Select(u => new DatasetItem
            {
                DatasetItemId = u.FileId,
                DatasetId = datasetId,
                MediaType = u.ContentType,
                StorageUri = u.StorageUri,
                Metadata = u.Metadata,
                ContentHash = u.Hash,
                CreatedAt = DateTime.UtcNow
            });

            await _datasetItemRepository.CreateRangeAsync(items);

            if (dataset.ProjectId.HasValue)
            {
                var taskItems = items.Select(i => new LabelingTaskItem
                {
                    TaskItemId = Guid.NewGuid(),
                    TaskId = null,
                    ProjectId = dataset.ProjectId.Value,
                    DatasetItemId = i.DatasetItemId,
                    RevisionCount = 0,
                    Status = Models.Enums.LabelingTaskItemStatus.Unassigned
                });

                await _taskItemRepository.AddRangeAsync(taskItems);
            }

            await _datasetItemRepository.SaveChangesAsync();
            await _taskItemRepository.SaveChangesAsync();
        }

        public async Task DeleteDataItem(Guid id)
        {
            var item = await _datasetItemRepository.GetByIdAsync(id);
            if (item == null)
                throw new InvalidOperationException("Dataset item not found");
            if (!item.ItemDataset.IsActive)
                throw new InvalidOperationException("Cannot remove sample to inactive dataset");

            // delete storage object if exists
            if (!string.IsNullOrWhiteSpace(item.StorageUri))
            {
                try
                {
                    await _fileStorage.DeleteFileAsync(item.StorageUri);
                }
                catch
                {
                    throw new InvalidOperationException(
                        $"Failed to delete storage file: {item.StorageUri}");
                }
            }

            await _datasetItemRepository.DeleteAsync(item);
            await _datasetItemRepository.SaveChangesAsync();
        }

        private DatasetItemResponse MapToResponse(DatasetItem item)
        {
            ImageMetadata? metadata = null;

            if (!string.IsNullOrWhiteSpace(item.Metadata))
            {
                try
                {
                    metadata = JsonSerializer.Deserialize<ImageMetadata>(item.Metadata);
                }
                catch (JsonException)
                {
                    // log invalid metadata if needed
                }
            }

            return new DatasetItemResponse 
            {
                ItemId = item.DatasetItemId,
                DatasetId = item.DatasetId,
                MediaType = item.MediaType,
                StorageUri = item.StorageUri,
                Metadata = metadata,
                CreatedAt = item.CreatedAt
            };
        }
    }
}
