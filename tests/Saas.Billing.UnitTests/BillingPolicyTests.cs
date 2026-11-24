using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.Policies;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.UnitTests;

public sealed class BillingPolicyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SpecialistId = Guid.NewGuid();
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid EncounterId = Guid.NewGuid();
    private static readonly Guid RecordId = Guid.NewGuid();
    private static readonly DateTimeOffset NowUtc = new(2026, 11, 20, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidateFullPayment_RequiresFinalizedEncounterReviewedSheetAndExactAmount()
    {
        var sheet = EncounterChargeSheet.Create(
            Guid.NewGuid(),
            TenantId,
            EncounterId,
            RecordId,
            PatientId,
            SpecialistId,
            [new("CON-ESP", "Consulta", ChargeConceptCategory.CONSULTA, 1, 1200m)],
            "Enf. Clara",
            NowUtc);

        var encInProgress = new EncounterSummary(
            EncounterId, TenantId, RecordId, PatientId, SpecialistId, EncounterStatus.EN_CURSO, null);
        var encFinalized = new EncounterSummary(
            EncounterId, TenantId, RecordId, PatientId, SpecialistId, EncounterStatus.FINALIZADA, NowUtc);

        var r1 = BillingPolicy.ValidateFullPayment(encInProgress, sheet, 1200m, PaymentMethod.EFECTIVO);
        Assert.Equal("ENCOUNTER_NOT_FINALIZED", r1.Error!.Code);

        var r2 = BillingPolicy.ValidateFullPayment(encFinalized, sheet, 1200m, PaymentMethod.EFECTIVO);
        Assert.Equal("CHARGE_SHEET_NOT_REVIEWED", r2.Error!.Code);

        sheet.MarkReviewed("Enf. Clara", NowUtc);

        var r3 = BillingPolicy.ValidateFullPayment(encFinalized, sheet, 600m, PaymentMethod.TARJETA);
        Assert.Equal("PARTIAL_PAYMENT_NOT_ALLOWED", r3.Error!.Code);

        var ok = BillingPolicy.ValidateFullPayment(encFinalized, sheet, 1200m, PaymentMethod.TARJETA);
        Assert.True(ok.IsSuccess);

        sheet.MarkPaid("COB-2026-0001", PaymentMethod.TARJETA, "Enf. Clara", NowUtc);
        var reopenBlocked = BillingPolicy.ValidateChargeSheetCanBeReopened(sheet);
        Assert.Equal("CHARGE_SHEET_ALREADY_PAID", reopenBlocked.Error!.Code);
    }

    [Fact]
    public void ValidateCutRules_PreventsDoubleInclusionAndRequiresObservationNoteAndAdjustmentReason()
    {
        var payment = PatientPayment.Create(
            Guid.NewGuid(),
            "COB-2026-0001",
            TenantId,
            Guid.NewGuid(),
            EncounterId,
            PatientId,
            SpecialistId,
            1200m,
            PaymentMethod.EFECTIVO,
            new DateTimeOffset(2026, 11, 15, 18, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            "Enf. Clara",
            cutId: Guid.NewGuid());

        var doubleInclusion = BillingPolicy.ValidateCutPreparation(
            TenantId,
            SpecialistId,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            [payment]);
        Assert.Equal("PAYMENT_ALREADY_IN_CUT", doubleInclusion.Error!.Code);

        payment.ReleaseFromCut();
        var cut = SpecialistCut.Create(
            Guid.NewGuid(),
            "CORTE-2026-0001",
            TenantId,
            SpecialistId,
            "Dr. Salazar",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            [payment],
            Guid.NewGuid(),
            "Enf. Clara",
            NowUtc);

        var missingComment = BillingPolicy.ValidateCutResolution(cut, SpecialistId, isObservation: true, "   ");
        Assert.Equal("OBSERVATION_COMMENT_REQUIRED", missingComment.Error!.Code);

        cut.MarkObserved("Dr. Salazar", "Falta referencia", NowUtc);
        var missingNote = BillingPolicy.ValidateCutRevision(
            cut,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            [payment],
            "");
        Assert.Equal("ADJUSTMENT_NOTE_REQUIRED", missingNote.Error!.Code);

        cut.MarkConfirmed("Dr. Salazar", null, NowUtc);
        var confirmedEdit = BillingPolicy.ValidateCutRevision(
            cut,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            [payment],
            "Intento tardío");
        Assert.Equal("CUT_ALREADY_CONFIRMED", confirmedEdit.Error!.Code);

        var zeroAdjustment = BillingPolicy.ValidateAdjustment(0m, "Motivo");
        Assert.Equal("INVALID_ADJUSTMENT_AMOUNT", zeroAdjustment.Error!.Code);
    }
}
