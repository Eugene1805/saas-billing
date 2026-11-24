using Microsoft.Extensions.Options;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence;

namespace Saas.Billing.Infrastructure.AccessControl;

public sealed class AccessControlOptions
{
    public const string SectionName = "AccessControl";

    public string Mode { get; init; } = "Local";
    public bool SimulateUnavailable { get; init; }
    public List<PatientBillingScopeSeedEntry> Patients { get; init; } = [];
    public List<AuxiliaryBillingLinkSeedEntry> AuxiliaryLinks { get; init; } = [];
}

public sealed class PatientBillingScopeSeedEntry
{
    public Guid PatientId { get; init; }
    public Guid TenantId { get; init; }
    public Guid? PrimarySpecialistId { get; init; }
    public List<Guid> TreatingSpecialistIds { get; init; } = [];
    public List<Guid> ExceptionalManageBillingAccountIds { get; init; } = [];
}

public sealed class AuxiliaryBillingLinkSeedEntry
{
    public Guid AuxiliaryAccountId { get; init; }
    public Guid TenantId { get; init; }
    public List<Guid> LinkedSpecialistIds { get; init; } = [];
}

internal sealed class LocalBillingAccessControlAdapter(
    IOptions<AccessControlOptions> optionsAccessor) : IBillingAccessControlPort
{
    private static readonly IReadOnlyList<PatientBillingScopeSeedEntry> DefaultPatients =
    [
        new()
        {
            PatientId = BillingDbInitializer.Pat0101Id,
            TenantId = BillingDbInitializer.NorteTenantId,
            PrimarySpecialistId = BillingDbInitializer.MendozaNorteAccountId,
            TreatingSpecialistIds = [BillingDbInitializer.SalazarNorteAccountId],
            ExceptionalManageBillingAccountIds = [],
        },
        new()
        {
            PatientId = BillingDbInitializer.Pat0102Id,
            TenantId = BillingDbInitializer.NorteTenantId,
            PrimarySpecialistId = BillingDbInitializer.SalazarNorteAccountId,
            TreatingSpecialistIds = [],
            ExceptionalManageBillingAccountIds = [],
        },
        new()
        {
            PatientId = BillingDbInitializer.Pat0103Id,
            TenantId = BillingDbInitializer.NorteTenantId,
            PrimarySpecialistId = null,
            TreatingSpecialistIds = [],
            ExceptionalManageBillingAccountIds = [],
        },
        new()
        {
            PatientId = BillingDbInitializer.Pat0104Id,
            TenantId = BillingDbInitializer.NorteTenantId,
            PrimarySpecialistId = BillingDbInitializer.SalazarNorteAccountId,
            TreatingSpecialistIds = [],
            ExceptionalManageBillingAccountIds = [BillingDbInitializer.AuxRamirezNorteAccountId],
        },
        new()
        {
            PatientId = BillingDbInitializer.Pat0201Id,
            TenantId = BillingDbInitializer.ValleTenantId,
            PrimarySpecialistId = BillingDbInitializer.MendozaValleAccountId,
            TreatingSpecialistIds = [],
            ExceptionalManageBillingAccountIds = [],
        },
    ];

    private static readonly IReadOnlyList<AuxiliaryBillingLinkSeedEntry> DefaultAuxiliaryLinks =
    [
        new()
        {
            AuxiliaryAccountId = BillingDbInitializer.AuxRamirezNorteAccountId,
            TenantId = BillingDbInitializer.NorteTenantId,
            LinkedSpecialistIds = [BillingDbInitializer.MendozaNorteAccountId],
        },
    ];

    public Task<AccessControlCheckResult> EvaluatePatientAccessAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        Guid patientId,
        BillingAccessOperation operation,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(AccessControlCheckResult.Unavailable());
        }

        var patients = opts.Patients.Count > 0 ? opts.Patients : DefaultPatients;
        var patient = patients.FirstOrDefault(p => p.TenantId == tenantId && p.PatientId == patientId);
        if (patient is null)
        {
            return Task.FromResult(AccessControlCheckResult.MissingPatient());
        }

        if (actorRole == OrgRole.ADMIN_CLINICA)
        {
            return Task.FromResult(AccessControlCheckResult.Allow());
        }

        if (actorRole == OrgRole.AUXILIAR)
        {
            var linked = ResolveLinkedSpecialists(opts, tenantId, actorAccountId);
            if (IsPatientAuthorizedForAuxiliary(patient, actorAccountId, linked))
            {
                return Task.FromResult(AccessControlCheckResult.Allow());
            }

            return Task.FromResult(AccessControlCheckResult.Denied(
                "OUT_OF_BILLING_SCOPE",
                "No tienes ámbito ordinario ni permiso excepcional vigente (MANAGE_BILLING) sobre este paciente (CU-20 A3)."));
        }

        return Task.FromResult(AccessControlCheckResult.Denied(
            "FORBIDDEN_BILLING_ROLE",
            "Rol no autorizado para operar cargos de pacientes."));
    }

    public Task<AuthorizedPatientsScopeResult> GetAuthorizedPatientIdsAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.Unavailable());
        }

        if (actorRole == OrgRole.ADMIN_CLINICA)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.All());
        }

        var patients = (opts.Patients.Count > 0 ? opts.Patients : DefaultPatients)
            .Where(p => p.TenantId == tenantId);

        if (actorRole == OrgRole.AUXILIAR)
        {
            var linked = ResolveLinkedSpecialists(opts, tenantId, actorAccountId);
            var allowed = patients
                .Where(p => IsPatientAuthorizedForAuxiliary(p, actorAccountId, linked))
                .Select(p => p.PatientId)
                .ToHashSet();

            return Task.FromResult(AuthorizedPatientsScopeResult.Restricted(allowed));
        }

        return Task.FromResult(AuthorizedPatientsScopeResult.Restricted(new HashSet<Guid>()));
    }

    private static HashSet<Guid> ResolveLinkedSpecialists(
        AccessControlOptions options,
        Guid tenantId,
        Guid auxiliaryAccountId)
    {
        var links = options.AuxiliaryLinks.Count > 0 ? options.AuxiliaryLinks : DefaultAuxiliaryLinks;
        var entry = links.FirstOrDefault(l =>
            l.TenantId == tenantId && l.AuxiliaryAccountId == auxiliaryAccountId);
        return entry is null ? [] : entry.LinkedSpecialistIds.ToHashSet();
    }

    private static bool IsPatientAuthorizedForAuxiliary(
        PatientBillingScopeSeedEntry patient,
        Guid auxiliaryAccountId,
        HashSet<Guid> linkedSpecialists)
    {
        if (patient.PrimarySpecialistId is null)
        {
            return true;
        }

        if (patient.PrimarySpecialistId is Guid primary && linkedSpecialists.Contains(primary))
        {
            return true;
        }

        if (patient.TreatingSpecialistIds.Any(linkedSpecialists.Contains))
        {
            return true;
        }

        return patient.ExceptionalManageBillingAccountIds.Contains(auxiliaryAccountId);
    }
}
