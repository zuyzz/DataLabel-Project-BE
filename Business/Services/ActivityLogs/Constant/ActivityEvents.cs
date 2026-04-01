namespace DataLabelProject.Business.Services.ActivityLogs.Constant;

public static class ActivityEvents
{
    // User events
    public const string UserCreated = "USER_CREATED";

    // Project events
    public const string ProjectCreated = "PROJECT_CREATED";
    public const string ProjectUpdated = "PROJECT_UPDATED";
    public const string ProjectDeactivated = "PROJECT_DEACTIVATED";

    // Label events
    public const string LabelCreated = "LABEL_CREATED";
    public const string LabelAttached = "LABEL_ATTACHED";
    public const string LabelDetached = "LABEL_DETACHED";

    // Dataset events
    public const string DatasetCreated = "DATASET_CREATED";
    public const string DatasetAttached = "DATASET_ATTACHED";
    public const string DatasetDetached = "DATASET_DETACHED";

    // Guideline events
    public const string GuidelineCreated = "GUIDELINE_CREATED";
    public const string GuidelineUpdated = "GUIDELINE_UPDATED";

    // Task events
    public const string TaskCreated = "TASK_CREATED";

    // Assignment events
    public const string AssignmentCreated = "ASSIGNMENT_CREATED";

    // Annotation events
    public const string AnnotationSubmitted = "ANNOTATION_SUBMITTED";
    public const string AnnotationUpdated = "ANNOTATION_UPDATED";

    // Review events
    public const string ReviewSubmitted = "REVIEW_SUBMITTED";

    // Member events
    public const string MemberAdded = "MEMBER_ADDED";
    public const string MemberRemoved = "MEMBER_REMOVED";

    // Consensus events
    public const string ConsensusCreated = "CONSENSUS_CREATED";
}
