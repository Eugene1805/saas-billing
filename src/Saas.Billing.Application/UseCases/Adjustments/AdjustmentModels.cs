using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Application.UseCases.Adjustments;

public sealed record CreateAdjustmentCommand(
    string TargetType,
    Guid TargetId,
    decimal DeltaMxn,
    string Reason);

public sealed record BillingAdjustmentDto(
    Guid Id,
    string Folio,
    Guid TenantId,
    string TargetType,
    Guid TargetId,
    decimal DeltaMxn,
    string Reason,
    Guid ActorAccountId,
    string ActorName,
    string CreatedAtGmt6);

public sealed record AdjustmentListResponse(
    int Total,
    IReadOnlyList<BillingAdjustmentDto> Items);

internal static class AdjustmentMapper
{
    public static BillingAdjustmentDto ToDto(BillingAdjustment adjustment)
    {
        return new BillingAdjustmentDto(
            adjustment.Id,
            adjustment.Folio,
            adjustment.TenantId,
            adjustment.TargetType.ToString(),
            adjustment.TargetId,
            adjustment.DeltaMxn,
            adjustment.Reason,
            adjustment.ActorAccountId,
            adjustment.ActorName,
            Gmt6Clock.FormatIsoGmt6(adjustment.CreatedAtUtc));
    }
}
