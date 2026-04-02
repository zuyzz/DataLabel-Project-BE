using DataLabelProject.Application.DTOs.Datasets;

namespace DataLabelProject.Application.DTOs.Tasks;

public class TaskItemDetailResponse
{
    public Guid TaskItemId { get; set; }
    public Guid ProjectId { get; set; }
    public DatasetItemResponse DatasetItem { get; set; } = null!;
    public string Status { get; set; } = string.Empty;
}
