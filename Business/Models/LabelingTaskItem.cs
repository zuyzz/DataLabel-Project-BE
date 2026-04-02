using System;
using System.Collections.Generic;
using DataLabelProject.Business.Models.Enums;

namespace DataLabelProject.Business.Models;

public partial class LabelingTaskItem
{
    public Guid TaskItemId { get; set; }

    public Guid? TaskId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid DatasetItemId { get; set; }

    public LabelingTaskItemStatus Status { get; set; } = LabelingTaskItemStatus.Unassigned;

    public virtual LabelingTask? Task { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;
    
    public virtual DatasetItem DatasetItem { get; set; } = null!;

    public virtual ICollection<Annotation> Annotations { get; set; } = new List<Annotation>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
