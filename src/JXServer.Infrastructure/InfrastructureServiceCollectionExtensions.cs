using JXServer.Domain.Accounts;
using JXServer.Domain.Characters;
using JXServer.Domain.SessionLeases;
using JXServer.Infrastructure.Accounts;
using JXServer.Infrastructure.Characters;
using JXServer.Infrastructure.Configuration;
using JXServer.Infrastructure.EntityFrameworkCore;
using JXServer.Infrastructure.SessionLeases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JXServer.Infrastructure;

/// <summary>
/// Dependency injection registration extensions for EF Core 10 persistence and infrastructure services.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="JXServerDbContext"/>, repositories, and database initialization hosted service.
    /// </summary>
    public static IServiceCollection AddJXServerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PersistenceOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        OptionsBuilder<PersistenceOptions> optionsBuilder = services
            .AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName));

        if (configureOptions is not null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.AddDbContextFactory<JXServerDbContext>((sp, builder) =>
        {
            PersistenceOptions options = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            if (string.Equals(options.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                builder.UseSqlServer(options.ConnectionString);
            }
            else
            {
                builder.UseSqlite(options.ConnectionString);
            }
        });

        services.AddSingleton<IAccountRepository, EfCoreAccountRepository>();
        services.AddSingleton<ICharacterRepository, EfCoreCharacterRepository>();
        services.AddSingleton<ISessionLeaseRepository, InMemorySessionLeaseRepository>();
        services.AddHostedService<DatabaseInitializerHostedService>();

        return services;
    }
}
