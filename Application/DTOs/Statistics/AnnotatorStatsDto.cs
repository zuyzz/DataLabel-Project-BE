namespace DataLabelProject.Application.DTOs.Statistics;

public class AnnotatorStatsDto
{
    public int IncompletedItems { get; set; }
    public int SubmittedItems { get; set; }
    public int ConflictedItems { get; set; }
    public int ResolvedItems { get; set; }
    public int TotalItems { get; set; }
    public int TodayAnnotationCount { get; set; }
}
