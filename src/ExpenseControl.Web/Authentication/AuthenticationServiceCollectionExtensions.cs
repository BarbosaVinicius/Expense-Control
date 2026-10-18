using Microsoft.AspNetCore.Components.Authorization;

namespace ExpenseControl.Web.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddSessionAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SessionOptions>()
            .Bind(configuration.GetSection(SessionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<SessionAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SessionAuthenticationStateProvider>());
        return services;
    }
}
