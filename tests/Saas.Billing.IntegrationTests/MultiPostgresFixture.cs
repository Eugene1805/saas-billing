using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Saas.Billing.IntegrationTests;

public sealed class MultiPostgresFixture : IAsyncLifetime
{
    public static readonly Guid ClinicAId = Guid.Parse("10000000-0000-0000-0000-0000000000a1");
    public static readonly Guid ClinicBId = Guid.Parse("10000000-0000-0000-0000-0000000000b2");
    public static readonly Guid ClinicUnconfiguredId = Guid.Parse("10000000-0000-0000-0000-0000000000c3");

    public static readonly Guid AdminAId = Guid.Parse("20000000-0000-0000-0000-000000000a01");
    public static readonly Guid SpecAId = Guid.Parse("20000000-0000-0000-0000-000000000a02");
    public static readonly Guid AdminBId = Guid.Parse("20000000-0000-0000-0000-000000000b01");
    public static readonly Guid SpecBId = Guid.Parse("20000000-0000-0000-0000-000000000b02");
    public static readonly Guid AdminUnconfiguredId = Guid.Parse("20000000-0000-0000-0000-000000000c01");

    public static readonly Guid PatAId = Guid.Parse("30000000-0000-0000-0000-000000000a01");
    public static readonly Guid PatBId = Guid.Parse("30000000-0000-0000-0000-000000000b01");
    public static readonly Guid PatCId = Guid.Parse("30000000-0000-0000-0000-000000000c01");

    public static readonly Guid RecAId = Guid.Parse("35000000-0000-0000-0000-000000000a01");
    public static readonly Guid RecBId = Guid.Parse("35000000-0000-0000-0000-000000000b01");
    public static readonly Guid RecCId = Guid.Parse("35000000-0000-0000-0000-000000000c01");

    public static readonly Guid EncAId = Guid.Parse("40000000-0000-0000-0000-000000000a01");
    public static readonly Guid EncBId = Guid.Parse("40000000-0000-0000-0000-000000000b01");
    public static readonly Guid EncCId = Guid.Parse("40000000-0000-0000-0000-000000000c01");

    public const string JwtSigningKey = "BillingIntegrationJwtSigningKey_AtLeast32Bytes!";

    private readonly List<PostgreSqlContainer> _containers = [];

    public string TenantADbConnectionString { get; private set; } = string.Empty;
    public string TenantBDbConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var extA = Environment.GetEnvironmentVariable("TEST_TENANT_A_DB_CS");
        var extB = Environment.GetEnvironmentVariable("TEST_TENANT_B_DB_CS");

        if (!string.IsNullOrWhiteSpace(extA) && !string.IsNullOrWhiteSpace(extB))
        {
            TenantADbConnectionString = extA;
            TenantBDbConnectionString = extB;
            return;
        }

        var containerA = BuildPostgres("billing_alfa", "alfa_u", "alfa_p");
        var containerB = BuildPostgres("billing_beta", "beta_u", "beta_p");

        _containers.AddRange([containerA, containerB]);
        await Task.WhenAll(_containers.Select(c => c.StartAsync()));

