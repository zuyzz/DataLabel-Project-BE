namespace DataLabelProject.Application.DTOs.Statistics;

public class WeeklyPerformanceDto
{
    public DateOnly Date { get; set; }
    public int Annotations { get; set; }
    public int Reviews { get; set; }
    public int TotalWorkload { get; set; }
}

public class ManagerStatsDto
{
    public int ActiveProjects { get; set; }
    public int IncompletedProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int TotalProjects { get; set; }
    public List<WeeklyPerformanceDto> WeeklyPerformance { get; set; } = new();
}
