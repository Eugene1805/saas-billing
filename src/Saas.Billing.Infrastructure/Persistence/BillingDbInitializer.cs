using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence.Tenancy;

namespace Saas.Billing.Infrastructure.Persistence;

public sealed class BillingDbInitializer(
    ITenantBillingDbContextFactory tenantDbContextFactory,
    ITenantDatabaseMigratorPort tenantDatabaseMigrator,
    ITenantConnectionResolver tenantConnectionResolver,
    IConfiguration configuration)
{
    // IDs deterministas compartidos 1:1 con saas-identity, saas-agenda y el frontend prototipo Vital
    public static readonly Guid NorteTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ValleTenantId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid AuroraTenantId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid SurNoBillingTenantId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    public static readonly Guid SuperAdminAccountId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid MendozaNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid SalazarNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid AuxRamirezNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000005");
    public static readonly Guid VegaNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000006");
    public static readonly Guid AdminValleAccountId = Guid.Parse("20000000-0000-0000-0000-000000000007");
    public static readonly Guid MendozaValleAccountId = Guid.Parse("20000000-0000-0000-0000-000000000008");
    public static readonly Guid AdminSurAccountId = Guid.Parse("20000000-0000-0000-0000-000000000009");

    public static readonly Guid Pat0101Id = Guid.Parse("30000000-0000-0000-0000-000000000101");
    public static readonly Guid Pat0102Id = Guid.Parse("30000000-0000-0000-0000-000000000102");
    public static readonly Guid Pat0103Id = Guid.Parse("30000000-0000-0000-0000-000000000103");
    public static readonly Guid Pat0104Id = Guid.Parse("30000000-0000-0000-0000-000000000104");
    public static readonly Guid Pat0201Id = Guid.Parse("30000000-0000-0000-0000-000000000201");

    public static readonly Guid Rec0101Id = Guid.Parse("35000000-0000-0000-0000-000000000101");
    public static readonly Guid Rec0102Id = Guid.Parse("35000000-0000-0000-0000-000000000102");
    public static readonly Guid Rec0104Id = Guid.Parse("35000000-0000-0000-0000-000000000104");

    public static readonly Guid Enc0101_1Id = Guid.Parse("40000000-0000-0000-0000-000000000101");
    public static readonly Guid Enc0101_2Id = Guid.Parse("40000000-0000-0000-0000-000000000102");
    public static readonly Guid Enc0102_1Id = Guid.Parse("40000000-0000-0000-0000-000000000103");
    public static readonly Guid Enc0104_1Id = Guid.Parse("40000000-0000-0000-0000-000000000104");
    public static readonly Guid Enc0101_InProgressId = Guid.Parse("40000000-0000-0000-0000-000000000105");

    public static readonly Guid SeedChargeSheet1Id = Guid.Parse("50000000-0000-0000-0000-000000000101");
    public static readonly Guid SeedChargeSheet2Id = Guid.Parse("50000000-0000-0000-0000-000000000102");
    public static readonly Guid SeedChargeSheet3Id = Guid.Parse("50000000-0000-0000-0000-000000000103");

    public static readonly Guid SeedPayment1Id = Guid.Parse("60000000-0000-0000-0000-000000000001");
    public static readonly Guid SeedPayment2Id = Guid.Parse("60000000-0000-0000-0000-000000000002");

    public static readonly Guid SeedCut1Id = Guid.Parse("70000000-0000-0000-0000-000000000001");

    public async Task InitializeConfiguredTenantsAsync(
        bool isProduction,
        CancellationToken cancellationToken = default)
    {
        foreach (var tenantId in tenantConnectionResolver.ListConfiguredTenantIds())
        {
            await tenantDatabaseMigrator.EnsureMigratedAsync(tenantId, cancellationToken);
        }

        if (isProduction || !configuration.GetValue("SeedDemoData", false))
        {
            return;
        }

        if (!tenantConnectionResolver.TryResolveConnectionString(NorteTenantId, out _))
        {
            return;
        }

        await SeedNorteTenantIfEmptyAsync(cancellationToken);
    }

    private async Task SeedNorteTenantIfEmptyAsync(CancellationToken cancellationToken)
    {
        await tenantDatabaseMigrator.EnsureMigratedAsync(NorteTenantId, cancellationToken);
        await using var dbContext = tenantDbContextFactory.CreateDbContext(NorteTenantId);

        if (await dbContext.ChargeSheets.AnyAsync(cancellationToken))
        {
            return;
        }

        var gmt6 = TimeSpan.FromHours(-6);

        var sheet1 = EncounterChargeSheet.Create(
            SeedChargeSheet1Id,
            NorteTenantId,
            Enc0101_1Id,
            Rec0101Id,
            Pat0101Id,
            MendozaNorteAccountId,
            [
                new("CON-ESP", "Consulta de especialidad", ChargeConceptCategory.CONSULTA, 1, 1200m),
                new("EST-ECG", "Electrocardiograma en consultorio", ChargeConceptCategory.ESTUDIO, 1, 650m),
            ],
            "Enf. Clara Ramírez",
            new DateTimeOffset(2026, 11, 14, 12, 28, 0, gmt6));
        sheet1.MarkReviewed("Enf. Clara Ramírez", new DateTimeOffset(2026, 11, 14, 12, 35, 0, gmt6));
        sheet1.MarkPaid("COB-2026-0001", PaymentMethod.TARJETA, "Enf. Clara Ramírez", new DateTimeOffset(2026, 11, 14, 12, 40, 0, gmt6));

        var sheet2 = EncounterChargeSheet.Create(
            SeedChargeSheet2Id,
            NorteTenantId,
            Enc0101_2Id,
            Rec0101Id,
            Pat0101Id,
            MendozaNorteAccountId,
            [
                new("CON-ESP", "Consulta de especialidad", ChargeConceptCategory.CONSULTA, 1, 1200m),
            ],
            "Enf. Clara Ramírez",
            new DateTimeOffset(2026, 11, 18, 11, 6, 0, gmt6));

        var sheet3 = EncounterChargeSheet.Create(
            SeedChargeSheet3Id,
            NorteTenantId,
            Enc0102_1Id,
            Rec0102Id,
            Pat0102Id,
            SalazarNorteAccountId,
            [
                new("CON-GEN", "Consulta médica general", ChargeConceptCategory.CONSULTA, 1, 800m),
                new("INS-MED", "Insumo aplicado en consulta", ChargeConceptCategory.INSUMO, 1, 350m),
            ],
            "Lic. Mariana Paredes",
            new DateTimeOffset(2026, 11, 15, 16, 0, 0, gmt6));
        sheet3.MarkReviewed("Lic. Mariana Paredes", new DateTimeOffset(2026, 11, 15, 16, 5, 0, gmt6));
        sheet3.MarkPaid("COB-2026-0002", PaymentMethod.EFECTIVO, "Lic. Mariana Paredes", new DateTimeOffset(2026, 11, 15, 16, 10, 0, gmt6));

        var payment1 = PatientPayment.Create(
            SeedPayment1Id,
            "COB-2026-0001",
            NorteTenantId,
            SeedChargeSheet1Id,
            Enc0101_1Id,
            Pat0101Id,
            MendozaNorteAccountId,
            1850m,
            PaymentMethod.TARJETA,
            new DateTimeOffset(2026, 11, 14, 12, 40, 0, gmt6),
            AuxRamirezNorteAccountId,
            "Enf. Clara Ramírez");

        var payment2 = PatientPayment.Create(
            SeedPayment2Id,
            "COB-2026-0002",
            NorteTenantId,
            SeedChargeSheet3Id,
            Enc0102_1Id,
            Pat0102Id,
            SalazarNorteAccountId,
            1150m,
            PaymentMethod.EFECTIVO,
            new DateTimeOffset(2026, 11, 15, 16, 10, 0, gmt6),
            AdminNorteAccountId,
            "Lic. Mariana Paredes");

        var cut1 = SpecialistCut.Create(
            SeedCut1Id,
            "CORTE-2026-0001",
            NorteTenantId,
            MendozaNorteAccountId,
            "Dra. Sofía Mendoza",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 15),
            [payment1],
            AuxRamirezNorteAccountId,
            "Enf. Clara Ramírez",
            new DateTimeOffset(2026, 11, 15, 18, 0, 0, gmt6),
            "Corte inicial preparado para Dra. Sofía Mendoza con COB-2026-0001.");

        dbContext.ChargeSheets.AddRange(sheet1, sheet2, sheet3);
        dbContext.SpecialistCuts.Add(cut1);
        dbContext.PatientPayments.AddRange(payment1, payment2);

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }
}
