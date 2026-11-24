namespace Saas.Billing.Domain.ValueObjects;

public sealed record BillableConcept(
    string Code,
    string Label,
    ChargeConceptCategory Category,
    decimal DefaultPriceMxn)
{
    public static IReadOnlyList<BillableConcept> DefaultCatalog { get; } =
    [
        new("CON-GEN", "Consulta médica general", ChargeConceptCategory.CONSULTA, 800m),
        new("CON-ESP", "Consulta de especialidad", ChargeConceptCategory.CONSULTA, 1200m),
        new("EST-ECG", "Electrocardiograma en consultorio", ChargeConceptCategory.ESTUDIO, 650m),
        new("EST-LAB", "Toma de muestra básica", ChargeConceptCategory.ESTUDIO, 450m),
        new("INS-MAT", "Material de curación estándar", ChargeConceptCategory.INSUMO, 250m),
        new("INS-MED", "Insumo aplicado en consulta", ChargeConceptCategory.INSUMO, 350m),
    ];
}
