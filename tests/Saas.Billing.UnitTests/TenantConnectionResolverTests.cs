using Microsoft.Extensions.Options;
using Saas.Billing.Infrastructure.Persistence.Tenancy;

namespace Saas.Billing.UnitTests;

public sealed class TenantConnectionResolverTests
{
    private sealed class StaticOptionsMonitor(TenantDatabasesOptions currentValue)
        : IOptionsMonitor<TenantDatabasesOptions>
    {
        public TenantDatabasesOptions CurrentValue => currentValue;
        public TenantDatabasesOptions Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<TenantDatabasesOptions, string?> listener) => null;
    }

    [Fact]
    public void TryResolveConnectionString_SupportsConnectionsMap_AndCnpgSecretsDirectoryFormats()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vital-bill-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var uriTenantId = Guid.Parse("10000000-0000-0000-0000-000000000021");
            var cnpgFilesTenantId = Guid.Parse("10000000-0000-0000-0000-000000000022");
            var mapTenantId = Guid.Parse("10000000-0000-0000-0000-000000000023");

            var uriDir = Path.Combine(tempRoot, uriTenantId.ToString("D"));
            Directory.CreateDirectory(uriDir);
            File.WriteAllText(
                Path.Combine(uriDir, "uri"),
                "postgresql://bill_u:Sec%21ret1@pg-bill-21.internal:5432/billing_t21?sslmode=Require\n");

            var filesDir = Path.Combine(tempRoot, cnpgFilesTenantId.ToString("D"));
            Directory.CreateDirectory(filesDir);
            File.WriteAllText(Path.Combine(filesDir, "host"), "pg-bill-22.internal\n");
            File.WriteAllText(Path.Combine(filesDir, "port"), "5435");
            File.WriteAllText(Path.Combine(filesDir, "dbname"), "billing_t22");
            File.WriteAllText(Path.Combine(filesDir, "username"), "bill_22");
            File.WriteAllText(Path.Combine(filesDir, "password"), "pass_22");
            File.WriteAllText(Path.Combine(filesDir, "sslmode"), "Disable");

            var resolver = new TenantConnectionResolver(new StaticOptionsMonitor(new TenantDatabasesOptions
            {
                SecretsDirectory = tempRoot,
                Connections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [mapTenantId.ToString("D")] = "Host=pg-bill-23;Port=5432;Database=billing_t23;Username=u23;Password=p23",
                },
            }));

            Assert.True(resolver.TryResolveConnectionString(uriTenantId, out var cs21));
            Assert.Contains("Host=pg-bill-21.internal", cs21);
            Assert.Contains("Database=billing_t21", cs21);
            Assert.Contains("SSL Mode=Require", cs21, StringComparison.OrdinalIgnoreCase);

            Assert.True(resolver.TryResolveConnectionString(cnpgFilesTenantId, out var cs22));
            Assert.Contains("Host=pg-bill-22.internal", cs22);
            Assert.Contains("Port=5435", cs22);
            Assert.Contains("Database=billing_t22", cs22);

            Assert.True(resolver.TryResolveConnectionString(mapTenantId, out var cs23));
            Assert.Contains("Database=billing_t23", cs23);

            Assert.False(resolver.TryResolveConnectionString(Guid.Empty, out _));
            Assert.False(resolver.TryResolveConnectionString(Guid.NewGuid(), out _));

            var configured = resolver.ListConfiguredTenantIds();
            Assert.Contains(uriTenantId, configured);
            Assert.Contains(cnpgFilesTenantId, configured);
            Assert.Contains(mapTenantId, configured);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
