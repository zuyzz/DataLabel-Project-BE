using System.ComponentModel.DataAnnotations;

namespace DataLabelProject.Application.DTOs.Exports;

public class CreateExportRequest
{
    [Required]
    public string Format { get; set; } = null!;

    [Range(0.5, 0.95)]
    public double? TrainSplitRatio { get; set; } = 0.8;

    public int? RandomSeed { get; set; }
}
