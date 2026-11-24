using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saas.Billing.Application.Ports;
using Saas.Billing.Application.UseCases.Adjustments;
using Saas.Billing.Application.UseCases.ChargeSheets;
using Saas.Billing.Application.UseCases.Cuts;
using Saas.Billing.Application.UseCases.Metrics;
using Saas.Billing.Application.UseCases.Payments;
using Saas.Billing.Infrastructure.AccessControl;
using Saas.Billing.Infrastructure.Clinical;
using Saas.Billing.Infrastructure.Directory;
using Saas.Billing.Infrastructure.Persistence;
using Saas.Billing.Infrastructure.Persistence.Repositories;
using Saas.Billing.Infrastructure.Persistence.Tenancy;
using Saas.Billing.Infrastructure.Platform;
using Saas.Billing.Infrastructure.Security;

namespace Saas.Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PlatformOptions>(configuration.GetSection(PlatformOptions.SectionName));
        services.Configure<AccessControlOptions>(configuration.GetSection(AccessControlOptions.SectionName));
        services.Configure<ClinicalOptions>(configuration.GetSection(ClinicalOptions.SectionName));
        services.Configure<DirectoryOptions>(configuration.GetSection(DirectoryOptions.SectionName));
        services.Configure<TenantDatabasesOptions>(configuration.GetSection(TenantDatabasesOptions.SectionName));

        services.AddSingleton<ITenantConnectionResolver, TenantConnectionResolver>();
        services.AddSingleton<TenantDataSourcePool>();
        services.AddSingleton<ITenantBillingDbContextFactory, NpgsqlTenantBillingDbContextFactory>();
        services.AddSingleton<ITenantDatabaseMigratorPort, EfTenantDatabaseMigrator>();
        services.AddScoped<TenantBillingDbContextAccessor>();

        services.AddSingleton<IClockPort, SystemClockAdapter>();
        services.AddScoped<IChargeSheetRepository, EfChargeSheetRepository>();
        services.AddScoped<IPaymentRepository, EfPaymentRepository>();
        services.AddScoped<ISpecialistCutRepository, EfSpecialistCutRepository>();
        services.AddScoped<IBillingAdjustmentRepository, EfBillingAdjustmentRepository>();

        services.AddScoped<ITenantBillingAccessPort, LocalTenantBillingAccessAdapter>();
        services.AddScoped<IBillingAccessControlPort, LocalBillingAccessControlAdapter>();
        services.AddScoped<IClinicalEncounterGatewayPort, LocalClinicalEncounterGatewayAdapter>();
        services.AddScoped<IBillingDirectoryPort, LocalBillingDirectoryAdapter>();
        services.AddScoped<BillingDbInitializer>();

        services.AddScoped<ListChargeSheetsUseCase>();
        services.AddScoped<GetChargeSheetByIdUseCase>();
        services.AddScoped<UpsertChargeSheetUseCase>();
        services.AddScoped<ReviewChargeSheetUseCase>();
        services.AddScoped<ReopenChargeSheetUseCase>();

        services.AddScoped<ListPaymentsUseCase>();
        services.AddScoped<RecordFullPaymentUseCase>();

        services.AddScoped<ListCutsUseCase>();
        services.AddScoped<GetCutByIdUseCase>();
        services.AddScoped<PrepareCutUseCase>();
        services.AddScoped<ReviseCutUseCase>();
        services.AddScoped<ConfirmCutUseCase>();
        services.AddScoped<ObserveCutUseCase>();

        services.AddScoped<ListAdjustmentsUseCase>();
        services.AddScoped<CreateAdjustmentUseCase>();

        services.AddScoped<GetClinicPaymentsMetricUseCase>();
        services.AddScoped<GetClinicCutsMetricUseCase>();

        return services;
    }
}
