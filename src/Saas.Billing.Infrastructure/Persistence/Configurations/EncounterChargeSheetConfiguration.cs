using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Infrastructure.Persistence.Configurations;

internal sealed class EncounterChargeSheetConfiguration : IEntityTypeConfiguration<EncounterChargeSheet>
{
    public void Configure(EntityTypeBuilder<EncounterChargeSheet> builder)
    {
        builder.ToTable("charge_sheets");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.EncounterId).HasColumnName("encounter_id").IsRequired();
        builder.Property(c => c.RecordId).HasColumnName("record_id").IsRequired();
        builder.Property(c => c.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(c => c.SpecialistId).HasColumnName("specialist_id").IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.TotalMxn).HasColumnName("total_mxn").HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.ReviewedByName).HasColumnName("reviewed_by_name").HasMaxLength(160);
        builder.Property(c => c.ReviewedAtUtc).HasColumnName("reviewed_at_utc");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.ChargeSheetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.AuditLog)
            .WithOne()
            .HasForeignKey(a => a.ChargeSheetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.AuditLog)
            .HasField("_auditLog")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => new { c.TenantId, c.EncounterId })
            .IsUnique()
            .HasDatabaseName("ux_charge_sheets_tenant_encounter");

        builder.HasIndex(c => new { c.TenantId, c.PatientId })
            .HasDatabaseName("ix_charge_sheets_tenant_patient");

        builder.HasIndex(c => new { c.TenantId, c.SpecialistId, c.Status })
            .HasDatabaseName("ix_charge_sheets_tenant_specialist_status");
    }
}
