using Saas.Billing.Application.Ports;

namespace Saas.Billing.Infrastructure.Persistence.Tenancy;

internal sealed class TenantBillingDbContextAccessor(
    ITenantDatabaseMigratorPort databaseMigrator,
    ITenantBillingDbContextFactory dbContextFactory) : IAsyncDisposable, IDisposable
{
    private readonly Dictionary<Guid, BillingDbContext> _contexts = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<BillingDbContext> GetDbContextAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_contexts.TryGetValue(tenantId, out var existing))
            {
                return existing;
            }

            await databaseMigrator.EnsureMigratedAsync(tenantId, cancellationToken);
            var created = dbContextFactory.CreateDbContext(tenantId);
            _contexts[tenantId] = created;
            return created;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        foreach (var dbContext in _contexts.Values)
        {
            dbContext.Dispose();
        }

        _contexts.Clear();
        _gate.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var dbContext in _contexts.Values)
        {
            await dbContext.DisposeAsync();
        }

        _contexts.Clear();
        _gate.Dispose();
    }
}
