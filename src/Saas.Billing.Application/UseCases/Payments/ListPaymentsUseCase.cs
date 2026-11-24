using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Payments;

public sealed class ListPaymentsUseCase(
    ITenantBillingAccessPort tenantPort,
    IBillingAccessControlPort accessPort,
    IPaymentRepository paymentRepository)
{
    public async Task<Result<PaymentListResponse>> ExecuteAsync(
        BillingActorContext actor,
        Guid? specialistId,
        Guid? patientId,
        string? methodRaw,
        bool? unassignedOnly,
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
            return Result<PaymentListResponse>.Failure(tenantCheck.Error);
        }

        PaymentMethod? methodFilter = null;
        if (!string.IsNullOrWhiteSpace(methodRaw))
        {
            if (int.TryParse(methodRaw.Trim(), out _) ||
                !Enum.TryParse<PaymentMethod>(methodRaw.Trim(), ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Result<PaymentListResponse>.Failure(
                    "INVALID_PAYMENT_METHOD",
                    "Método de pago inválido. Usa EFECTIVO, TARJETA o TRANSFERENCIA.",
                    400);
            }

            methodFilter = parsed;
        }

        DateTimeOffset? fromUtc = null;
        DateTimeOffset? toExclusiveUtc = null;

        if (!string.IsNullOrWhiteSpace(fromDateGmt6) || !string.IsNullOrWhiteSpace(toDateGmt6))
        {
            DateOnly fromDate = DateOnly.MinValue;
            DateOnly toDate = DateOnly.MaxValue;

            if (!string.IsNullOrWhiteSpace(fromDateGmt6) &&
                !Gmt6Clock.TryParseGmt6Date(fromDateGmt6, out fromDate))
            {
                return Result<PaymentListResponse>.Failure(
                    "INVALID_DATE_RANGE",
                    "fromDateGmt6 debe tener formato yyyy-MM-dd.",
                    400);
            }

            if (!string.IsNullOrWhiteSpace(toDateGmt6) &&
                !Gmt6Clock.TryParseGmt6Date(toDateGmt6, out toDate))
            {
                return Result<PaymentListResponse>.Failure(
                    "INVALID_DATE_RANGE",
                    "toDateGmt6 debe tener formato yyyy-MM-dd.",
                    400);
            }

            if (fromDate > toDate)
            {
                return Result<PaymentListResponse>.Failure(
                    "INVALID_DATE_RANGE",
                    "fromDateGmt6 no puede ser mayor que toDateGmt6.",
                    400);
            }

            if (!string.IsNullOrWhiteSpace(fromDateGmt6))
            {
                fromUtc = Gmt6Clock.ToUtcDayBounds(fromDate, fromDate).StartUtc;
            }

            if (!string.IsNullOrWhiteSpace(toDateGmt6))
            {
                toExclusiveUtc = Gmt6Clock.ToUtcDayBounds(toDate, toDate).EndExclusiveUtc;
            }
        }

        var tenantId = tenantCheck.Value.TenantId;
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
                return Result<PaymentListResponse>.Failure(
                    "ACCESS_CONTROL_UNAVAILABLE",
                    "El servicio de control de acceso CU-10 no está disponible.",
                    503);
            }

            if (!scope.AllTenantPatientsAllowed)
            {
                patientRestriction = scope.AllowedPatientIds;
            }
        }

        var payments = await paymentRepository.ListAsync(
            new PaymentQueryFilter(
                tenantId,
                effectiveSpecialistFilter,
                patientId,
                methodFilter,
                unassignedOnly,
                fromUtc,
                toExclusiveUtc,
                patientRestriction),
            cancellationToken);

        var items = payments.Select(PaymentMapper.ToDto).ToList();
        return Result<PaymentListResponse>.Success(new PaymentListResponse(items.Count, items));
    }
}
