using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Policies;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Cuts;

public sealed class ReviseCutUseCase(
    ITenantBillingAccessPort tenantPort,
    IPaymentRepository paymentRepository,
    ISpecialistCutRepository cutRepository,
    IBillingAdjustmentRepository adjustmentRepository,
    IClockPort clock)
{
    public async Task<Result<SpecialistCutDetailDto>> ExecuteAsync(
        BillingActorContext actor,
        Guid cutId,
        ReviseCutCommand command,
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

        if (actor.Role != OrgRole.ADMIN_CLINICA && actor.Role != OrgRole.AUXILIAR)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "FORBIDDEN_CUT_PREPARATION",
                "Solo personal administrativo o auxiliar autorizado corrige cortes antes de confirmar.",
                403);
        }

        if (!Gmt6Clock.TryParseGmt6Date(command.PeriodStartGmt6, out var periodStart) ||
            !Gmt6Clock.TryParseGmt6Date(command.PeriodEndGmt6, out var periodEnd))
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "INVALID_CUT_PERIOD",
                "periodStartGmt6 y periodEndGmt6 deben tener formato yyyy-MM-dd.",
                400);
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

        var previousPayments = await paymentRepository.ListByCutIdAsync(
            tenantId,
            cut.Id,
            tracked: true,
            cancellationToken);

        var distinctIds = command.PaymentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "NO_PAYMENTS_IN_PERIOD",
                "El corte debe conservar al menos un cobro asociado.",
                400);
        }

        var nextPayments = await paymentRepository.ListByIdsTrackedAsync(
            tenantId,
            distinctIds,
            cancellationToken);
        if (nextPayments.Count != distinctIds.Count)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "PAYMENT_NOT_FOUND",
                "Uno o más cobros seleccionados no existen dentro de esta clínica.",
                404);
        }

        var policyCheck = BillingPolicy.ValidateCutRevision(
            cut,
            periodStart,
            periodEnd,
            nextPayments,
            command.AdjustmentNote);
        if (!policyCheck.IsSuccess)
        {
            return Result<SpecialistCutDetailDto>.Failure(policyCheck.Error!);
        }

        var evt = cut.ReviseAndResubmit(
            periodStart,
            periodEnd,
            previousPayments,
            nextPayments,
            command.AdjustmentNote,
            actor.FullName,
            clock.UtcNow);

        await cutRepository.UpdateWithEventAsync(cut, evt, cancellationToken);

        var adjustments = await adjustmentRepository.ListAsync(
            tenantId,
            AdjustmentTargetType.CUT,
            cut.Id,
            cancellationToken);

        return Result<SpecialistCutDetailDto>.Success(
            CutMapper.ToDetailDto(cut, nextPayments, adjustments));
    }
}
