using Microsoft.EntityFrameworkCore;

namespace Saas.Billing.Infrastructure.Persistence.Tenancy;

internal sealed class NpgsqlTenantBillingDbContextFactory(TenantDataSourcePool dataSourcePool)
    : ITenantBillingDbContextFactory
{
    public BillingDbContext CreateDbContext(Guid tenantId)
    {
        var (_, dataSource) = dataSourcePool.GetConnection(tenantId);

        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>();
        optionsBuilder.UseNpgsql(
            dataSource,
            npgsql => npgsql
                .MigrationsHistoryTable("__EFMigrationsHistory", "billing")
                .EnableRetryOnFailure());

        return new BillingDbContext(optionsBuilder.Options);
    }
}
