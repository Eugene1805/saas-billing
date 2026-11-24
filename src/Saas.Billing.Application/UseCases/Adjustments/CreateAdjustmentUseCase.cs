using System.Globalization;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.Policies;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Adjustments;

public sealed class CreateAdjustmentUseCase(
    ITenantBillingAccessPort tenantPort,
    IPaymentRepository paymentRepository,
    ISpecialistCutRepository cutRepository,
    IBillingAdjustmentRepository adjustmentRepository,
    IClockPort clock)
{
    public async Task<Result<BillingAdjustmentDto>> ExecuteAsync(
        BillingActorContext actor,
        CreateAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await BillingUseCaseGuards.EnsureTenantBillingEnabledAsync(
            actor,
            tenantPort,
            cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<BillingAdjustmentDto>.Failure(tenantCheck.Error);
        }

        if (actor.Role != OrgRole.ADMIN_CLINICA)
        {
            return Result<BillingAdjustmentDto>.Failure(
                "FORBIDDEN_ADJUSTMENT_ROLE",
                "Solo AdminClinica puede registrar ajustes compensatorios vinculados posteriores al cobro o confirmación.",
                403);
        }

        if (string.IsNullOrWhiteSpace(command.TargetType) ||
            int.TryParse(command.TargetType.Trim(), out _) ||
            !Enum.TryParse<AdjustmentTargetType>(command.TargetType.Trim(), ignoreCase: true, out var targetType) ||
            !Enum.IsDefined(targetType))
        {
            return Result<BillingAdjustmentDto>.Failure(
                "INVALID_ADJUSTMENT_TARGET",
                "targetType debe ser PAYMENT o CUT.",
                400);
        }

        var policyCheck = BillingPolicy.ValidateAdjustment(command.DeltaMxn, command.Reason);
        if (!policyCheck.IsSuccess)
        {
            return Result<BillingAdjustmentDto>.Failure(policyCheck.Error!);
        }

        var tenantId = tenantCheck.Value.TenantId;

        if (targetType == AdjustmentTargetType.PAYMENT)
        {
            var payment = await paymentRepository.GetByIdAsync(tenantId, command.TargetId, cancellationToken);
            if (payment is null)
            {
                return Result<BillingAdjustmentDto>.Failure(
                    "ADJUSTMENT_TARGET_NOT_FOUND",
                    "Cobro objetivo no encontrado dentro de esta clínica.",
                    404);
            }
        }
        else
        {
            var cut = await cutRepository.GetByIdAsync(
                tenantId,
                command.TargetId,
                includeHistory: false,
                tracked: false,
                cancellationToken);
            if (cut is null)
            {
                return Result<BillingAdjustmentDto>.Failure(
                    "ADJUSTMENT_TARGET_NOT_FOUND",
                    "Corte objetivo no encontrado dentro de esta clínica.",
                    404);
            }

            if (cut.Status != SpecialistCutStatus.CONFIRMADO)
            {
                return Result<BillingAdjustmentDto>.Failure(
                    "ADJUSTMENT_REQUIRES_CONFIRMED_CUT",
                    "Antes de confirmar el corte usa la corrección ordinaria; los ajustes compensatorios aplican a cortes ya CONFIRMADO.",
                    409);
            }
        }

        var nowUtc = clock.UtcNow;
        var gmt6Year = Gmt6Clock.ToDateOnlyGmt6(nowUtc).Year;
        var nextSequence = await adjustmentRepository.CountByTenantAsync(tenantId, cancellationToken) + 1;
        var folio = string.Format(CultureInfo.InvariantCulture, "AJU-{0}-{1:D4}", gmt6Year, nextSequence);

        var adjustment = BillingAdjustment.Create(
            Guid.NewGuid(),
            folio,
            tenantId,
            targetType,
            command.TargetId,
            command.DeltaMxn,
            command.Reason,
            actor.AccountId,
            actor.FullName,
            nowUtc);

        await adjustmentRepository.AddAsync(adjustment, cancellationToken);
        return Result<BillingAdjustmentDto>.Success(AdjustmentMapper.ToDto(adjustment));
    }
}
