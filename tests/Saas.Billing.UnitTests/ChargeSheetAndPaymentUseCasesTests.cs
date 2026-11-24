using Saas.Billing.Application.UseCases.ChargeSheets;
using Saas.Billing.Application.UseCases.Payments;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.UnitTests;

public sealed class ChargeSheetAndPaymentUseCasesTests
{
    private static readonly Guid NorteTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SurTenantId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid MendozaId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid SalazarId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    private static readonly Guid AuxId = Guid.Parse("20000000-0000-0000-0000-000000000005");
    private static readonly Guid Pat0101Id = Guid.Parse("30000000-0000-0000-0000-000000000101");
    private static readonly Guid Pat0102Id = Guid.Parse("30000000-0000-0000-0000-000000000102");
    private static readonly Guid RecId = Guid.Parse("35000000-0000-0000-0000-000000000101");
    private static readonly Guid EncFinalizedId = Guid.Parse("40000000-0000-0000-0000-000000000110");
    private static readonly Guid EncOutOfScopeId = Guid.Parse("40000000-0000-0000-0000-000000000111");

    private static (
        FakeClockPort Clock,
        FakeTenantBillingPort TenantPort,
        FakeBillingAccessPort AccessPort,
        FakeClinicalGatewayPort ClinicalPort,
        InMemoryChargeSheetRepository SheetRepo,
        InMemoryPaymentRepository PaymentRepo) BuildFixture()
    {
        var clock = new FakeClockPort(new DateTimeOffset(2026, 11, 22, 16, 0, 0, TimeSpan.Zero));
        var tenantPort = new FakeTenantBillingPort();
        tenantPort.Tenants[NorteTenantId] = new TenantBillingState(
            NorteTenantId, "clinica-norte", "Clínica Norte", TenantOperationalState.ACTIVA, true, true, "2026-11-22T10:00:00-06:00");
        tenantPort.Tenants[SurTenantId] = new TenantBillingState(
            SurTenantId, "clinica-sur", "Clínica Sur", TenantOperationalState.ACTIVA, false, true, "2026-11-22T10:00:00-06:00");

        var accessPort = new FakeBillingAccessPort();
        accessPort.Decisions[(AuxId, NorteTenantId, Pat0101Id)] = AccessControlCheckResult.Allow();
        accessPort.Decisions[(AuxId, NorteTenantId, Pat0102Id)] = AccessControlCheckResult.Denied(
            "OUT_OF_BILLING_SCOPE",
            "Fuera de ámbito.");

        var clinicalPort = new FakeClinicalGatewayPort();
        clinicalPort.Encounters[(NorteTenantId, EncFinalizedId)] = new EncounterSummary(
            EncFinalizedId, NorteTenantId, RecId, Pat0101Id, MendozaId, EncounterStatus.FINALIZADA, clock.UtcNow);
        clinicalPort.Encounters[(NorteTenantId, EncOutOfScopeId)] = new EncounterSummary(
            EncOutOfScopeId, NorteTenantId, RecId, Pat0102Id, SalazarId, EncounterStatus.FINALIZADA, clock.UtcNow);

        return (clock, tenantPort, accessPort, clinicalPort, new InMemoryChargeSheetRepository(), new InMemoryPaymentRepository());
    }

