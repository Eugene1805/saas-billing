using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Policies;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Cuts;

internal static class CutResolutionHelper
{
    public static async Task<Result<SpecialistCutDetailDto>> ResolveAsync(
        BillingActorContext actor,
        Guid cutId,
        bool isObservation,
        string? note,
        ITenantBillingAccessPort tenantPort,
        ISpecialistCutRepository cutRepository,
        IPaymentRepository paymentRepository,
        IBillingAdjustmentRepository adjustmentRepository,
        IClockPort clock,
        CancellationToken cancellationToken)
    {
        var tenantCheck = await BillingUseCaseGuards.EnsureTenantBillingEnabledAsync(
            actor,
            tenantPort,
            cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<SpecialistCutDetailDto>.Failure(tenantCheck.Error);
        }

        if (actor.Role != OrgRole.ESPECIALISTA)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "FORBIDDEN_CUT_RESOLUTION_ROLE",
                "Solo el especialista titular del corte puede confirmarlo u observarlo (CU-23).",
                403);
        }

        var tenantId = tenantCheck.Value.TenantId;
        var cut = await cutRepository.GetByIdAsync(
            tenantId,
            cutId,
            includeHistory: true,
            tracked: true,
            cancellationToken);
        if (cut is null)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "CUT_NOT_FOUND",
                "Corte por especialista no encontrado dentro de esta clínica.",
                404);
        }

        var policyCheck = BillingPolicy.ValidateCutResolution(
            cut,
            actor.AccountId,
            isObservation,
            note);
        if (!policyCheck.IsSuccess)
        {
            return Result<SpecialistCutDetailDto>.Failure(policyCheck.Error!);
        }

        var nowUtc = clock.UtcNow;
        var evt = isObservation
            ? cut.MarkObserved(actor.FullName, note!, nowUtc)
            : cut.MarkConfirmed(actor.FullName, note, nowUtc);

        await cutRepository.UpdateWithEventAsync(cut, evt, cancellationToken);

        var payments = await paymentRepository.ListByCutIdAsync(
            tenantId,
            cut.Id,
            tracked: false,
            cancellationToken);

        var adjustments = await adjustmentRepository.ListAsync(
            tenantId,
            AdjustmentTargetType.CUT,
            cut.Id,
            cancellationToken);

        return Result<SpecialistCutDetailDto>.Success(
            CutMapper.ToDetailDto(cut, payments, adjustments));
    }
}
