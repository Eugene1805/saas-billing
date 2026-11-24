using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Policies;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.ChargeSheets;

public sealed class ReopenChargeSheetUseCase(
    ITenantBillingAccessPort tenantPort,
    IBillingAccessControlPort accessPort,
    IChargeSheetRepository sheetRepository,
    IClockPort clock)
{
    public async Task<Result<ChargeSheetDto>> ExecuteAsync(
        BillingActorContext actor,
        Guid chargeSheetId,
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

        var sheet = await sheetRepository.GetByIdAsync(
            tenantCheck.Value.TenantId,
            chargeSheetId,
            includeDetails: true,
            cancellationToken);
        if (sheet is null)
        {
            return Result<ChargeSheetDto>.Failure(
                "CHARGE_SHEET_NOT_FOUND",
                "Hoja de cargos no encontrada dentro de esta clínica.",
                404);
        }

        var accessCheck = await BillingUseCaseGuards.CheckPatientBillingAccessAsync(
            actor,
            tenantCheck.Value.TenantId,
            sheet.PatientId,
            sheet.SpecialistId,
            allowEncounterSpecialist: true,
            BillingAccessOperation.MANAGE_BILLING,
            accessPort,
            cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return Result<ChargeSheetDto>.Failure(accessCheck.Error!);
        }

        var policyCheck = BillingPolicy.ValidateChargeSheetCanBeReopened(sheet);
        if (!policyCheck.IsSuccess)
        {
            return Result<ChargeSheetDto>.Failure(policyCheck.Error!);
        }

        var audit = sheet.Reopen(actor.FullName, clock.UtcNow);
        await sheetRepository.UpdateWithAuditAsync(sheet, audit, cancellationToken);
        return Result<ChargeSheetDto>.Success(ChargeSheetMapper.ToDto(sheet, includeAuditLog: true));
    }
}
