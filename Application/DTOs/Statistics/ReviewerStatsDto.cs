namespace DataLabelProject.Application.DTOs.Statistics;

public class ReviewerStatsDto
{
    public int IncompletedItems { get; set; }
    public int ApprovedItems { get; set; }
    public int RejectedItems { get; set; }
    public int TotalItems { get; set; }
    public int TodayReviews { get; set; }
}
