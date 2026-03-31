using DataLabelProject.Application.DTOs.Datasets;
using DataLabelProject.Business.Services.Datasets;
using DataLabelProject.Business.Services.DatasetItems;
using DataLabelProject.Business.Services.Projects;
using DataLabelProject.Business.Services.FileUpload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataLabelProject.Application.Controllers;

[ApiController]
[Route("api/datasets")]
public class DatasetsController : ControllerBase
{
    private readonly IDatasetService _datasetService;
    private readonly IDatasetItemService _itemService;
    private readonly IEnumerable<IFileUploadStrategy> _fileUploadstrategies;

    public DatasetsController(
        IDatasetService datasetService, 
        IDatasetItemService itemService,
        IEnumerable<IFileUploadStrategy> fileUploadstrategies)
    {
        _datasetService = datasetService;
        _itemService = itemService;
        _fileUploadstrategies = fileUploadstrategies;
    }

    /// <summary>
    /// Create a new dataset. File upload is optional.
    /// Accepts image files or archive files (zip/rar) containing only image files.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> CreateDataset([FromBody] CreateDatasetRequest request)
    {
        var result = await _datasetService.CreateDataset(request);
        return Ok(result);
    }

    /// <summary>
    /// Get dataset by ID with item count and metadata.
    /// </summary>
    [HttpGet("{datasetId:guid}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetDataset([FromRoute] Guid datasetId)
    {
        var result = await _datasetService.GetDatasetById(datasetId);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetDatasets([FromQuery] DatasetQueryParameters @params)
    {
        var result = await _datasetService.GetDatasets(@params);
        return Ok(result);
    }

    /// <summary>
    /// Get all dataset items for a specific dataset.
    /// </summary>
    [HttpGet("{datasetId:guid}/items")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetDatasetItemsForDataset([FromRoute] Guid datasetId, [FromQuery] DatasetItemQueryParameters @params)
    {
        var result = await _itemService.GetDataItemsByDatasetId(datasetId, @params);
        return Ok(result);
    }

    /// <summary>
    /// Get all dataset items for a specific dataset.
    /// </summary>
    [HttpGet("items")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetDatasetItems([FromQuery] DatasetItemQueryParameters @params)
    {
        var result = await _itemService.GetDataItems(@params);
        return Ok(result);
    }

    /// <summary>
    /// Delete a dataset item by ID.
    /// </summary>
    [HttpDelete("items/{itemId:guid}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> DeleteDatasetItem([FromRoute] Guid itemId)
    {
        // Service will validate item belongs to dataset and remove storage
        await _itemService.DeleteDataItem(itemId);
        return NoContent();
    }

    /// <summary>
    /// Update an existing dataset. Can update name and description.
    /// </summary>
    [HttpPut("{datasetId:guid}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> UpdateDataset([FromRoute] Guid datasetId, [FromBody] UpdateDatasetRequest request)
    {
        var result = await _datasetService.UpdateDataset(datasetId, request);
        return Ok(result);
    }

    /// <summary>
    /// Delete a dataset and all its associated data.
    /// </summary>
    [HttpDelete("{datasetId:guid}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> DeleteDataset([FromRoute] Guid datasetId)
    {
        await _datasetService.DeleteDataset(datasetId);
        return NoContent();
    }

    /// <summary>
    /// Upload a file or archive into an existing dataset.
    /// Accepts single image/audio/text or archive (zip/rar) containing allowed files.
    /// Uploads rely on strategy detection; dataset mediaType field no longer exists.
    /// </summary>
    [HttpPost("{datasetId:guid}/items")]
    [Authorize(Roles = "admin,manager")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDatasetItems([FromRoute] Guid datasetId, [FromForm] CreateDatasetItemRequest request)
    {
        await _itemService.CreateDataItems(datasetId, request);
        return Ok();
    }

    /// <summary>
    /// Attach a dataset to a project.
    /// Only the creator of both the dataset and project can perform this operation.
    /// </summary>
    [HttpPost("add/{projectId:guid}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> AddDataset(
        [FromRoute] Guid projectId, 
        [FromBody] AddDatasetRequest request)
    {
        await _datasetService.AddDatasetToProject(request.DatasetId, projectId);
        return NoContent();
    }

    /// <summary>
    /// Detach a dataset to a project.
    /// Only the creator of both the dataset and project can perform this operation.
    /// </summary>
    [HttpPost("remove/{projectId:guid}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> RemoveDataset(
        [FromRoute] Guid projectId, 
        [FromBody] RemoveDatasetRequest request)
    {
        await _datasetService.RemoveDatasetFromProject(request.DatasetId, projectId);
        return NoContent();
    }
}
