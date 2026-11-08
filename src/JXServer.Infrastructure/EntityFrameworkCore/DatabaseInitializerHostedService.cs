using JXServer.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JXServer.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Hosted service that initializes the EF Core database schema on startup when <see cref="PersistenceOptions.AutoInitializeDatabase"/> is enabled.
/// </summary>
public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IDbContextFactory<JXServerDbContext> _dbContextFactory;
    private readonly IOptions<PersistenceOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseInitializerHostedService"/> class.
    /// </summary>
    public DatabaseInitializerHostedService(
        IDbContextFactory<JXServerDbContext> dbContextFactory,
        IOptions<PersistenceOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _options = options;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.AutoInitializeDatabase)
        {
            return;
        }

        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
