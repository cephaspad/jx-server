using JXServer.Application.Accounts;
using JXServer.Application.Characters;
using JXServer.Application.Configuration;
using JXServer.Application.SessionLeases;
using JXServer.Contracts.Accounts;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.Domain.Accounts;
using JXServer.Domain.Characters;
using JXServer.Domain.SessionLeases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JXServer.Application;

/// <summary>
/// Dependency injection registration extensions for Control-Plane Domain Managers and Application Services.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddJXServerApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ApplicationOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        OptionsBuilder<ApplicationOptions> optionsBuilder = services
            .AddOptions<ApplicationOptions>()
            .Bind(configuration.GetSection(ApplicationOptions.SectionName));

        if (configureOptions is not null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AccountOperationLockProvider>();
        services.AddSingleton<AccountManager>();
        services.AddSingleton<CharacterManager>();
        services.AddSingleton<SessionLeaseManager>();

        services.AddSingleton<IAccountAppService, AccountAppService>();
        services.AddSingleton<ICharacterAppService, CharacterAppService>();
        services.AddSingleton<ISessionLeaseAppService, SessionLeaseAppService>();

        return services;
    }
}
