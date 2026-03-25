using System.Collections.Generic;

public class ExportDataset
{
    public List<ExportImage> Images { get; set; } = new();
    public List<ExportAnnotation> Annotations { get; set; } = new();
    public List<ExportCategory> Categories { get; set; } = new();
}

public class ExportImage
{
    public int Id { get; set; }
    public string FileName { get; set; } = null!;
    public int Width { get; set; }
    public int Height { get; set; }
    public string StorageUri { get; set; } = null!;
}

public class ExportAnnotation
{
    public int Id { get; set; }
    public int ImageId { get; set; }
    public int CategoryId { get; set; }
    public double[] Bbox { get; set; } = null!;
}

public class ExportCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}