using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Infrastructure.Persistence.Configurations;

internal sealed class PatientPaymentConfiguration : IEntityTypeConfiguration<PatientPayment>
{
    public void Configure(EntityTypeBuilder<PatientPayment> builder)
    {
        builder.ToTable("patient_payments", t =>
        {
            t.HasCheckConstraint("ck_patient_payments_amount_pos", "amount_mxn > 0");
        });
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.Folio).HasColumnName("folio").HasMaxLength(32).IsRequired();
        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.ChargeSheetId).HasColumnName("charge_sheet_id").IsRequired();
        builder.Property(p => p.EncounterId).HasColumnName("encounter_id").IsRequired();
        builder.Property(p => p.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(p => p.SpecialistId).HasColumnName("specialist_id").IsRequired();
        builder.Property(p => p.AmountMxn).HasColumnName("amount_mxn").HasPrecision(12, 2).IsRequired();
        builder.Property(p => p.Method).HasColumnName("method").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(p => p.PaidAtUtc).HasColumnName("paid_at_utc").IsRequired();
        builder.Property(p => p.CollectedByAccountId).HasColumnName("collected_by_account_id").IsRequired();
        builder.Property(p => p.CollectedByName).HasColumnName("collected_by_name").HasMaxLength(160).IsRequired();
        builder.Property(p => p.CutId).HasColumnName("cut_id");

        builder.HasOne<EncounterChargeSheet>()
            .WithOne()
            .HasForeignKey<PatientPayment>(p => p.ChargeSheetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SpecialistCut>()
            .WithMany()
            .HasForeignKey(p => p.CutId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.ChargeSheetId)
            .IsUnique()
            .HasDatabaseName("ux_patient_payments_charge_sheet");

        builder.HasIndex(p => new { p.TenantId, p.Folio })
            .IsUnique()
            .HasDatabaseName("ux_patient_payments_tenant_folio");

        builder.HasIndex(p => new { p.TenantId, p.SpecialistId, p.PaidAtUtc })
            .HasDatabaseName("ix_patient_payments_tenant_spec_paid");

        builder.HasIndex(p => p.CutId)
            .HasDatabaseName("ix_patient_payments_cut");
    }
}
