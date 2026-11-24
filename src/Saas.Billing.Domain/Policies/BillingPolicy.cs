using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Domain.Policies;

public static class BillingPolicy
{
    public static Result ValidateChargeLineItems(IReadOnlyList<ChargeLineItemDraft> items)
    {
        if (items is null || items.Count == 0)
        {
            return Result.Failure(
                "INVALID_CHARGE_ITEMS",
                "Agrega al menos un concepto cobrable antes de guardar la hoja de cargos (CU-20 A2).",
                400);
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ConceptCode) ||
                string.IsNullOrWhiteSpace(item.Description) ||
                !Enum.IsDefined(item.Category) ||
                item.Quantity <= 0 ||
                item.UnitPriceMxn < 0m)
            {
                return Result.Failure(
                    "INVALID_CHARGE_ITEMS",
                    "Cada concepto requiere código, descripción, categoría, cantidad entera positiva y precio unitario válido (CU-20 A2).",
                    400);
            }
        }

        return Result.Success();
    }

    public static Result ValidateChargeSheetCanBeEdited(EncounterChargeSheet? existingSheet)
    {
        if (existingSheet is null)
        {
            return Result.Success();
        }

        if (existingSheet.Status == ChargeSheetStatus.COBRADA)
        {
            return Result.Failure(
                "CHARGE_SHEET_ALREADY_PAID",
                "La hoja ya se encuentra COBRADA y no puede sobrescribirse directamente (CU-20 A5).",
                409);
        }

        if (existingSheet.Status == ChargeSheetStatus.REVISADA)
        {
            return Result.Failure(
                "CHARGE_SHEET_NOT_OPEN",
                "La hoja está en estado REVISADA; devuélvela a ABIERTA antes de modificar sus conceptos.",
                409);
        }

        return Result.Success();
    }

    public static Result ValidateChargeSheetCanBeReviewed(EncounterChargeSheet sheet)
    {
        if (sheet.Status == ChargeSheetStatus.COBRADA)
        {
            return Result.Failure(
                "CHARGE_SHEET_ALREADY_PAID",
                "La hoja ya se encuentra COBRADA.",
                409);
        }

        if (sheet.Status != ChargeSheetStatus.ABIERTA)
        {
            return Result.Failure(
                "CHARGE_SHEET_NOT_OPEN",
                $"Solo una hoja en estado ABIERTA puede confirmarse como REVISADA (estado actual: {sheet.Status}).",
                409);
        }

        if (sheet.Items.Count == 0 || sheet.TotalMxn <= 0m)
        {
            return Result.Failure(
                "CHARGE_SHEET_EMPTY",
                "Registra al menos un cargo con importe positivo antes de marcar la hoja como REVISADA (CU-20).",
                400);
        }

        return Result.Success();
    }

    public static Result ValidateChargeSheetCanBeReopened(EncounterChargeSheet sheet)
    {
        if (sheet.Status == ChargeSheetStatus.COBRADA)
        {
            return Result.Failure(
                "CHARGE_SHEET_ALREADY_PAID",
                "Una hoja COBRADA no puede devolverse a estado ABIERTA (CU-20 A5).",
                409);
        }

        if (sheet.Status != ChargeSheetStatus.REVISADA)
        {
            return Result.Failure(
                "CHARGE_SHEET_NOT_REVIEWED",
                "La hoja ya se encuentra en estado ABIERTA.",
                409);
        }

        return Result.Success();
    }

    public static Result ValidateFullPayment(
        EncounterSummary encounter,
        EncounterChargeSheet? sheet,
        decimal amountMxn,
        PaymentMethod method)
    {
        if (encounter.Status != EncounterStatus.FINALIZADA)
        {
            return Result.Failure(
                "ENCOUNTER_NOT_FINALIZED",
                "El cobro ordinario exige que la atención clínica esté FINALIZADA; la nota médica puede permanecer en BORRADOR (CU-21 A1 / A3).",
                409);
        }

        if (sheet is null)
        {
            return Result.Failure(
                "CHARGE_SHEET_MISSING",
                "Primero registra y marca como REVISADA la hoja de cargos de esta atención en CU-20.",
                404);
        }

        if (sheet.Status == ChargeSheetStatus.COBRADA)
        {
            return Result.Failure(
                "CHARGE_SHEET_ALREADY_PAID",
                "Esta hoja de cargos ya fue COBRADA (CU-21 A5).",
                409);
        }

        if (sheet.Status != ChargeSheetStatus.REVISADA)
        {
            return Result.Failure(
                "CHARGE_SHEET_NOT_REVIEWED",
                "La hoja de cargos sigue en estado ABIERTA; confírmala como REVISADA antes de cobrar (CU-21 A2).",
                409);
        }

        var roundedInput = MoneyHelper.RoundMxn(amountMxn);
        if (roundedInput <= 0m || Math.Abs(roundedInput - sheet.TotalMxn) > 0.009m)
        {
            return Result.Failure(
                "PARTIAL_PAYMENT_NOT_ALLOWED",
                $"En el MVP solo se registran cobros completos por {MoneyHelper.FormatMxn(sheet.TotalMxn)}; no se admiten pagos parciales (CU-21 A4).",
                400);
        }

        if (!Enum.IsDefined(method))
        {
            return Result.Failure(
                "INVALID_PAYMENT_METHOD",
                "Selecciona un método válido: EFECTIVO, TARJETA o TRANSFERENCIA.",
                400);
        }

        return Result.Success();
    }

    public static Result ValidateCutPreparation(
        Guid tenantId,
        Guid specialistId,
        DateOnly periodStartGmt6,
        DateOnly periodEndGmt6,
        IReadOnlyList<PatientPayment> selectedPayments)
    {
        if (periodStartGmt6 > periodEndGmt6)
        {
            return Result.Failure(
                "INVALID_CUT_PERIOD",
                "Define un rango de fechas válido en GMT-6 (periodStartGmt6 <= periodEndGmt6).",
                400);
        }

        if (selectedPayments.Count == 0)
        {
            return Result.Failure(
                "NO_PAYMENTS_IN_PERIOD",
                "Selecciona al menos un cobro disponible para preparar el corte (CU-22 A2).",
                400);
        }

        foreach (var payment in selectedPayments)
        {
            if (payment.TenantId != tenantId || payment.SpecialistId != specialistId || payment.CutId is not null)
            {
                return Result.Failure(
                    "PAYMENT_ALREADY_IN_CUT",
                    "Uno o más cobros seleccionados ya pertenecen a otro corte activo o a otro especialista (CU-22 A3).",
                    409);
            }

            var paidDate = Gmt6Clock.ToDateOnlyGmt6(payment.PaidAtUtc);
            if (paidDate < periodStartGmt6 || paidDate > periodEndGmt6)
            {
                return Result.Failure(
                    "PAYMENT_OUTSIDE_CUT_PERIOD",
                    $"El cobro {payment.Folio} ({Gmt6Clock.FormatDateGmt6(paidDate)}) queda fuera de la ventana del corte en GMT-6.",
                    400);
            }
        }

        return Result.Success();
    }

    public static Result ValidateCutRevision(
        SpecialistCut cut,
        DateOnly periodStartGmt6,
        DateOnly periodEndGmt6,
        IReadOnlyList<PatientPayment> selectedPayments,
        string? adjustmentNote)
    {
        if (cut.Status == SpecialistCutStatus.CONFIRMADO)
        {
            return Result.Failure(
                "CUT_ALREADY_CONFIRMED",
                "El corte ya fue CONFIRMADO por el especialista y no admite modificación ordinaria (CU-22 A4).",
                409);
        }

        if (periodStartGmt6 > periodEndGmt6)
        {
            return Result.Failure(
                "INVALID_CUT_PERIOD",
                "Rango de periodo GMT-6 inválido.",
                400);
        }

        if (string.IsNullOrWhiteSpace(adjustmentNote))
        {
            return Result.Failure(
                "ADJUSTMENT_NOTE_REQUIRED",
                "Captura la nota trazable de corrección antes de reenviar el corte a PREPARADO (PC-05).",
                400);
        }

        if (selectedPayments.Count == 0)
        {
            return Result.Failure(
                "NO_PAYMENTS_IN_PERIOD",
                "El corte debe conservar al menos un cobro asociado.",
                400);
        }

        foreach (var payment in selectedPayments)
        {
            if (payment.TenantId != cut.TenantId ||
                payment.SpecialistId != cut.SpecialistId ||
                (payment.CutId is Guid assignedCutId && assignedCutId != cut.Id))
            {
                return Result.Failure(
                    "PAYMENT_ALREADY_IN_CUT",
                    "Existe un cobro asignado a otro corte o especialista (CU-22 A3).",
                    409);
            }

            var paidDate = Gmt6Clock.ToDateOnlyGmt6(payment.PaidAtUtc);
            if (paidDate < periodStartGmt6 || paidDate > periodEndGmt6)
            {
                return Result.Failure(
                    "PAYMENT_OUTSIDE_CUT_PERIOD",
                    $"El cobro {payment.Folio} queda fuera del periodo del corte en GMT-6.",
                    400);
            }
        }

        return Result.Success();
    }

    public static Result ValidateCutResolution(
        SpecialistCut cut,
        Guid actorAccountId,
        bool isObservation,
        string? comment)
    {
        if (cut.SpecialistId != actorAccountId)
        {
            return Result.Failure(
                "FORBIDDEN_CUT_OWNER",
                "Este corte pertenece a otro especialista de la clínica (CU-23 A1).",
                403);
        }

        if (cut.Status != SpecialistCutStatus.PREPARADO)
        {
            return Result.Failure(
                "CUT_NOT_PREPARED",
                $"Solo los cortes en estado PREPARADO admiten confirmación u observación (estado actual: {cut.Status}) (CU-23 A2).",
                409);
        }

        if (isObservation && string.IsNullOrWhiteSpace(comment))
        {
            return Result.Failure(
                "OBSERVATION_COMMENT_REQUIRED",
                "El comentario describiendo la discrepancia es obligatorio para marcar el corte como OBSERVADO (CU-23 A3 / PC-05).",
                400);
        }

        return Result.Success();
    }

    public static Result ValidateAdjustment(decimal deltaMxn, string? reason)
    {
        if (MoneyHelper.RoundMxn(deltaMxn) == 0m)
        {
            return Result.Failure(
                "INVALID_ADJUSTMENT_AMOUNT",
                "El ajuste compensatorio requiere un importe distinto de cero (MXN).",
                400);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(
                "ADJUSTMENT_REASON_REQUIRED",
                "Captura el motivo obligatorio del ajuste compensatorio vinculado.",
                400);
        }

        return Result.Success();
    }
}
