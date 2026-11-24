using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Saas.Billing.Application.UseCases.Adjustments;
using Saas.Billing.Application.UseCases.ChargeSheets;
using Saas.Billing.Application.UseCases.Cuts;
using Saas.Billing.Application.UseCases.Metrics;
using Saas.Billing.Application.UseCases.Payments;
using Saas.Billing.Domain.Common;

namespace Saas.Billing.IntegrationTests;

public sealed class MultiTenantIsolationIntegrationTests(MultiPostgresFixture fixture)
    : IClassFixture<MultiPostgresFixture>
{
    [Fact]
    public async Task BillingOperations_AndCutFlow_ArePhysicallyIsolatedAcrossTenantPostgresInstances()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateDefaultClient();

        var adminAToken = SignJwt(MultiPostgresFixture.AdminAId, MultiPostgresFixture.ClinicAId, "ADMIN_CLINICA", "Admin Alfa", "admin@alfa.demo");
        var specAToken = SignJwt(MultiPostgresFixture.SpecAId, MultiPostgresFixture.ClinicAId, "ESPECIALISTA", "Dra. Alfa", "dra@alfa.demo");
        var adminBToken = SignJwt(MultiPostgresFixture.AdminBId, MultiPostgresFixture.ClinicBId, "ADMIN_CLINICA", "Admin Beta", "admin@beta.demo");

        // 1. Clínica Alfa: crear hoja ABIERTA -> REVISADA -> cobro completo -> corte -> confirmar -> ajuste vinculado
        var sheetA = await SendAsync<ChargeSheetDto>(
            client,
            HttpMethod.Put,
            $"/v1/encounters/{MultiPostgresFixture.EncAId}/charge-sheet",
            adminAToken,
            new
            {
                items = new[]
                {
                    new { conceptCode = "CON-ESP", description = "Consulta de especialidad", category = "CONSULTA", quantity = 1, unitPriceMxn = 1200m },
                    new { conceptCode = "EST-ECG", description = "Electrocardiograma", category = "ESTUDIO", quantity = 1, unitPriceMxn = 650m },
                },
            });
        Assert.Equal("ABIERTA", sheetA.Status);
        Assert.Equal(1850m, sheetA.TotalMxn);

        var reviewedA = await SendAsync<ChargeSheetDto>(
            client,
            HttpMethod.Post,
            $"/v1/charge-sheets/{sheetA.Id}/review",
            adminAToken);
        Assert.Equal("REVISADA", reviewedA.Status);

        var paymentA = await SendAsync<PatientPaymentDto>(
            client,
            HttpMethod.Post,
            "/v1/payments",
            adminAToken,
            new { chargeSheetId = sheetA.Id, amountMxn = 1850m, method = "TARJETA" });
        Assert.StartsWith("COB-", paymentA.Folio);

        var todayGmt6 = Gmt6Clock.FormatDateGmt6(Gmt6Clock.ToDateOnlyGmt6(DateTimeOffset.UtcNow));
        var cutA = await SendAsync<SpecialistCutDetailDto>(
            client,
            HttpMethod.Post,
            "/v1/cuts",
            adminAToken,
            new
            {
                specialistId = MultiPostgresFixture.SpecAId,
                periodStartGmt6 = todayGmt6,
                periodEndGmt6 = todayGmt6,
                paymentIds = new[] { paymentA.Id },
            });
        Assert.Equal("PREPARADO", cutA.Status);
        Assert.Equal(1850m, cutA.TotalMxn);

        var confirmedCutA = await SendAsync<SpecialistCutDetailDto>(
            client,
            HttpMethod.Post,
            $"/v1/cuts/{cutA.Id}/confirm",
            specAToken,
            new { note = "Conforme" });
        Assert.Equal("CONFIRMADO", confirmedCutA.Status);

        var adjustmentA = await SendAsync<BillingAdjustmentDto>(
            client,
            HttpMethod.Post,
            "/v1/adjustments",
            adminAToken,
            new { targetType = "CUT", targetId = cutA.Id, deltaMxn = -150m, reason = "Bonificación clínica alfa" });
        Assert.StartsWith("AJU-", adjustmentA.Folio);

        // 2. Clínica Beta opera en su propia instancia PostgreSQL 17
        var sheetB = await SendAsync<ChargeSheetDto>(
            client,
            HttpMethod.Put,
            $"/v1/encounters/{MultiPostgresFixture.EncBId}/charge-sheet",
            adminBToken,
            new
            {
                items = new[]
                {
                    new { conceptCode = "CON-GEN", description = "Consulta general Beta", category = "CONSULTA", quantity = 1, unitPriceMxn = 900m },
                },
            });
        await SendAsync<ChargeSheetDto>(
            client,
            HttpMethod.Post,
            $"/v1/charge-sheets/{sheetB.Id}/review",
            adminBToken);
        var paymentB = await SendAsync<PatientPaymentDto>(
            client,
            HttpMethod.Post,
            "/v1/payments",
            adminBToken,
            new { chargeSheetId = sheetB.Id, amountMxn = 900m, method = "EFECTIVO" });
        Assert.Equal("COB-", paymentB.Folio[..4]);

        // 3. Aislamiento lógico HTTP: Beta no puede ver hojas de Alfa ni métricas de Alfa
        using (var crossReq = new HttpRequestMessage(HttpMethod.Get, $"/v1/charge-sheets/{sheetA.Id}"))
        {
            crossReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminBToken);
            var crossResp = await client.SendAsync(crossReq);
            Assert.Equal(HttpStatusCode.NotFound, crossResp.StatusCode);
        }

        var listB = await SendAsync<ChargeSheetListResponse>(client, HttpMethod.Get, "/v1/charge-sheets", adminBToken);
        Assert.Single(listB.Items);
        Assert.Equal(sheetB.Id, listB.Items[0].Id);

        var metricsA = await SendAsync<KpiEnvelopeDto<RecordedPaymentsKpiData>>(client, HttpMethod.Get, "/v1/metrics/clinic/payments", adminAToken);
        var metricsB = await SendAsync<KpiEnvelopeDto<RecordedPaymentsKpiData>>(client, HttpMethod.Get, "/v1/metrics/clinic/payments", adminBToken);
        Assert.Equal("AVAILABLE", metricsA.Status);
        Assert.Equal(1850m, metricsA.Data?.TotalMxn);
        Assert.Equal("AVAILABLE", metricsB.Status);
        Assert.Equal(900m, metricsB.Data?.TotalMxn);

        // 4. Verificación SQL directa por instancia PostgreSQL
        Assert.Equal(1L, await CountSqlAsync(fixture.TenantADbConnectionString, "SELECT COUNT(*) FROM billing.charge_sheets;"));
        Assert.Equal(1L, await CountSqlAsync(fixture.TenantADbConnectionString, "SELECT COUNT(*) FROM billing.patient_payments;"));
        Assert.Equal(1L, await CountSqlAsync(fixture.TenantADbConnectionString, "SELECT COUNT(*) FROM billing.specialist_cuts;"));
        Assert.Equal(1L, await CountSqlAsync(fixture.TenantADbConnectionString, "SELECT COUNT(*) FROM billing.billing_adjustments;"));
        Assert.Equal(0L, await CountSqlAsync(fixture.TenantADbConnectionString, $"SELECT COUNT(*) FROM billing.charge_sheets WHERE tenant_id <> '{MultiPostgresFixture.ClinicAId}';"));

        Assert.Equal(1L, await CountSqlAsync(fixture.TenantBDbConnectionString, "SELECT COUNT(*) FROM billing.charge_sheets;"));
        Assert.Equal(1L, await CountSqlAsync(fixture.TenantBDbConnectionString, "SELECT COUNT(*) FROM billing.patient_payments;"));
        Assert.Equal(0L, await CountSqlAsync(fixture.TenantBDbConnectionString, "SELECT COUNT(*) FROM billing.specialist_cuts;"));
        Assert.Equal(0L, await CountSqlAsync(fixture.TenantBDbConnectionString, "SELECT COUNT(*) FROM billing.billing_adjustments;"));
        Assert.Equal(0L, await CountSqlAsync(fixture.TenantBDbConnectionString, $"SELECT COUNT(*) FROM billing.charge_sheets WHERE tenant_id <> '{MultiPostgresFixture.ClinicBId}';"));
    }

    [Fact]
    public async Task UnconfiguredTenantDatabase_FailsClosedWith503()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateDefaultClient();

        var unconfiguredToken = SignJwt(
            MultiPostgresFixture.AdminUnconfiguredId,
            MultiPostgresFixture.ClinicUnconfiguredId,
            "ADMIN_CLINICA",
            "Admin Sin BD",
            "admin@sinbd.demo");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/v1/charge-sheets");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", unconfiguredToken);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("TENANT_DATABASE_NOT_CONFIGURED", json.RootElement.GetProperty("code").GetString());
    }

    private static string SignJwt(Guid accountId, Guid tenantId, string role, string fullName, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MultiPostgresFixture.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim("sub", accountId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("role", role),
            new Claim("full_name", fullName),
            new Claim("email", email),
        };
        var token = new JwtSecurityToken(
            issuer: "saas-identity",
            audience: "vital-saas",
            claims: claims,
            notBefore: now.AddSeconds(-5),
            expires: now.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<T> SendAsync<T>(
        HttpClient client,
        HttpMethod method,
        string url,
        string bearerToken,
        object? payload = null)
    {
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        if (payload is not null)
        {
            req.Content = JsonContent.Create(payload);
        }

        var resp = await client.SendAsync(req);
        var raw = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, $"{method} {url} failed with {(int)resp.StatusCode}: {raw}");
        var parsed = JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        return parsed!;
    }

    private static async Task<long> CountSqlAsync(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var scalar = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(scalar);
    }
}
