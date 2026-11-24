using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Infrastructure.Persistence.Configurations;

internal sealed class ChargeSheetAuditEventConfiguration : IEntityTypeConfiguration<ChargeSheetAuditEvent>
{
    public void Configure(EntityTypeBuilder<ChargeSheetAuditEvent> builder)
    {
        builder.ToTable("charge_sheet_audit_events");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.ChargeSheetId).HasColumnName("charge_sheet_id").IsRequired();
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.ActorName).HasColumnName("actor_name").HasMaxLength(160).IsRequired();
        builder.Property(a => a.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(32).IsRequired();
        builder.Property(a => a.Detail).HasColumnName("detail").HasMaxLength(500).IsRequired();

        builder.HasIndex(a => new { a.ChargeSheetId, a.OccurredAtUtc })
            .HasDatabaseName("ix_charge_sheet_audit_sheet_time");
    }
}
