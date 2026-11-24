using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Infrastructure.Persistence.Configurations;

internal sealed class SpecialistCutEventConfiguration : IEntityTypeConfiguration<SpecialistCutEvent>
{
    public void Configure(EntityTypeBuilder<SpecialistCutEvent> builder)
    {
        builder.ToTable("specialist_cut_events");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(h => h.SpecialistCutId).HasColumnName("specialist_cut_id").IsRequired();
        builder.Property(h => h.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(h => h.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(h => h.ActorName).HasColumnName("actor_name").HasMaxLength(160).IsRequired();
        builder.Property(h => h.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(h => h.Note).HasColumnName("note").HasMaxLength(500).IsRequired();

        builder.HasIndex(h => new { h.SpecialistCutId, h.OccurredAtUtc })
            .HasDatabaseName("ix_specialist_cut_events_cut_time");
    }
}
