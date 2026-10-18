using ExpenseControl.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseControl.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers the application services. Repositories and other adapters come from the infrastructure layer.</summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuthService>();
        services.AddScoped<LedgerService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
