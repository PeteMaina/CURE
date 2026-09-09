using CURE.Application.Customers;
using CURE.Application.CoreCrm;
using Microsoft.Extensions.DependencyInjection;

namespace CURE.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCureApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICoreCrmService, CoreCrmService>();
        return services;
    }
}