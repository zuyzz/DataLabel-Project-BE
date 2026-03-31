namespace DataLabelProject.Application.DTOs.Statistics;

public class ProjectOverviewDto
{
    public int TotalMembers { get; set; }
    public int TotalLabels { get; set; }
    public int TotalDatasets { get; set; }
    public int TotalDatasetItems { get; set; }
    public int CompletedTaskItems { get; set; }
    public int TotalTaskItems { get; set; }
    public float Progress { get; set; }
}
