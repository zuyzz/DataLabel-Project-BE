namespace DataLabelProject.Application.DTOs.Statistics;

public class AnnotatorStatsDto
{
    public int IncompletedItems { get; set; }
    public int ExpiredItems { get; set; }
    public int SubmittedItems { get; set; }
    public int CompletedItems { get; set; }
    public int SkippedItems { get; set; }
    public int TotalItems { get; set; }
    public int TodayAnnotationCount { get; set; }
}
