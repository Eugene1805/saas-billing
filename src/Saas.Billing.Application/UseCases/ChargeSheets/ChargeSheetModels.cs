using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.ChargeSheets;

public sealed record ChargeLineItemInput(
    string ConceptCode,
    string Description,
    string Category,
    int Quantity,
    decimal UnitPriceMxn);

public sealed record ChargeLineItemDto(
    Guid Id,
    string ConceptCode,
    string Description,
    string Category,
    int Quantity,
    decimal UnitPriceMxn,
    decimal LineTotalMxn);

public sealed record ChargeSheetAuditEventDto(
    Guid Id,
    string ActorName,
    string OccurredAtGmt6,
    string Action,
    string Detail);

public sealed record ChargeSheetDto(
    Guid Id,
    Guid TenantId,
    Guid EncounterId,
    Guid RecordId,
    Guid PatientId,
    Guid SpecialistId,
    string Status,
    decimal TotalMxn,
    IReadOnlyList<ChargeLineItemDto> Items,
    string? ReviewedByName,
    string? ReviewedAtGmt6,
    string CreatedAtGmt6,
    string UpdatedAtGmt6,
    IReadOnlyList<ChargeSheetAuditEventDto>? AuditLog = null);

public sealed record ChargeSheetListResponse(
    int Total,
    IReadOnlyList<ChargeSheetDto> Items);

internal static class ChargeSheetMapper
{
    public static ChargeSheetDto ToDto(EncounterChargeSheet sheet, bool includeAuditLog)
    {
        var items = sheet.Items
            .OrderBy(i => i.SortOrder)
            .Select(i => new ChargeLineItemDto(
                i.Id,
                i.ConceptCode,
                i.Description,
                i.Category,
                i.Quantity,
                i.UnitPriceMxn,
                MoneyHelper.RoundMxn(i.Quantity * i.UnitPriceMxn)))
            .ToList();

        var audit = includeAuditLog
            ? sheet.AuditLog
                .OrderBy(a => a.OccurredAtUtc)
                .ThenBy(a => a.Id)
                .Select(a => new ChargeSheetAuditEventDto(
                    a.Id,
                    a.ActorName,
                    Gmt6Clock.FormatIsoGmt6(a.OccurredAtUtc),
                    a.Action,
                    a.Detail))
                .ToList()
            : null;

        return new ChargeSheetDto(
            sheet.Id,
            sheet.TenantId,
            sheet.EncounterId,
            sheet.RecordId,
            sheet.PatientId,
            sheet.SpecialistId,
            sheet.Status.ToString(),
            sheet.TotalMxn,
            items,
            sheet.ReviewedByName,
            sheet.ReviewedAtUtc is DateTimeOffset rev ? Gmt6Clock.FormatIsoGmt6(rev) : null,
            Gmt6Clock.FormatIsoGmt6(sheet.CreatedAtUtc),
            Gmt6Clock.FormatIsoGmt6(sheet.UpdatedAtUtc),
            audit);
    }
}
