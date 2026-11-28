using CURE.Application.Customers;
using CURE.Application.CoreCrm;
using CURE.Application.Engagement;
using CURE.Application.Pipeline;
using CURE.Application.Workspace;
using CURE.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CURE.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCureInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Database:ConnectionString must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.MigrationConnectionString),
                "Database:MigrationConnectionString must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RuntimeRoleName),
                "Database:RuntimeRoleName must identify the non-owner application role.")
            .ValidateOnStart();
        services.AddSingleton<PostgresConnectionFactory>();
        services.AddScoped<ICustomerStore, PostgresCustomerStore>();
        services.AddScoped<ICoreCrmStore, PostgresCoreCrmStore>();
        services.AddScoped<IPipelineStore, PostgresPipelineStore>();
        services.AddScoped<IWorkspaceStore, PostgresWorkspaceStore>();
        services.AddScoped<IActivityStore, PostgresActivityStore>();
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}