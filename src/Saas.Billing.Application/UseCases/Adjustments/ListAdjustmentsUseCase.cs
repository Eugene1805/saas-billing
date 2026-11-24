using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Adjustments;

public sealed class ListAdjustmentsUseCase(
    ITenantBillingAccessPort tenantPort,
    IBillingAdjustmentRepository adjustmentRepository,
    ISpecialistCutRepository cutRepository)
{
    public async Task<Result<AdjustmentListResponse>> ExecuteAsync(
        BillingActorContext actor,
        string? targetTypeRaw,
        Guid? targetId,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await BillingUseCaseGuards.EnsureTenantBillingEnabledAsync(
            actor,
            tenantPort,
            cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<AdjustmentListResponse>.Failure(tenantCheck.Error);
        }

        AdjustmentTargetType? targetType = null;
        if (!string.IsNullOrWhiteSpace(targetTypeRaw))
        {
            if (int.TryParse(targetTypeRaw.Trim(), out _) ||
                !Enum.TryParse<AdjustmentTargetType>(targetTypeRaw.Trim(), ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Result<AdjustmentListResponse>.Failure(
                    "INVALID_ADJUSTMENT_TARGET",
                    "targetType debe ser PAYMENT o CUT.",
                    400);
            }

            targetType = parsed;
        }

        var tenantId = tenantCheck.Value.TenantId;
        var items = await adjustmentRepository.ListAsync(tenantId, targetType, targetId, cancellationToken);

        if (actor.Role == OrgRole.ESPECIALISTA)
        {
            var ownCutIds = (await cutRepository.ListAsync(
                    new CutQueryFilter(tenantId, actor.AccountId, null, null, null),
                    cancellationToken))
                .Select(c => c.Id)
                .ToHashSet();

            items = items
                .Where(a => a.TargetType == AdjustmentTargetType.CUT && ownCutIds.Contains(a.TargetId))
                .ToList();
        }

        var dtos = items.Select(AdjustmentMapper.ToDto).ToList();
        return Result<AdjustmentListResponse>.Success(new AdjustmentListResponse(dtos.Count, dtos));
    }
}
