using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Infrastructure.Persistence.Configurations;

internal sealed class SpecialistCutConfiguration : IEntityTypeConfiguration<SpecialistCut>
{
    public void Configure(EntityTypeBuilder<SpecialistCut> builder)
    {
        builder.ToTable("specialist_cuts", t =>
        {
            t.HasCheckConstraint("ck_specialist_cuts_valid_period", "period_start_gmt6 <= period_end_gmt6");
        });
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.Folio).HasColumnName("folio").HasMaxLength(32).IsRequired();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.SpecialistId).HasColumnName("specialist_id").IsRequired();
        builder.Property(c => c.SpecialistName).HasColumnName("specialist_name").HasMaxLength(160).IsRequired();
        builder.Property(c => c.PeriodStartGmt6).HasColumnName("period_start_gmt6").IsRequired();
        builder.Property(c => c.PeriodEndGmt6).HasColumnName("period_end_gmt6").IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.EfectivoMxn).HasColumnName("efectivo_mxn").HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.TarjetaMxn).HasColumnName("tarjeta_mxn").HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.TransferenciaMxn).HasColumnName("transferencia_mxn").HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.TotalMxn).HasColumnName("total_mxn").HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.PaymentCount).HasColumnName("payment_count").IsRequired();
        builder.Property(c => c.PreparedByAccountId).HasColumnName("prepared_by_account_id").IsRequired();
        builder.Property(c => c.PreparedByName).HasColumnName("prepared_by_name").HasMaxLength(160).IsRequired();
        builder.Property(c => c.PreparedAtUtc).HasColumnName("prepared_at_utc").IsRequired();
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(c => c.ConfirmedByName).HasColumnName("confirmed_by_name").HasMaxLength(160);
        builder.Property(c => c.ConfirmedAtUtc).HasColumnName("confirmed_at_utc");
        builder.Property(c => c.LastObservationNote).HasColumnName("last_observation_note").HasMaxLength(500);

        builder.HasMany(c => c.History)
            .WithOne()
            .HasForeignKey(h => h.SpecialistCutId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.History)
            .HasField("_history")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => new { c.TenantId, c.Folio })
            .IsUnique()
            .HasDatabaseName("ux_specialist_cuts_tenant_folio");

        builder.HasIndex(c => new { c.TenantId, c.SpecialistId, c.Status })
            .HasDatabaseName("ix_specialist_cuts_tenant_spec_status");
    }
}
