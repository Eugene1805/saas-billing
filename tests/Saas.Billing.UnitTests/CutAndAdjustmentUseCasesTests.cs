using Saas.Billing.Application.UseCases.Adjustments;
using Saas.Billing.Application.UseCases.Cuts;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.UnitTests;

public sealed class CutAndAdjustmentUseCasesTests
{
    private static readonly Guid NorteTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid MendozaId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid SalazarId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    private static readonly Guid AuxId = Guid.Parse("20000000-0000-0000-0000-000000000005");
    private static readonly Guid AdminId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task CutLifecycle_ObserveReviseConfirmAndLinkedAdjustment_WorksEndToEnd()
    {
        var clock = new FakeClockPort(new DateTimeOffset(2026, 11, 22, 16, 0, 0, TimeSpan.Zero));
        var tenantPort = new FakeTenantBillingPort();
        tenantPort.Tenants[NorteTenantId] = new TenantBillingState(
            NorteTenantId, "clinica-norte", "Clínica Norte", TenantOperationalState.ACTIVA, true, true, "2026-11-22T10:00:00-06:00");

        var dirPort = new FakeBillingDirectoryPort();
        dirPort.Specialists[(NorteTenantId, MendozaId)] = new SpecialistSummary(
            MendozaId, NorteTenantId, true, "Dra. Sofía Mendoza", "Cardiología");

        var paymentRepo = new InMemoryPaymentRepository();
        var cutRepo = new InMemoryCutRepository();
        var adjRepo = new InMemoryAdjustmentRepository();

        var p1 = PatientPayment.Create(
            Guid.NewGuid(),
            "COB-2026-0010",
            NorteTenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MendozaId,
            1200m,
            PaymentMethod.EFECTIVO,
            new DateTimeOffset(2026, 11, 18, 17, 0, 0, TimeSpan.Zero),
            AuxId,
            "Enf. Clara");

        var p2 = PatientPayment.Create(
            Guid.NewGuid(),
            "COB-2026-0011",
            NorteTenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MendozaId,
            650m,
            PaymentMethod.TARJETA,
            new DateTimeOffset(2026, 11, 19, 17, 0, 0, TimeSpan.Zero),
            AuxId,
            "Enf. Clara");

        paymentRepo.Items.AddRange([p1, p2]);

        var prepare = new PrepareCutUseCase(tenantPort, dirPort, paymentRepo, cutRepo, clock);
        var observe = new ObserveCutUseCase(tenantPort, cutRepo, paymentRepo, adjRepo, clock);
        var revise = new ReviseCutUseCase(tenantPort, paymentRepo, cutRepo, adjRepo, clock);
        var confirm = new ConfirmCutUseCase(tenantPort, cutRepo, paymentRepo, adjRepo, clock);
        var createAdjustment = new CreateAdjustmentUseCase(tenantPort, paymentRepo, cutRepo, adjRepo, clock);

        var aux = new BillingActorContext(AuxId, NorteTenantId, OrgRole.AUXILIAR, "Enf. Clara");
        var mendoza = new BillingActorContext(MendozaId, NorteTenantId, OrgRole.ESPECIALISTA, "Dra. Mendoza");
        var salazar = new BillingActorContext(SalazarId, NorteTenantId, OrgRole.ESPECIALISTA, "Dr. Salazar");
        var admin = new BillingActorContext(AdminId, NorteTenantId, OrgRole.ADMIN_CLINICA, "Lic. Mariana");

        var prepared = await prepare.ExecuteAsync(
            aux,
            new PrepareCutCommand(MendozaId, "2026-11-15", "2026-11-22", [p1.Id]));
        Assert.True(prepared.IsSuccess);
        Assert.Equal(1200m, prepared.Value.TotalMxn);

        var wrongSpec = await confirm.ExecuteAsync(salazar, prepared.Value.Id, null);
        Assert.Equal("FORBIDDEN_CUT_OWNER", wrongSpec.Error.Code);

        var observed = await observe.ExecuteAsync(
            mendoza,
            prepared.Value.Id,
            new ResolveCutCommand("Falta incluir el estudio p2."));
        Assert.True(observed.IsSuccess);
        Assert.Equal("OBSERVADO", observed.Value.Status);

        var adjBeforeConfirm = await createAdjustment.ExecuteAsync(
            admin,
            new CreateAdjustmentCommand("CUT", prepared.Value.Id, -50m, "Anticipado"));
        Assert.Equal("ADJUSTMENT_REQUIRES_CONFIRMED_CUT", adjBeforeConfirm.Error.Code);

        var revised = await revise.ExecuteAsync(
            aux,
            prepared.Value.Id,
            new ReviseCutCommand("2026-11-15", "2026-11-22", [p1.Id, p2.Id], "Incluido COB-2026-0011"));
        Assert.True(revised.IsSuccess);
        Assert.Equal("PREPARADO", revised.Value.Status);
        Assert.Equal(1850m, revised.Value.TotalMxn);

        var confirmed = await confirm.ExecuteAsync(mendoza, prepared.Value.Id, new ResolveCutCommand("De acuerdo."));
        Assert.True(confirmed.IsSuccess);
        Assert.Equal("CONFIRMADO", confirmed.Value.Status);

        var auxAdjBlocked = await createAdjustment.ExecuteAsync(
            aux,
            new CreateAdjustmentCommand("CUT", confirmed.Value.Id, -100m, "Auxiliar no debe poder"));
        Assert.Equal("FORBIDDEN_ADJUSTMENT_ROLE", auxAdjBlocked.Error.Code);

        var adjustment = await createAdjustment.ExecuteAsync(
            admin,
            new CreateAdjustmentCommand("CUT", confirmed.Value.Id, -100m, "Descuento administrativo autorizado"));
        Assert.True(adjustment.IsSuccess);
        Assert.Equal("AJU-2026-0001", adjustment.Value.Folio);
    }
}
