using ExpenseControl.Core.Abstractions;
using ExpenseControl.Infrastructure.Email;
using ExpenseControl.Infrastructure.Persistence;
using ExpenseControl.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace ExpenseControl.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>Registers MySQL persistence, schema migrations, password hashing and e-mail delivery.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string 'ConnectionStrings:{ConnectionStringName}' is not configured. " +
                "In development it comes from appsettings.Development.json (start the database with: docker compose up -d). " +
                "Elsewhere set the environment variable ConnectionStrings__DefaultConnection.");
        }

        services.AddSingleton(_ => new MySqlDataSourceBuilder(connectionString).Build());
        services.AddSingleton(sp => new MigrationRunner(
            sp.GetRequiredService<MySqlDataSource>(), connectionString, sp.GetRequiredService<ILogger<MigrationRunner>>()));

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddHostedService<DatabaseMigrationHostedService>();

        services.AddSingleton<IUserRepository, MySqlUserRepository>();
        services.AddSingleton<ILedgerRepository, MySqlLedgerRepository>();
        services.AddSingleton<IBudgetRepository, MySqlBudgetRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        services.AddOptions<EmailOptions>().Bind(emailSection);
        if (emailSection.GetValue<bool>(nameof(EmailOptions.Enabled)))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }
}
