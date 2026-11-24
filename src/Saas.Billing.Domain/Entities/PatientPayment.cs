using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Domain.Entities;

public sealed class PatientPayment
{
    private PatientPayment()
    {
    }

    private PatientPayment(
        Guid id,
        string folio,
        Guid tenantId,
        Guid chargeSheetId,
        Guid encounterId,
        Guid patientId,
        Guid specialistId,
        decimal amountMxn,
        PaymentMethod method,
        DateTimeOffset paidAtUtc,
        Guid collectedByAccountId,
        string collectedByName,
        Guid? cutId)
    {
        Id = id;
        Folio = folio;
        TenantId = tenantId;
        ChargeSheetId = chargeSheetId;
        EncounterId = encounterId;
        PatientId = patientId;
        SpecialistId = specialistId;
        AmountMxn = MoneyHelper.RoundMxn(amountMxn);
        Method = method;
        PaidAtUtc = paidAtUtc.ToUniversalTime();
        CollectedByAccountId = collectedByAccountId;
        CollectedByName = collectedByName;
        CutId = cutId;
    }

    public Guid Id { get; private set; }
    public string Folio { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public Guid ChargeSheetId { get; private set; }
    public Guid EncounterId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid SpecialistId { get; private set; }
    public decimal AmountMxn { get; private set; }
    public PaymentMethod Method { get; private set; }
    public DateTimeOffset PaidAtUtc { get; private set; }
    public Guid CollectedByAccountId { get; private set; }
    public string CollectedByName { get; private set; } = string.Empty;
    public Guid? CutId { get; private set; }

    public static PatientPayment Create(
        Guid id,
        string folio,
        Guid tenantId,
        Guid chargeSheetId,
        Guid encounterId,
        Guid patientId,
        Guid specialistId,
        decimal amountMxn,
        PaymentMethod method,
        DateTimeOffset paidAtUtc,
        Guid collectedByAccountId,
        string collectedByName,
        Guid? cutId = null)
    {
        return new PatientPayment(
            id,
            folio.Trim(),
            tenantId,
            chargeSheetId,
            encounterId,
            patientId,
            specialistId,
            amountMxn,
            method,
            paidAtUtc,
            collectedByAccountId,
            collectedByName.Trim(),
            cutId);
    }

    public void AssignToCut(Guid cutId)
    {
        CutId = cutId;
    }

    public void ReleaseFromCut()
    {
        CutId = null;
    }
}
