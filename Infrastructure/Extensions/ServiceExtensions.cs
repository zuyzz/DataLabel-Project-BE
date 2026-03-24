using DataLabelProject.Data.Repositories.Abstractions;
using DataLabelProject.Data.Repositories.Implementations.Categories;
using DataLabelProject.Data.Repositories.Implementations.Guidelines;
using DataLabelProject.Data.Repositories.Implementations.Labels;
using DataLabelProject.Data.Repositories.Implementations.Projects;
using DataLabelProject.Data.Repositories.Implementations.Roles;
using DataLabelProject.Data.Repositories.Implementations.Users;
using DataLabelProject.Data.Repositories.Implementations.Datasets;
using DataLabelProject.Data.Repositories.Implementations.DatasetItems;
using DataLabelProject.Data.Repositories.Implementations.LabelingTasks;
using DataLabelProject.Data.Repositories.Implementations.Assignments;
using DataLabelProject.Data.Repositories.Implementations.Annotations;
using DataLabelProject.Data.Repositories.Implementations.RefreshTokens;
using DataLabelProject.Business.Services;
using DataLabelProject.Business.Services.Auth;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Business.Services.Roles;
using DataLabelProject.Business.Services.Categories;
using DataLabelProject.Business.Services.Projects;
using DataLabelProject.Business.Services.Labels;
using DataLabelProject.Business.Services.Guidelines;
using DataLabelProject.Business.Services.Datasets;
using DataLabelProject.Business.Services.DatasetItems;
using DataLabelProject.Business.Services.FileUpload;
using DataLabelProject.Business.Services.FileUpload.Metadata;
using DataLabelProject.Business.Services.Annotations;
using DataLabelProject.Business.Services.Tasks;
using DataLabelProject.Business.Services.Assignments;
using DataLabelProject.Business.Services.TaskItems;
using DataLabelProject.Business.Services.Exports;
using DataLabelProject.Data.Repositories.Implementations.Reviews;
using DataLabelProject.Business.Services.Reviews;
using DataLabelProject.Data.Repositories.Implementations.Consensus;
using DataLabelProject.Business.Services.Consensus;
using DataLabelProject.Business.Services.Statistics;
using DataLabelProject.Data.Repositories.Implementations.ProjectConfigs;
using DataLabelProject.Business.Events.Abstraction;
using DataLabelProject.Business.Events.Dispatcher;
using DataLabelProject.Business.Events.DomainEvents.Project;
using DataLabelProject.Business.Events.Handlers.ProjectConfigs;
using DataLabelProject.Business.Events.Handlers.Guidelines;

namespace DataLabelProject.Infrastructure.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
        services.AddScoped<IProjectLabelRepository, ProjectLabelRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<IGuidelineRepository, GuidelineRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IDatasetItemRepository, DatasetItemRepository>();
        services.AddScoped<ILabelingTaskRepository, LabelingTaskRepository>();
        services.AddScoped<ILabelingTaskItemRepository, LabelingTaskItemRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IAnnotationRepository, AnnotationRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IConsensusRepository, ConsensusRepository>();
        services.AddScoped<IProjectConfigRepository, ProjectConfigRepository>();

        // Services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();
        services.AddScoped<ILabelService, LabelService>();
        services.AddScoped<IGuidelineService, GuidelineService>();
        services.AddScoped<IDatasetService, DatasetService>();
        services.AddScoped<IDatasetItemService, DatasetItemService>();
        services.AddScoped<ILabelingTaskService, LabelingTaskService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<ITaskItemService, TaskItemService>();
        services.AddScoped<IAnnotationService, AnnotationService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IConsensusService, ConsensusService>();
        services.AddScoped<IAgreementService, AgreementService>();
        services.AddScoped<IClusteringService, ClusteringService>();
        services.AddScoped<IIoUService, IoUService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IStatisticsService, StatisticsService>();

        // File upload strategies
        services.AddScoped<IFileUploadStrategy, ImageUploadStrategy>();
        services.AddScoped<IFileUploadStrategy, ArchiveUploadStrategy>();

        // Metadata extractors
        services.AddScoped<IMetadataExtractor, ImageMetadataExtractor>();
        services.AddScoped<MetadataExtractorFactory>();

        // Event Dispatcher
        services.AddScoped<IEventDispatcher, EventDispatcher>();

        // Event Handlers
        // --- Project ---
        services.AddScoped<IEventHandler<ProjectCreatedEvent>, CreateMemberHandler>();
        services.AddScoped<IEventHandler<ProjectCreatedEvent>, CreateConfigHandler>();
        services.AddScoped<IEventHandler<ProjectDeletedEvent>, DeleteConfigHandler>();

        return services;
    }
}
