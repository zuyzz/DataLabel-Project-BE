using System;
using System.Collections.Generic;

namespace DataLabelProject.Business.Models;

public partial class Project
{
    public Guid ProjectId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public Guid CategoryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual ICollection<Guideline> Guidelines { get; set; } = new List<Guideline>();

    public virtual ICollection<LabelingTask> Tasks { get; set; } = new List<LabelingTask>();

    public virtual ICollection<LabelingTaskItem> TaskItems { get; set; } = new List<LabelingTaskItem>();

    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();

    public virtual ICollection<ProjectLabel> ProjectLabels { get; set; } = new List<ProjectLabel>();

    public virtual ICollection<ProjectConfig> ProjectConfigs { get; set; } = new List<ProjectConfig>();

    public virtual Category ProjectCategory { get; set; } = null!;

    public virtual User? CreatedByUser { get; set; }
}
