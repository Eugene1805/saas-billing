using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Cuts;

public sealed class GetCutByIdUseCase(
    ITenantBillingAccessPort tenantPort,
    ISpecialistCutRepository cutRepository,
    IPaymentRepository paymentRepository,
    IBillingAdjustmentRepository adjustmentRepository)
{
    public async Task<Result<SpecialistCutDetailDto>> ExecuteAsync(
        BillingActorContext actor,
        Guid cutId,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await BillingUseCaseGuards.EnsureTenantBillingEnabledAsync(
            actor,
            tenantPort,
            cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<SpecialistCutDetailDto>.Failure(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value.TenantId;
        var cut = await cutRepository.GetByIdAsync(
            tenantId,
            cutId,
            includeHistory: true,
            tracked: false,
            cancellationToken);

        if (cut is null)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "CUT_NOT_FOUND",
                "Corte por especialista no encontrado dentro de esta clínica.",
                404);
        }

        if (actor.Role == OrgRole.ESPECIALISTA && cut.SpecialistId != actor.AccountId)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "FORBIDDEN_CUT_OWNER",
                "Este corte pertenece a otro especialista de la clínica (CU-23 A1).",
                403);
        }

        var payments = await paymentRepository.ListByCutIdAsync(
            tenantId,
            cutId,
            tracked: false,
            cancellationToken);

        var adjustments = await adjustmentRepository.ListAsync(
            tenantId,
            AdjustmentTargetType.CUT,
            cutId,
            cancellationToken);

        return Result<SpecialistCutDetailDto>.Success(
            CutMapper.ToDetailDto(cut, payments, adjustments));
    }
}
