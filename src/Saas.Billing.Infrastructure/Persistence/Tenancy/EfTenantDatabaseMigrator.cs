using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Saas.Billing.Application.Ports;
using Saas.Billing.Infrastructure.Persistence.Repositories;

namespace Saas.Billing.Infrastructure.Persistence.Tenancy;

internal sealed class EfTenantDatabaseMigrator(
    TenantDataSourcePool dataSourcePool,
    ITenantBillingDbContextFactory tenantDbContextFactory)
    : ITenantDatabaseMigratorPort
{
    private readonly ConcurrentDictionary<Guid, string> _migratedConnections = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _tenantLocks = new();

    public async Task EnsureMigratedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var (connectionString, dataSource) = dataSourcePool.GetConnection(tenantId);
        if (_migratedConnections.TryGetValue(tenantId, out var migratedCs) &&
            string.Equals(migratedCs, connectionString, StringComparison.Ordinal))
        {
            return;
        }

        var semaphore = _tenantLocks.GetOrAdd(tenantId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_migratedConnections.TryGetValue(tenantId, out migratedCs) &&
                string.Equals(migratedCs, connectionString, StringComparison.Ordinal))
            {
                return;
            }

            var lockKey = ComputeAdvisoryLockKey(tenantId, salt: 0x42494C4C_4D494752); // BILLMIGR
            await using var lockConn = await dataSource.OpenConnectionAsync(cancellationToken);

            await using (var lockCmd = lockConn.CreateCommand())
            {
                lockCmd.CommandText = "SELECT pg_advisory_lock(@key);";
                lockCmd.Parameters.AddWithValue("key", lockKey);
                await lockCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            try
            {
                await using var dbContext = tenantDbContextFactory.CreateDbContext(tenantId);
                await dbContext.Database.MigrateAsync(cancellationToken);
                _migratedConnections[tenantId] = connectionString;
            }
            finally
            {
                try
                {
                    await using var unlockCmd = lockConn.CreateCommand();
                    unlockCmd.CommandText = "SELECT pg_advisory_unlock(@key);";
                    unlockCmd.Parameters.AddWithValue("key", lockKey);
                    await unlockCmd.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch
                {
                    // La conexión se cerrará y liberará el advisory lock automáticamente.
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException &&
                                   ex is not TenantDatabaseUnavailableException &&
                                   TenantPersistenceHelper.IsConnectivityFailure(ex))
        {
            throw new TenantDatabaseUnavailableException(
                tenantId,
                "TENANT_DATABASE_UNAVAILABLE",
                $"Error de conectividad al migrar la base de datos del tenant {tenantId}.",
                ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static long ComputeAdvisoryLockKey(Guid tenantId, long salt)
    {
        var bytes = tenantId.ToByteArray();
        var high = BitConverter.ToInt64(bytes, 0);
        var low = BitConverter.ToInt64(bytes, 8);
        return high ^ low ^ salt;
    }
}
