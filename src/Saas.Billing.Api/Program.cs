using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Saas.Billing.Api.Endpoints;
using Saas.Billing.Api.OpenApi;
using Saas.Billing.Infrastructure;
using Saas.Billing.Infrastructure.Persistence;
using Saas.Billing.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

var httpPort = builder.Configuration.GetValue("Ports:Http", 8080);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(httpPort, listen => listen.Protocols = HttpProtocols.Http1);
});

builder.Services.AddBillingInfrastructure(builder.Configuration);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) ||
    Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException("Configura Jwt:SigningKey con al menos 32 bytes antes de iniciar saas-billing.");
}

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddExceptionHandler<TenantDatabaseExceptionHandler>();
builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:4173", "http://127.0.0.1:4173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("VitalFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddBillingOpenApi();

var app = builder.Build();

await InitializeDatabaseWithRetryAsync(app.Services, app.Environment.IsProduction(), app.Logger);

app.UseExceptionHandler();
app.UseCors("VitalFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapBillingOpenApi();
app.MapHealthEndpoints();
app.MapCatalogEndpoints();
app.MapChargeSheetEndpoints();
app.MapPaymentEndpoints();
app.MapCutEndpoints();
app.MapAdjustmentEndpoints();
app.MapMetricsEndpoints();

app.Run();

static async Task InitializeDatabaseWithRetryAsync(
    IServiceProvider services,
    bool isProduction,
    ILogger logger)
{
    const int maxAttempts = 10;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<BillingDbInitializer>();
            await initializer.InitializeConfiguredTenantsAsync(isProduction);
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && attempt < maxAttempts)
        {
            logger.LogWarning(
                ex,
                "Base de datos aún no disponible al inicializar Billing (intento {Attempt}/{MaxAttempts}).",
                attempt,
                maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

public partial class Program;
