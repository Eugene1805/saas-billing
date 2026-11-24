using Microsoft.Extensions.Options;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence;

namespace Saas.Billing.Infrastructure.Directory;

public sealed class DirectoryOptions
{
    public const string SectionName = "Directory";

    public string Mode { get; init; } = "Local";
    public bool SimulateUnavailable { get; init; }
    public List<SpecialistBillingSeedEntry> Specialists { get; init; } = [];
}

public sealed class SpecialistBillingSeedEntry
{
    public Guid AccountId { get; init; }
    public Guid TenantId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Specialty { get; init; } = string.Empty;
    public bool Active { get; init; } = true;
}

internal sealed class LocalBillingDirectoryAdapter(
    IOptions<DirectoryOptions> optionsAccessor) : IBillingDirectoryPort
{
    private static readonly IReadOnlyList<SpecialistBillingSeedEntry> DefaultSpecialists =
    [
        new()
        {
            AccountId = BillingDbInitializer.MendozaNorteAccountId,
            TenantId = BillingDbInitializer.NorteTenantId,
            FullName = "Dra. Sofía Mendoza",
            Specialty = "Cardiología",
            Active = true,
        },
        new()
        {
            AccountId = BillingDbInitializer.SalazarNorteAccountId,
            TenantId = BillingDbInitializer.NorteTenantId,
            FullName = "Dr. Mateo Salazar",
            Specialty = "Medicina General",
            Active = true,
        },
        new()
        {
            AccountId = BillingDbInitializer.VegaNorteAccountId,
            TenantId = BillingDbInitializer.NorteTenantId,
            FullName = "Dr. Nicolás Vega",
            Specialty = "Pediatría",
            Active = false,
        },
        new()
        {
            AccountId = BillingDbInitializer.MendozaValleAccountId,
            TenantId = BillingDbInitializer.ValleTenantId,
            FullName = "Dra. Sofía Mendoza (Cuenta Valle)",
            Specialty = "Cardiología",
            Active = true,
        },
    ];

    public Task<SpecialistLookupResult> GetSpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(SpecialistLookupResult.Unavailable());
        }

        var catalog = opts.Specialists.Count > 0 ? opts.Specialists : DefaultSpecialists;
        var match = catalog.FirstOrDefault(s =>
            s.TenantId == tenantId && s.AccountId == specialistId);

        if (match is null)
        {
            return Task.FromResult(SpecialistLookupResult.NotFound());
        }

        return Task.FromResult(SpecialistLookupResult.WithSpecialist(new SpecialistSummary(
            match.AccountId,
            match.TenantId,
            match.Active,
            match.FullName,
            match.Specialty)));
    }
}
