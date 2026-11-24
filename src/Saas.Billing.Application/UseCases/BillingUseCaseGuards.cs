using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases;

internal static class BillingUseCaseGuards
{
    public static async Task<Result<TenantBillingState>> EnsureTenantBillingEnabledAsync(
        BillingActorContext actor,
        ITenantBillingAccessPort tenantPort,
        CancellationToken cancellationToken)
    {
        var check = await CheckTenantStateAsync(actor, tenantPort, cancellationToken);
        if (!check.IsSuccess)
        {
            return check;
        }

        if (!check.Value.UsesBilling)
        {
            return Result<TenantBillingState>.Failure(
                "BILLING_DISABLED",
                "Esta clínica no utiliza Billing de pacientes; la atención clínica sigue operando con normalidad (CU-20 A1).",
                403);
        }

        return check;
    }

    public static async Task<Result<TenantBillingState>> CheckTenantStateAsync(
        BillingActorContext actor,
        ITenantBillingAccessPort tenantPort,
        CancellationToken cancellationToken)
    {
        if (actor.Role == OrgRole.SUPER_ADMIN)
        {
            return Result<TenantBillingState>.Failure(
                "FORBIDDEN_BILLING_ROLE",
                "SuperAdmin no opera cargos, cobros ni cortes clínicos de tenants (CU-10).",
                403);
        }

        if (actor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
        {
            return Result<TenantBillingState>.Failure(
                "TENANT_CONTEXT_REQUIRED",
                "La operación requiere una clínica activa en el token JWT.",
                403);
        }

        var lookup = await tenantPort.GetByTenantIdAsync(tenantId, cancellationToken);
        if (lookup.DependencyUnavailable)
        {
            return Result<TenantBillingState>.Failure(
                "PLATFORM_UNAVAILABLE",
                "No fue posible validar el estado operativo y configuración Billing de la clínica.",
                503);
        }

        if (!lookup.Found || lookup.Tenant is null)
        {
            return Result<TenantBillingState>.Failure(
                "TENANT_NOT_FOUND",
                "La clínica solicitada no existe en Platform.",
                404);
        }

        if (lookup.Tenant.State == TenantOperationalState.SUSPENDIDA)
        {
            return Result<TenantBillingState>.Failure(
                "TENANT_SUSPENDED",
                "La clínica se encuentra SUSPENDIDA y bloqueada para operaciones de Billing (CU-15).",
                403);
        }

        if (lookup.Tenant.State == TenantOperationalState.PENDIENTE_ADMIN)
        {
            return Result<TenantBillingState>.Failure(
                "TENANT_PENDING_ADMIN",
                "La clínica aún está en estado PENDIENTE_ADMIN.",
                403);
        }

        if (!lookup.Tenant.MayOperate)
        {
            return Result<TenantBillingState>.Failure(
                "TENANT_INACTIVE",
                "La clínica no está habilitada para operar.",
                403);
        }

        return Result<TenantBillingState>.Success(lookup.Tenant);
    }

    public static async Task<Result> CheckPatientBillingAccessAsync(
        BillingActorContext actor,
        Guid tenantId,
        Guid patientId,
        Guid encounterSpecialistId,
        bool allowEncounterSpecialist,
        BillingAccessOperation operation,
        IBillingAccessControlPort accessPort,
        CancellationToken cancellationToken)
    {
        if (actor.Role == OrgRole.ADMIN_CLINICA)
        {
            return Result.Success();
        }

        if (actor.Role == OrgRole.ESPECIALISTA)
        {
            if (!allowEncounterSpecialist)
            {
                return Result.Failure(
                    "FORBIDDEN_COLLECTION_ROLE",
                    "El registro de cobros y cortes corresponde a personal administrativo o auxiliar autorizado (CU-21 / CU-22).",
                    403);
            }

            if (encounterSpecialistId != actor.AccountId)
            {
                return Result.Failure(
                    "FORBIDDEN_OTHER_SPECIALIST_BILLING",
                    "Solo el especialista titular de esta atención puede revisar o ajustar sus cargos.",
                    403);
            }

            return Result.Success();
        }

        if (actor.Role == OrgRole.AUXILIAR)
        {
            var access = await accessPort.EvaluatePatientAccessAsync(
                actor.AccountId,
                tenantId,
                actor.Role,
                patientId,
                operation,
                cancellationToken);

            if (access.DependencyUnavailable)
            {
                return Result.Failure(
                    access.ErrorCode ?? "ACCESS_CONTROL_UNAVAILABLE",
                    access.ErrorMessage ?? "Control de acceso CU-10 no disponible.",
                    503);
            }

            if (access.PatientNotFound)
            {
                return Result.Failure(
                    access.ErrorCode ?? "PATIENT_NOT_FOUND",
                    access.ErrorMessage ?? "Paciente no encontrado en esta clínica.",
                    404);
            }

            if (!access.Allowed)
            {
                return Result.Failure(
                    access.ErrorCode ?? "OUT_OF_BILLING_SCOPE",
                    access.ErrorMessage ?? "Sin ámbito ordinario ni permiso excepcional MANAGE_BILLING sobre este paciente (CU-20 A3).",
                    403);
            }

            return Result.Success();
        }

        return Result.Failure(
            "FORBIDDEN_BILLING_ROLE",
            "Rol no autorizado para operar Billing.",
            403);
    }
}
