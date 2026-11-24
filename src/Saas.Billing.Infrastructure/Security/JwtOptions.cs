namespace Saas.Billing.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "saas-identity";
    public string Audience { get; init; } = "vital-saas";
    public string SigningKey { get; init; } = string.Empty;
}
