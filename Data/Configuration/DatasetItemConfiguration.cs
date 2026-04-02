using DataLabelProject.Business.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataLabelProject.Data.Configuration;

public class DatasetItemConfiguration : IEntityTypeConfiguration<DatasetItem>
{
    public void Configure(EntityTypeBuilder<DatasetItem> entity)
    {
        entity.HasKey(e => e.DatasetItemId).HasName("DatasetItem_pkey");

        entity.ToTable("DatasetItem");

        entity.Property(e => e.DatasetItemId).HasColumnName("itemId").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.DatasetId).HasColumnName("datasetId");
        entity.Property(e => e.MediaType).HasColumnName("mediaType").HasColumnType("character varying");
        entity.Property(e => e.StorageUri).HasColumnName("storageUri");
        entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        entity.Property(e => e.ContentHash).HasColumnName("contentHash");
        entity.Property(e => e.CreatedAt).HasColumnName("createdAt").HasDefaultValueSql("now()");

        entity.HasOne(d => d.ItemDataset).WithMany(p => p.DatasetItems)
            .HasForeignKey(d => d.DatasetId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("DatasetItem_datasetId_fkey");
    }
}
