namespace Saas.Billing.Domain.ValueObjects;

public enum BillingAccessOperation
{
    READ_PATIENT_BILLING = 0,
    MANAGE_BILLING = 1,
}

public sealed record AccessControlCheckResult(
    bool Allowed,
    bool DependencyUnavailable,
    bool PatientNotFound,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AccessControlCheckResult Allow() =>
        new(true, false, false, null, null);

    public static AccessControlCheckResult Denied(string code, string message) =>
        new(false, false, false, code, message);

    public static AccessControlCheckResult MissingPatient() =>
        new(false, false, true, "PATIENT_NOT_FOUND", "Paciente no encontrado dentro de esta clínica.");

    public static AccessControlCheckResult Unavailable() =>
        new(false, true, false, "ACCESS_CONTROL_UNAVAILABLE", "El servicio de control de acceso CU-10 no está disponible.");
}

public sealed record AuthorizedPatientsScopeResult(
    bool AllTenantPatientsAllowed,
    bool DependencyUnavailable,
    IReadOnlySet<Guid> AllowedPatientIds)
{
    public static AuthorizedPatientsScopeResult All() =>
        new(true, false, new HashSet<Guid>());

    public static AuthorizedPatientsScopeResult Restricted(IReadOnlySet<Guid> ids) =>
        new(false, false, ids);

    public static AuthorizedPatientsScopeResult Unavailable() =>
        new(false, true, new HashSet<Guid>());
}
