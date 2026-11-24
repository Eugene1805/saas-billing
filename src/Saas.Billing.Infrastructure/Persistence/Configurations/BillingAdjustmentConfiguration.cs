using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Infrastructure.Persistence.Configurations;

internal sealed class BillingAdjustmentConfiguration : IEntityTypeConfiguration<BillingAdjustment>
{
    public void Configure(EntityTypeBuilder<BillingAdjustment> builder)
    {
        builder.ToTable("billing_adjustments", t =>
        {
            t.HasCheckConstraint("ck_billing_adjustments_nonzero", "delta_mxn <> 0");
        });
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.Folio).HasColumnName("folio").HasMaxLength(32).IsRequired();
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.TargetType).HasColumnName("target_type").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(a => a.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(a => a.DeltaMxn).HasColumnName("delta_mxn").HasPrecision(12, 2).IsRequired();
        builder.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(a => a.ActorAccountId).HasColumnName("actor_account_id").IsRequired();
        builder.Property(a => a.ActorName).HasColumnName("actor_name").HasMaxLength(160).IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.Folio })
            .IsUnique()
            .HasDatabaseName("ux_billing_adjustments_tenant_folio");

        builder.HasIndex(a => new { a.TenantId, a.TargetType, a.TargetId })
            .HasDatabaseName("ix_billing_adjustments_target");
    }
}
