using System.Net.Sockets;
using Npgsql;
using Saas.Billing.Application.Ports;
using Saas.Billing.Infrastructure.Persistence.Tenancy;

namespace Saas.Billing.Infrastructure.Persistence.Repositories;

internal static class TenantPersistenceHelper
{
    public static async Task<T> ExecuteAsync<T>(
        TenantBillingDbContextAccessor accessor,
        Guid tenantId,
        Func<BillingDbContext, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dbContext = await accessor.GetDbContextAsync(tenantId, cancellationToken);
            return await operation(dbContext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException &&
                                   ex is not TenantDatabaseUnavailableException &&
                                   IsConnectivityFailure(ex))
        {
            throw new TenantDatabaseUnavailableException(
                tenantId,
                "TENANT_DATABASE_UNAVAILABLE",
                $"Error de conectividad al consultar la base de datos del tenant {tenantId}.",
                ex);
        }
    }

    public static async Task ExecuteAsync(
        TenantBillingDbContextAccessor accessor,
        Guid tenantId,
        Func<BillingDbContext, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            accessor,
            tenantId,
            async dbContext =>
            {
                await operation(dbContext);
                return true;
            },
            cancellationToken);
    }

    public static bool IsConnectivityFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException or SocketException)
            {
                return true;
            }

            if (current is PostgresException postgresException)
            {
                if (postgresException.IsTransient ||
                    postgresException.SqlState.StartsWith("08", StringComparison.Ordinal) ||
                    postgresException.SqlState.StartsWith("57", StringComparison.Ordinal) ||
                    postgresException.SqlState.StartsWith("53", StringComparison.Ordinal))
                {
                    return true;
                }

                return false;
            }

            if (current is NpgsqlException)
            {
                return true;
            }
        }

        return false;
    }
}
