using DataLabelProject.Business.Models;

namespace DataLabelProject.Business.Services.ActivityLogs;

public interface IActivityLogMessageBuilder
{
    string Build(ActivityLog log);
}
