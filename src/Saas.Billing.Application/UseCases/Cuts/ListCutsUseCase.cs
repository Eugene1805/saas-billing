using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Cuts;

public sealed class ListCutsUseCase(
    ITenantBillingAccessPort tenantPort,
    ISpecialistCutRepository cutRepository)
{
    public async Task<Result<SpecialistCutListResponse>> ExecuteAsync(
        BillingActorContext actor,
        Guid? specialistId,
        string? statusRaw,
        string? fromDateGmt6,
        string? toDateGmt6,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await BillingUseCaseGuards.EnsureTenantBillingEnabledAsync(
            actor,
            tenantPort,
            cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<SpecialistCutListResponse>.Failure(tenantCheck.Error);
        }

        SpecialistCutStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(statusRaw))
        {
            if (int.TryParse(statusRaw.Trim(), out _) ||
                !Enum.TryParse<SpecialistCutStatus>(statusRaw.Trim(), ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Result<SpecialistCutListResponse>.Failure(
                    "INVALID_CUT_STATUS",
                    "El estado del corte debe ser PREPARADO, OBSERVADO o CONFIRMADO.",
                    400);
            }

            statusFilter = parsed;
        }

        DateOnly? fromDate = null;
        DateOnly? toDate = null;

        if (!string.IsNullOrWhiteSpace(fromDateGmt6))
        {
            if (!Gmt6Clock.TryParseGmt6Date(fromDateGmt6, out var parsedFrom))
            {
                return Result<SpecialistCutListResponse>.Failure(
                    "INVALID_CUT_PERIOD",
                    "fromDateGmt6 debe tener formato yyyy-MM-dd.",
                    400);
            }

            fromDate = parsedFrom;
        }

        if (!string.IsNullOrWhiteSpace(toDateGmt6))
        {
            if (!Gmt6Clock.TryParseGmt6Date(toDateGmt6, out var parsedTo))
            {
                return Result<SpecialistCutListResponse>.Failure(
                    "INVALID_CUT_PERIOD",
                    "toDateGmt6 debe tener formato yyyy-MM-dd.",
                    400);
            }

            toDate = parsedTo;
        }

        if (fromDate is DateOnly f && toDate is DateOnly t && f > t)
        {
            return Result<SpecialistCutListResponse>.Failure(
                "INVALID_CUT_PERIOD",
                "fromDateGmt6 no puede ser mayor que toDateGmt6.",
                400);
        }

        var effectiveSpecialistFilter = actor.Role == OrgRole.ESPECIALISTA
            ? actor.AccountId
            : specialistId;

        var cuts = await cutRepository.ListAsync(
            new CutQueryFilter(
                tenantCheck.Value.TenantId,
                effectiveSpecialistFilter,
                statusFilter,
                fromDate,
                toDate),
            cancellationToken);

        var items = cuts.Select(CutMapper.ToDto).ToList();
        return Result<SpecialistCutListResponse>.Success(new SpecialistCutListResponse(items.Count, items));
    }
}
