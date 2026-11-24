using Saas.Billing.Application.UseCases.Metrics;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.UnitTests;

public sealed class MetricsUseCasesTests
{
    private static readonly Guid NorteTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SurTenantId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    [Fact]
    public async Task ClinicMetrics_DistinguishesAvailableZeroAndNotApplicable()
    {
        var clock = new FakeClockPort(new DateTimeOffset(2026, 11, 22, 16, 0, 0, TimeSpan.Zero));
        var tenantPort = new FakeTenantBillingPort();
        tenantPort.Tenants[NorteTenantId] = new TenantBillingState(
            NorteTenantId, "clinica-norte", "Clínica Norte", TenantOperationalState.ACTIVA, true, true, "2026-11-22T10:00:00-06:00");
        tenantPort.Tenants[SurTenantId] = new TenantBillingState(
            SurTenantId, "clinica-sur", "Clínica Sur", TenantOperationalState.ACTIVA, false, true, "2026-11-22T10:00:00-06:00");

        var paymentRepo = new InMemoryPaymentRepository();
        var cutRepo = new InMemoryCutRepository();

        var p1 = PatientPayment.Create(
            Guid.NewGuid(),
            "COB-2026-0001",
            NorteTenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1850m,
            PaymentMethod.TARJETA,
            new DateTimeOffset(2026, 11, 14, 18, 40, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            "Enf. Clara");
        paymentRepo.Items.Add(p1);

        var cut = SpecialistCut.Create(
            Guid.NewGuid(),
            "CORTE-2026-0001",
            NorteTenantId,
            p1.SpecialistId,
            "Dra. Mendoza",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 15),
            [p1],
            Guid.NewGuid(),
            "Enf. Clara",
            clock.UtcNow);
        cutRepo.Items.Add(cut);

        var payMetric = new GetClinicPaymentsMetricUseCase(tenantPort, paymentRepo, clock);
        var cutMetric = new GetClinicCutsMetricUseCase(tenantPort, cutRepo, clock);

        var adminNorte = new BillingActorContext(Guid.NewGuid(), NorteTenantId, OrgRole.ADMIN_CLINICA, "Admin Norte");
        var adminSur = new BillingActorContext(Guid.NewGuid(), SurTenantId, OrgRole.ADMIN_CLINICA, "Admin Sur");
        var superAdmin = new BillingActorContext(Guid.NewGuid(), null, OrgRole.SUPER_ADMIN, "SuperAdmin");

        var forbidden = await payMetric.ExecuteAsync(superAdmin, null, null);
        Assert.Equal("FORBIDDEN_METRICS_ROLE", forbidden.Error.Code);

        var nortePay = await payMetric.ExecuteAsync(adminNorte, "2026-11-01", "2026-11-30");
        Assert.Equal("AVAILABLE", nortePay.Value.Status);
        Assert.Equal(1850m, nortePay.Value.Data!.TotalMxn);
        Assert.Equal(1850m, nortePay.Value.Data.ByMethod.TARJETA);

        var emptyZero = await payMetric.ExecuteAsync(adminNorte, "2025-01-01", "2025-01-31");
        Assert.Equal("AVAILABLE", emptyZero.Value.Status);
        Assert.Equal(0m, emptyZero.Value.Data!.TotalMxn);
        Assert.Equal(0, emptyZero.Value.Data.PaymentCount);

        var norteCuts = await cutMetric.ExecuteAsync(adminNorte, "2026-11-01", "2026-11-30");
        Assert.Equal("AVAILABLE", norteCuts.Value.Status);
        Assert.Equal(1, norteCuts.Value.Data!.TotalCuts);
        Assert.Equal(1, norteCuts.Value.Data.PreparadoCount);

        var surPay = await payMetric.ExecuteAsync(adminSur, null, null);
        Assert.Equal("NOT_APPLICABLE", surPay.Value.Status);
        Assert.Null(surPay.Value.Data);

        var surCuts = await cutMetric.ExecuteAsync(adminSur, null, null);
        Assert.Equal("NOT_APPLICABLE", surCuts.Value.Status);
        Assert.Null(surCuts.Value.Data);
    }
}
