using System.Globalization;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.Policies;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Payments;

public sealed class RecordFullPaymentUseCase(
    ITenantBillingAccessPort tenantPort,
    IBillingAccessControlPort accessPort,
    IClinicalEncounterGatewayPort clinicalPort,
    IChargeSheetRepository sheetRepository,
    IPaymentRepository paymentRepository,
    IClockPort clock)
{
    public async Task<Result<PatientPaymentDto>> ExecuteAsync(
        BillingActorContext actor,
        RecordPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await BillingUseCaseGuards.EnsureTenantBillingEnabledAsync(
            actor,
            tenantPort,
            cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<PatientPaymentDto>.Failure(tenantCheck.Error);
        }

        if (string.IsNullOrWhiteSpace(command.Method) ||
            int.TryParse(command.Method.Trim(), out _) ||
            !Enum.TryParse<PaymentMethod>(command.Method.Trim(), ignoreCase: true, out var method) ||
            !Enum.IsDefined(method))
        {
            return Result<PatientPaymentDto>.Failure(
                "INVALID_PAYMENT_METHOD",
                "Selecciona un método válido: EFECTIVO, TARJETA o TRANSFERENCIA.",
                400);
        }

        var tenantId = tenantCheck.Value.TenantId;

        var sheet = await sheetRepository.GetByIdAsync(
            tenantId,
            command.ChargeSheetId,
            includeDetails: true,
            cancellationToken);
        if (sheet is null)
        {
            return Result<PatientPaymentDto>.Failure(
                "CHARGE_SHEET_MISSING",
                "Hoja de cargos no encontrada dentro de esta clínica.",
                404);
        }

        var accessCheck = await BillingUseCaseGuards.CheckPatientBillingAccessAsync(
            actor,
            tenantId,
            sheet.PatientId,
            sheet.SpecialistId,
            allowEncounterSpecialist: false,
            BillingAccessOperation.MANAGE_BILLING,
            accessPort,
            cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return Result<PatientPaymentDto>.Failure(accessCheck.Error!);
        }

        var encounterLookup = await clinicalPort.GetEncounterAsync(
            tenantId,
            sheet.EncounterId,
            cancellationToken);
        if (encounterLookup.DependencyUnavailable)
        {
            return Result<PatientPaymentDto>.Failure(
                "CLINICAL_UNAVAILABLE",
                "El servicio Clinical no está disponible para verificar el estado de la atención.",
                503);
        }

        if (!encounterLookup.Found || encounterLookup.Encounter is null)
        {
            return Result<PatientPaymentDto>.Failure(
                "ENCOUNTER_NOT_FOUND",
                "La atención asociada a esta hoja de cargos no fue encontrada.",
                404);
        }

        var validation = BillingPolicy.ValidateFullPayment(
            encounterLookup.Encounter,
            sheet,
            command.AmountMxn,
            method);
        if (!validation.IsSuccess)
        {
            return Result<PatientPaymentDto>.Failure(validation.Error!);
        }

        var nowUtc = clock.UtcNow;
        var gmt6Year = Gmt6Clock.ToDateOnlyGmt6(nowUtc).Year;
        var nextSequence = await paymentRepository.CountByTenantAsync(tenantId, cancellationToken) + 1;
        var folio = string.Format(CultureInfo.InvariantCulture, "COB-{0}-{1:D4}", gmt6Year, nextSequence);

        var payment = PatientPayment.Create(
            Guid.NewGuid(),
            folio,
            tenantId,
            sheet.Id,
            sheet.EncounterId,
            sheet.PatientId,
            sheet.SpecialistId,
            sheet.TotalMxn,
            method,
            nowUtc,
            actor.AccountId,
            actor.FullName);

        var paidAudit = sheet.MarkPaid(folio, method, actor.FullName, nowUtc);

        var writeOutcome = await paymentRepository.RecordFullPaymentAsync(
            payment,
            sheet,
            paidAudit,
            cancellationToken);

        if (writeOutcome == PaymentWriteOutcome.ChargeSheetAlreadyPaid)
        {
            return Result<PatientPaymentDto>.Failure(
                "CHARGE_SHEET_ALREADY_PAID",
                "Esta hoja de cargos ya fue COBRADA concurrentemente (CU-21 A5).",
                409);
        }

        return Result<PatientPaymentDto>.Success(PaymentMapper.ToDto(payment));
    }
}
