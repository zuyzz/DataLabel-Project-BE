using System.Text.Json;
using DataLabelProject.Business.Services.ActivityLogs.Constant;
using DataLabelProject.Business.Models;

namespace DataLabelProject.Business.Services.ActivityLogs;

public class ActivityLogMessageBuilder : IActivityLogMessageBuilder
{
    public string Build(ActivityLog log)
    {
        var username = log.ActivityLogUser?.Username ?? "Someone";

        return log.EventType switch
        {
            ActivityEvents.UserCreated =>
                $"{username} created user {Deserialize<UserCreatedDetails>(log.Details)?.Username}",

            ActivityEvents.ProjectCreated =>
                $"{username} created project '{Deserialize<ProjectCreatedDetails>(log.Details)?.ProjectName}'",

            ActivityEvents.ProjectUpdated =>
                $"{username} updated project '{log.Project?.Name}'",

            ActivityEvents.ProjectDeactivated =>
                $"{username} deactivated project '{log.Project?.Name}'",

            ActivityEvents.LabelCreated =>
                $"{username} created label '{Deserialize<LabelCreatedDetails>(log.Details)?.LabelName}'",

            ActivityEvents.LabelAttached =>
                $"{username} attached label '{Deserialize<LabelAttachedDetails>(log.Details)?.LabelName}' to project '{Deserialize<LabelAttachedDetails>(log.Details)?.ProjectName}'",

            ActivityEvents.LabelDetached =>
                $"{username} detached label '{Deserialize<LabelDetachedDetails>(log.Details)?.LabelName}' from project '{Deserialize<LabelDetachedDetails>(log.Details)?.ProjectName}'",

            ActivityEvents.DatasetCreated =>
                $"{username} created dataset '{Deserialize<DatasetCreatedDetails>(log.Details)?.DatasetName}'",

            ActivityEvents.DatasetAttached =>
                $"{username} attached dataset '{Deserialize<DatasetAttachedDetails>(log.Details)?.DatasetName}' to project '{Deserialize<DatasetAttachedDetails>(log.Details)?.ProjectName}'",

            ActivityEvents.DatasetDetached =>
                $"{username} detached dataset '{Deserialize<DatasetDetachedDetails>(log.Details)?.DatasetName}' from project '{Deserialize<DatasetDetachedDetails>(log.Details)?.ProjectName}'",

            ActivityEvents.GuidelineCreated =>
                $"{username} created guideline for project '{Deserialize<GuidelineCreatedDetails>(log.Details)?.ProjectName}'",

            ActivityEvents.GuidelineUpdated =>
                $"{username} updated guideline for project '{log.Project?.Name}'",

            ActivityEvents.TaskCreated =>
                $"{username} created task in project '{log.Project?.Name}'",

            ActivityEvents.AssignmentCreated =>
                $"{username} assigned {Deserialize<AssignmentCreatedDetails>(log.Details)?.SampleCount} samples to user in project '{log.Project?.Name}'",

            ActivityEvents.AnnotationSubmitted =>
                $"{username} submitted annotation for task item in project '{log.Project?.Name}'",

            ActivityEvents.AnnotationUpdated =>
                $"{username} updated annotation in project '{log.Project?.Name}'",

            ActivityEvents.ReviewSubmitted =>
                $"{username} submitted review with result '{Deserialize<ReviewSubmittedDetails>(log.Details)?.Result}' in project '{log.Project?.Name}'",

            ActivityEvents.MemberAdded =>
                $"{username} added member '{Deserialize<MemberAddedDetails>(log.Details)?.MemberName}' to project '{Deserialize<MemberAddedDetails>(log.Details)?.ProjectName}'",

            ActivityEvents.MemberRemoved =>
                $"{username} removed member '{Deserialize<MemberRemovedDetails>(log.Details)?.MemberName}' from project '{Deserialize<MemberRemovedDetails>(log.Details)?.ProjectName}'",

            ActivityEvents.ConsensusCreated =>
                $"{username} created consensus for dataset item in project '{log.Project?.Name}'",

            _ =>
                $"{username} performed {log.EventType} in project '{log.Project?.Name}'"
        };
    }

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (json == null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null;
        }
    }
}
