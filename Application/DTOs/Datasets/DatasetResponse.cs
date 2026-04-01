namespace DataLabelProject.Application.DTOs.Datasets
{
    public record DatasetResponse
    {
        public Guid DatasetId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public bool IsActive { get; set; } 
        public int SampleCount { get; set; }
        public long TotalSize { get; set; } 
    };
}
