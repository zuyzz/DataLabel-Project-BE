using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Datasets;

namespace DataLabelProject.Business.Services.DatasetItems
{
    public interface IDatasetItemService
    {
        Task<PagedResponse<DatasetItemResponse>> GetDataItems(DatasetItemQueryParameters @params);
        Task<PagedResponse<DatasetItemResponse>> GetDataItemsByDatasetId(Guid datasetId, DatasetItemQueryParameters @params);
        Task<DatasetItemResponse?> GetDataItemById(Guid id);
        Task CreateDataItems(Guid datasetId, CreateDatasetItemRequest request);
        Task DeleteDataItem(Guid id);
    }
}
