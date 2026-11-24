using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.Policies;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.ChargeSheets;

public sealed class UpsertChargeSheetUseCase(
    ITenantBillingAccessPort tenantPort,
    IBillingAccessControlPort accessPort,
    IClinicalEncounterGatewayPort clinicalPort,
    IChargeSheetRepository sheetRepository,
    IClockPort clock)
{
    public async Task<Result<ChargeSheetDto>> ExecuteAsync(
        BillingActorContext actor,
        Guid encounterId,
        IReadOnlyList<ChargeLineItemInput> items,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await BillingUseCaseGuards.EnsureTenantBillingEnabledAsync(
            actor,
            tenantPort,
            cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<ChargeSheetDto>.Failure(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value.TenantId;

        var encounterLookup = await clinicalPort.GetEncounterAsync(tenantId, encounterId, cancellationToken);
        if (encounterLookup.DependencyUnavailable)
        {
            return Result<ChargeSheetDto>.Failure(
                "CLINICAL_UNAVAILABLE",
                "El servicio Clinical no está disponible para validar la atención médica.",
                503);
        }

        if (!encounterLookup.Found || encounterLookup.Encounter is null)
        {
            return Result<ChargeSheetDto>.Failure(
                "ENCOUNTER_NOT_FOUND",
                "Atención clínica no encontrada dentro de esta clínica.",
                404);
        }

        var encounter = encounterLookup.Encounter;

        var accessCheck = await BillingUseCaseGuards.CheckPatientBillingAccessAsync(
            actor,
            tenantId,
            encounter.PatientId,
            encounter.SpecialistId,
            allowEncounterSpecialist: true,
            BillingAccessOperation.MANAGE_BILLING,
            accessPort,
            cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return Result<ChargeSheetDto>.Failure(accessCheck.Error!);
        }

        var drafts = new List<ChargeLineItemDraft>(items.Count);
        foreach (var input in items)
        {
            if (string.IsNullOrWhiteSpace(input.Category) ||
                int.TryParse(input.Category.Trim(), out _) ||
                !Enum.TryParse<ChargeConceptCategory>(input.Category.Trim(), ignoreCase: true, out var category) ||
                !Enum.IsDefined(category))
            {
                return Result<ChargeSheetDto>.Failure(
                    "INVALID_CHARGE_ITEMS",
                    "La categoría del concepto debe ser CONSULTA, ESTUDIO o INSUMO.",
                    400);
            }

            drafts.Add(new ChargeLineItemDraft(
                input.ConceptCode,
                input.Description,
                category,
                input.Quantity,
                input.UnitPriceMxn));
        }

        var itemsCheck = BillingPolicy.ValidateChargeLineItems(drafts);
        if (!itemsCheck.IsSuccess)
        {
            return Result<ChargeSheetDto>.Failure(itemsCheck.Error!);
        }

        var existing = await sheetRepository.GetByEncounterIdAsync(
            tenantId,
            encounterId,
            includeDetails: true,
            cancellationToken);

        var editCheck = BillingPolicy.ValidateChargeSheetCanBeEdited(existing);
        if (!editCheck.IsSuccess)
        {
            return Result<ChargeSheetDto>.Failure(editCheck.Error!);
        }

        var nowUtc = clock.UtcNow;

        if (existing is null)
        {
            var created = EncounterChargeSheet.Create(
                Guid.NewGuid(),
                tenantId,
                encounter.EncounterId,
                encounter.RecordId,
                encounter.PatientId,
                encounter.SpecialistId,
                drafts,
                actor.FullName,
                nowUtc);

            await sheetRepository.AddAsync(created, cancellationToken);
            return Result<ChargeSheetDto>.Success(ChargeSheetMapper.ToDto(created, includeAuditLog: true));
        }

        var (addedItems, auditEvent) = existing.ReplaceItems(drafts, actor.FullName, nowUtc);
        await sheetRepository.ReplaceItemsAsync(existing, addedItems, auditEvent, cancellationToken);
        return Result<ChargeSheetDto>.Success(ChargeSheetMapper.ToDto(existing, includeAuditLog: true));
    }
}
