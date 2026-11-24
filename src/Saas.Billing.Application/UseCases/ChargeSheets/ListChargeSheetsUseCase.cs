using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.ChargeSheets;

public sealed class ListChargeSheetsUseCase(
    ITenantBillingAccessPort tenantPort,
    IBillingAccessControlPort accessPort,
    IChargeSheetRepository sheetRepository)
{
    public async Task<Result<ChargeSheetListResponse>> ExecuteAsync(
        BillingActorContext actor,
        Guid? encounterId,
        Guid? patientId,
        Guid? specialistId,
        string? statusRaw,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await BillingUseCaseGuards.EnsureTenantBillingEnabledAsync(
            actor,
            tenantPort,
            cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<ChargeSheetListResponse>.Failure(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value.TenantId;

        ChargeSheetStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(statusRaw))
        {
            if (int.TryParse(statusRaw.Trim(), out _) ||
                !Enum.TryParse<ChargeSheetStatus>(statusRaw.Trim(), ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Result<ChargeSheetListResponse>.Failure(
                    "INVALID_CHARGE_SHEET_STATUS",
                    "El estado debe ser ABIERTA, REVISADA o COBRADA.",
                    400);
            }

            statusFilter = parsed;
        }

        IReadOnlySet<Guid>? patientRestriction = null;
        var effectiveSpecialistFilter = specialistId;

        if (actor.Role == OrgRole.ESPECIALISTA)
        {
            effectiveSpecialistFilter = actor.AccountId;
        }
        else if (actor.Role == OrgRole.AUXILIAR)
        {
            var scope = await accessPort.GetAuthorizedPatientIdsAsync(
                actor.AccountId,
                tenantId,
                actor.Role,
                cancellationToken);
            if (scope.DependencyUnavailable)
            {
                return Result<ChargeSheetListResponse>.Failure(
                    "ACCESS_CONTROL_UNAVAILABLE",
                    "El servicio de control de acceso CU-10 no está disponible.",
                    503);
            }

            if (!scope.AllTenantPatientsAllowed)
            {
                patientRestriction = scope.AllowedPatientIds;
            }
        }

        var sheets = await sheetRepository.ListAsync(
            new ChargeSheetQueryFilter(
                tenantId,
                encounterId,
                patientId,
                effectiveSpecialistFilter,
                statusFilter,
                patientRestriction),
            cancellationToken);

        var items = sheets.Select(s => ChargeSheetMapper.ToDto(s, includeAuditLog: false)).ToList();
        return Result<ChargeSheetListResponse>.Success(new ChargeSheetListResponse(items.Count, items));
    }
}
