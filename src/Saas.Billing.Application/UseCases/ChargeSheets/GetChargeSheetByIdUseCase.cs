using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.ChargeSheets;

public sealed class GetChargeSheetByIdUseCase(
    ITenantBillingAccessPort tenantPort,
    IBillingAccessControlPort accessPort,
    IChargeSheetRepository sheetRepository)
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
            BillingAccessOperation.READ_PATIENT_BILLING,
            accessPort,
            cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return Result<ChargeSheetDto>.Failure(accessCheck.Error!);
        }

        return Result<ChargeSheetDto>.Success(ChargeSheetMapper.ToDto(sheet, includeAuditLog: true));
    }
}
