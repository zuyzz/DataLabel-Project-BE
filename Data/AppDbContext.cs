using System;
using System.Collections.Generic;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ActivityLog> ActivityLogs { get; set; }

    public virtual DbSet<Annotation> Annotations { get; set; }


    public virtual DbSet<Assignment> Assignments { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Consensus> Consensuses { get; set; }

    public virtual DbSet<Dataset> Datasets { get; set; }

    public virtual DbSet<DatasetItem> DatasetItems { get; set; }

    public virtual DbSet<Guideline> Guidelines { get; set; }

    public virtual DbSet<Label> Labels { get; set; }

    public virtual DbSet<LabelingTask> LabelingTasks { get; set; }

    public virtual DbSet<LabelingTaskItem> LabelingTaskItems { get; set; }

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<ProjectConfig> ProjectConfigs { get; set; }

    public virtual DbSet<ProjectLabel> ProjectLabels { get; set; }

    public virtual DbSet<ProjectMember> ProjectMembers { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Role> Roles { get; set; }


    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureSupabaseOptions(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private void ConfigureSupabaseOptions(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })

            // project/dataset media type enum
            .HasPostgresEnum("public", "enum_media_type", new[] { "image", "audio", "video" })
            // labeling task status enum
            .HasPostgresEnum<LabelingTaskStatus>("public", "enum_task_status")
            // labeling task item status enum
            .HasPostgresEnum<LabelingTaskItemStatus>("public", "enum_task_item_status")
            // annotation status enum
            .HasPostgresEnum<AnnotationStatus>("public", "enum_annotation_status")
            // review result enum
            .HasPostgresEnum<ReviewResult>("public", "enum_review_result")

            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("graphql", "pg_graphql")
            .HasPostgresExtension("vault", "supabase_vault");
    }
}
