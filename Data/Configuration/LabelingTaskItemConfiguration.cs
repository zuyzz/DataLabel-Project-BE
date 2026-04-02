using DataLabelProject.Business.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataLabelProject.Data.Configuration;

public class LabelingTaskItemConfiguration : IEntityTypeConfiguration<LabelingTaskItem>
{
    public void Configure(EntityTypeBuilder<LabelingTaskItem> entity)
    {
        entity.HasKey(e => e.TaskItemId).HasName("LabelingTaskItem_pkey");

        entity.ToTable("LabelingTaskItem");

        entity.Property(e => e.TaskItemId).HasColumnName("taskItemId").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.TaskId).HasColumnName("taskId");
        entity.Property(e => e.ProjectId).HasColumnName("projectId");
        entity.Property(e => e.DatasetItemId).HasColumnName("datasetItemId");
        entity.Property(e => e.Status).HasColumnName("status");

        entity.HasOne(d => d.Task).WithMany(p => p.TaskItems)
            .HasForeignKey(d => d.TaskId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("LabelingTaskItem_taskId_fkey");

        entity.HasOne(d => d.Project).WithMany(p => p.TaskItems)
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("LabelingTaskItem_projectId_fkey");

        entity.HasOne(d => d.DatasetItem).WithMany(p => p.TaskItems)
            .HasForeignKey(d => d.DatasetItemId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("LabelingTaskItem_datasetItemId_fkey");
    }
}
