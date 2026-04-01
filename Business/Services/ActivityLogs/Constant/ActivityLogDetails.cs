namespace DataLabelProject.Business.Services.ActivityLogs.Constant;

public class UserCreatedDetails
{
    public string Username { get; set; } = default!;
}

public class ProjectCreatedDetails
{
    public string ProjectName { get; set; } = default!;
}

public class LabelCreatedDetails
{
    public string LabelName { get; set; } = default!;
}

public class DatasetCreatedDetails
{
    public string DatasetName { get; set; } = default!;
}

public class GuidelineCreatedDetails
{
    public string GuidelineContent { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
}

public class LabelAttachedDetails
{
    public Guid LabelId { get; set; }
    public string LabelName { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
}

public class LabelDetachedDetails
{
    public Guid LabelId { get; set; }
    public string LabelName { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
}

public class AssignmentCreatedDetails
{
    public Guid AssignedTo { get; set; }
    public int SampleCount { get; set; }
}

public class AnnotationSubmittedDetails
{
    public Guid TaskItemId { get; set; }
}

public class ReviewSubmittedDetails
{
    public Guid TaskItemId { get; set; }
    public string Result { get; set; } = default!;
}

public class DatasetAttachedDetails
{
    public Guid DatasetId { get; set; }
    public string DatasetName { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
}

public class DatasetDetachedDetails
{
    public Guid DatasetId { get; set; }
    public string DatasetName { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
}

public class MemberAddedDetails
{
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
}

public class MemberRemovedDetails
{
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
}

public class ConsensusCreatedDetails
{
    public Guid DatasetItemId { get; set; }
}
