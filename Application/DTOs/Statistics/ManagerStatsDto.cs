namespace DataLabelProject.Application.DTOs.Statistics;

public class WeeklyPerformanceDto
{
    public DateOnly Date { get; set; }
    public int Annotations { get; set; }
    public int AnnotationRate { get; set; }
    public int Reviews { get; set; }
    public int ReviewRate { get; set; }
}

public class ManagerStatsDto
{
    public int ActiveProjects { get; set; }
    public int IncompletedProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int TotalProjects { get; set; }
    public int WeeklyAnnotations { get; set; }
    public int WeeklyReviews { get; set; }
    public int TodayAnnotations { get; set; }
    public int TodayReviews { get; set; }
    public List<WeeklyPerformanceDto> WeeklyPerformance { get; set; } = new();
}
