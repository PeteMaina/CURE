using CURE.Application.Customers;
using CURE.Application.CoreCrm;
using CURE.Application.Pipeline;
using CURE.Application.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace CURE.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCureApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICoreCrmService, CoreCrmService>();
        services.AddScoped<IPipelineService, PipelineService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        return services;
    }
}