        TenantADbConnectionString = containerA.GetConnectionString();
        TenantBDbConnectionString = containerB.GetConnectionString();
    }

    public WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            var settings = new Dictionary<string, string?>
            {
                [$"TenantDatabases:Connections:{ClinicAId}"] = TenantADbConnectionString,
                [$"TenantDatabases:Connections:{ClinicBId}"] = TenantBDbConnectionString,
                ["Jwt:Issuer"] = "saas-identity",
                ["Jwt:Audience"] = "vital-saas",
                ["Jwt:SigningKey"] = JwtSigningKey,
                ["Platform:Mode"] = "Local",
                ["AccessControl:Mode"] = "Local",
                ["Clinical:Mode"] = "Local",
                ["Directory:Mode"] = "Local",
                ["SeedDemoData"] = "false",
                ["OpenApi:Enabled"] = "false",

                ["Platform:Tenants:0:TenantId"] = ClinicAId.ToString(),
                ["Platform:Tenants:0:Slug"] = "clinica-alfa",
                ["Platform:Tenants:0:Name"] = "Clínica Alfa",
                ["Platform:Tenants:0:State"] = "ACTIVA",
                ["Platform:Tenants:0:UsesBilling"] = "true",

                ["Platform:Tenants:1:TenantId"] = ClinicBId.ToString(),
                ["Platform:Tenants:1:Slug"] = "clinica-beta",
                ["Platform:Tenants:1:Name"] = "Clínica Beta",
                ["Platform:Tenants:1:State"] = "ACTIVA",
                ["Platform:Tenants:1:UsesBilling"] = "true",

                ["Platform:Tenants:2:TenantId"] = ClinicUnconfiguredId.ToString(),
                ["Platform:Tenants:2:Slug"] = "clinica-sin-bd",
                ["Platform:Tenants:2:Name"] = "Clínica Sin BD",
                ["Platform:Tenants:2:State"] = "ACTIVA",
                ["Platform:Tenants:2:UsesBilling"] = "true",

                ["Directory:Specialists:0:AccountId"] = SpecAId.ToString(),
                ["Directory:Specialists:0:TenantId"] = ClinicAId.ToString(),
                ["Directory:Specialists:0:FullName"] = "Dra. Alfa Cardiología",
                ["Directory:Specialists:0:Specialty"] = "Cardiología",
                ["Directory:Specialists:0:Active"] = "true",

                ["Directory:Specialists:1:AccountId"] = SpecBId.ToString(),
                ["Directory:Specialists:1:TenantId"] = ClinicBId.ToString(),
                ["Directory:Specialists:1:FullName"] = "Dr. Beta Medicina",
                ["Directory:Specialists:1:Specialty"] = "Medicina General",
                ["Directory:Specialists:1:Active"] = "true",

                ["AccessControl:Patients:0:PatientId"] = PatAId.ToString(),
                ["AccessControl:Patients:0:TenantId"] = ClinicAId.ToString(),
                ["AccessControl:Patients:0:PrimarySpecialistId"] = SpecAId.ToString(),

                ["AccessControl:Patients:1:PatientId"] = PatBId.ToString(),
                ["AccessControl:Patients:1:TenantId"] = ClinicBId.ToString(),
                ["AccessControl:Patients:1:PrimarySpecialistId"] = SpecBId.ToString(),

                ["AccessControl:Patients:2:PatientId"] = PatCId.ToString(),
                ["AccessControl:Patients:2:TenantId"] = ClinicUnconfiguredId.ToString(),

                ["Clinical:Encounters:0:EncounterId"] = EncAId.ToString(),
                ["Clinical:Encounters:0:TenantId"] = ClinicAId.ToString(),
                ["Clinical:Encounters:0:RecordId"] = RecAId.ToString(),
                ["Clinical:Encounters:0:PatientId"] = PatAId.ToString(),
                ["Clinical:Encounters:0:SpecialistId"] = SpecAId.ToString(),
                ["Clinical:Encounters:0:Status"] = "FINALIZADA",

                ["Clinical:Encounters:1:EncounterId"] = EncBId.ToString(),
                ["Clinical:Encounters:1:TenantId"] = ClinicBId.ToString(),
                ["Clinical:Encounters:1:RecordId"] = RecBId.ToString(),
                ["Clinical:Encounters:1:PatientId"] = PatBId.ToString(),
                ["Clinical:Encounters:1:SpecialistId"] = SpecBId.ToString(),
                ["Clinical:Encounters:1:Status"] = "FINALIZADA",

                ["Clinical:Encounters:2:EncounterId"] = EncCId.ToString(),
                ["Clinical:Encounters:2:TenantId"] = ClinicUnconfiguredId.ToString(),
                ["Clinical:Encounters:2:RecordId"] = RecCId.ToString(),
                ["Clinical:Encounters:2:PatientId"] = PatCId.ToString(),
                ["Clinical:Encounters:2:SpecialistId"] = AdminUnconfiguredId.ToString(),
                ["Clinical:Encounters:2:Status"] = "FINALIZADA",
            };

            foreach (var kv in settings)
            {
                builder.UseSetting(kv.Key, kv.Value);
            }
        });
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_containers.Select(c => c.DisposeAsync().AsTask()));
    }

    private static PostgreSqlContainer BuildPostgres(string database, string username, string password) =>
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase(database)
            .WithUsername(username)
            .WithPassword(password)
            .Build();
}
