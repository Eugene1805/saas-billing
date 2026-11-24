using Microsoft.EntityFrameworkCore;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<EncounterChargeSheet> ChargeSheets => Set<EncounterChargeSheet>();
    public DbSet<ChargeLineItem> ChargeLineItems => Set<ChargeLineItem>();
    public DbSet<ChargeSheetAuditEvent> ChargeSheetAuditEvents => Set<ChargeSheetAuditEvent>();
    public DbSet<PatientPayment> PatientPayments => Set<PatientPayment>();
    public DbSet<SpecialistCut> SpecialistCuts => Set<SpecialistCut>();
    public DbSet<SpecialistCutEvent> SpecialistCutEvents => Set<SpecialistCutEvent>();
    public DbSet<BillingAdjustment> BillingAdjustments => Set<BillingAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("billing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
    }
}
