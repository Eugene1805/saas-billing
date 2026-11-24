using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.Ports;

public interface IBillingAccessControlPort
{
    Task<AccessControlCheckResult> EvaluatePatientAccessAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        Guid patientId,
        BillingAccessOperation operation,
        CancellationToken cancellationToken = default);

    Task<AuthorizedPatientsScopeResult> GetAuthorizedPatientIdsAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        CancellationToken cancellationToken = default);
}
