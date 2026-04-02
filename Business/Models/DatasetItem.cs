using System;
using System.Collections.Generic;

namespace DataLabelProject.Business.Models;

public partial class DatasetItem
{
    public Guid DatasetItemId { get; set; }

    public Guid DatasetId { get; set; }

    public string MediaType { get; set; } = null!;

    public string StorageUri { get; set; } = null!;

    public string Metadata { get; set; } = null!;

    public string ContentHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Dataset ItemDataset { get; set; } = null!;

    public virtual ICollection<LabelingTaskItem> TaskItems { get; set; } = new List<LabelingTaskItem>();
}
