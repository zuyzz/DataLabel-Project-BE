using DataLabelProject.Business.Models.Enums;
using DataLabelProject.Business.Services.Storage;
using DataLabelProject.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataLabelProject.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<LabelingTaskStatus>("enum_task_status");
        dataSourceBuilder.MapEnum<LabelingTaskItemStatus>("enum_task_item_status");
        dataSourceBuilder.MapEnum<AnnotationStatus>("enum_annotation_status");
        dataSourceBuilder.MapEnum<ReviewResult>("enum_review_result");
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                npgsqlOptions.CommandTimeout(30);
            });

            if (env.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        return services;
    }

    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SupabaseStorageOptions>(
            configuration.GetSection("SupabaseStorage"));

        services.AddHttpClient<IFileStorage, SupabaseFileStorage>();

        return services;
    }
}