    [Fact]
    public async Task UpsertReviewReopenAndPay_EnforcesBillingEnabledScopeAndConcurrency()
    {
        var (clock, tenantPort, accessPort, clinicalPort, sheetRepo, paymentRepo) = BuildFixture();

        var upsert = new UpsertChargeSheetUseCase(tenantPort, accessPort, clinicalPort, sheetRepo, clock);
        var review = new ReviewChargeSheetUseCase(tenantPort, accessPort, sheetRepo, clock);
        var reopen = new ReopenChargeSheetUseCase(tenantPort, accessPort, sheetRepo, clock);
        var pay = new RecordFullPaymentUseCase(tenantPort, accessPort, clinicalPort, sheetRepo, paymentRepo, clock);

        var aux = new BillingActorContext(AuxId, NorteTenantId, OrgRole.AUXILIAR, "Enf. Clara");
        var mendoza = new BillingActorContext(MendozaId, NorteTenantId, OrgRole.ESPECIALISTA, "Dra. Mendoza");
        var surAdmin = new BillingActorContext(Guid.NewGuid(), SurTenantId, OrgRole.ADMIN_CLINICA, "Admin Sur");
        var superAdmin = new BillingActorContext(Guid.NewGuid(), null, OrgRole.SUPER_ADMIN, "SuperAdmin");

        var disabledRes = await upsert.ExecuteAsync(
            surAdmin,
            EncFinalizedId,
            [new("CON-GEN", "Consulta", "CONSULTA", 1, 800m)]);
        Assert.Equal("BILLING_DISABLED", disabledRes.Error.Code);

        var superRes = await upsert.ExecuteAsync(
            superAdmin,
            EncFinalizedId,
            [new("CON-GEN", "Consulta", "CONSULTA", 1, 800m)]);
        Assert.Equal("FORBIDDEN_BILLING_ROLE", superRes.Error.Code);

        var outOfScopeRes = await upsert.ExecuteAsync(
            aux,
            EncOutOfScopeId,
            [new("CON-GEN", "Consulta", "CONSULTA", 1, 800m)]);
        Assert.Equal("OUT_OF_BILLING_SCOPE", outOfScopeRes.Error.Code);

        var created = await upsert.ExecuteAsync(
            mendoza,
            EncFinalizedId,
            [new("CON-ESP", "Consulta especialidad", "CONSULTA", 1, 1200m)]);
        Assert.True(created.IsSuccess);
        Assert.Equal(1200m, created.Value.TotalMxn);

        var reviewed = await review.ExecuteAsync(mendoza, created.Value.Id);
        Assert.True(reviewed.IsSuccess);
        Assert.Equal("REVISADA", reviewed.Value.Status);

        var specPayBlocked = await pay.ExecuteAsync(
            mendoza,
            new RecordPaymentCommand(created.Value.Id, 1200m, "EFECTIVO"));
        Assert.Equal("FORBIDDEN_COLLECTION_ROLE", specPayBlocked.Error.Code);

        var reopened = await reopen.ExecuteAsync(aux, created.Value.Id);
        Assert.Equal("ABIERTA", reopened.Value.Status);

        var reReviewed = await review.ExecuteAsync(aux, created.Value.Id);
        Assert.Equal("REVISADA", reReviewed.Value.Status);

        var paid = await pay.ExecuteAsync(
            aux,
            new RecordPaymentCommand(created.Value.Id, 1200m, "TARJETA"));
        Assert.True(paid.IsSuccess);
        Assert.Equal("COB-2026-0001", paid.Value.Folio);

        paymentRepo.SimulateConcurrentDuplicate = true;
        var sheetInRepo = sheetRepo.Items.First(s => s.Id == created.Value.Id);
        Assert.Equal(ChargeSheetStatus.COBRADA, sheetInRepo.Status);
    }

    [Fact]
    public async Task ClinicalUnavailable_FailsClosedWith503()
    {
        var (clock, tenantPort, accessPort, clinicalPort, sheetRepo, _) = BuildFixture();
        clinicalPort.Unavailable = true;

        var upsert = new UpsertChargeSheetUseCase(tenantPort, accessPort, clinicalPort, sheetRepo, clock);
        var admin = new BillingActorContext(Guid.NewGuid(), NorteTenantId, OrgRole.ADMIN_CLINICA, "Admin");

        var res = await upsert.ExecuteAsync(
            admin,
            EncFinalizedId,
            [new("CON-ESP", "Consulta", "CONSULTA", 1, 1200m)]);

        Assert.False(res.IsSuccess);
        Assert.Equal(503, res.Error.HttpStatus);
        Assert.Equal("CLINICAL_UNAVAILABLE", res.Error.Code);
    }
}
