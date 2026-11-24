using System.Globalization;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.Policies;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Cuts;

public sealed class PrepareCutUseCase(
    ITenantBillingAccessPort tenantPort,
    IBillingDirectoryPort directoryPort,
    IPaymentRepository paymentRepository,
    ISpecialistCutRepository cutRepository,
    IClockPort clock)
{
    public async Task<Result<SpecialistCutDetailDto>> ExecuteAsync(
        BillingActorContext actor,
        PrepareCutCommand command,
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
                "Solo Secretaría / Enfermería o AdminClinica preparan cortes económicos (CU-22).",
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
        var specLookup = await directoryPort.GetSpecialistAsync(tenantId, command.SpecialistId, cancellationToken);
        if (specLookup.DependencyUnavailable)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "DIRECTORY_UNAVAILABLE",
                "No fue posible consultar el directorio de especialistas.",
                503);
        }

        if (!specLookup.Found || specLookup.Specialist is null || !specLookup.Specialist.Active)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "INVALID_SPECIALIST",
                "Selecciona un especialista activo perteneciente a la clínica (CU-22 A2).",
                400);
        }

        var distinctIds = command.PaymentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "NO_PAYMENTS_IN_PERIOD",
                "Selecciona al menos un cobro disponible para preparar el corte (CU-22 A2).",
                400);
        }

        var payments = await paymentRepository.ListByIdsTrackedAsync(tenantId, distinctIds, cancellationToken);
        if (payments.Count != distinctIds.Count)
        {
            return Result<SpecialistCutDetailDto>.Failure(
                "PAYMENT_NOT_FOUND",
                "Uno o más cobros indicados no existen dentro de esta clínica.",
                404);
        }

        var policyCheck = BillingPolicy.ValidateCutPreparation(
            tenantId,
            command.SpecialistId,
            periodStart,
            periodEnd,
            payments);
        if (!policyCheck.IsSuccess)
        {
            return Result<SpecialistCutDetailDto>.Failure(policyCheck.Error!);
        }

        var nowUtc = clock.UtcNow;
        var gmt6Year = Gmt6Clock.ToDateOnlyGmt6(nowUtc).Year;
        var nextSequence = await cutRepository.CountByTenantAsync(tenantId, cancellationToken) + 1;
        var folio = string.Format(CultureInfo.InvariantCulture, "CORTE-{0}-{1:D4}", gmt6Year, nextSequence);

        var cut = SpecialistCut.Create(
            Guid.NewGuid(),
            folio,
            tenantId,
            specLookup.Specialist.AccountId,
            specLookup.Specialist.FullName,
            periodStart,
            periodEnd,
            payments,
            actor.AccountId,
            actor.FullName,
            nowUtc,
            command.Note);

        await cutRepository.AddAsync(cut, cancellationToken);
        return Result<SpecialistCutDetailDto>.Success(CutMapper.ToDetailDto(cut, payments, []));
    }
}
