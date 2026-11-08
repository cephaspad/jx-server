using JXServer.Domain.Accounts;
using JXServer.Infrastructure.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JXServer.Infrastructure.Accounts;

/// <summary>
/// EF Core 10 repository implementation for the <see cref="Account"/> aggregate root.
/// </summary>
public sealed class EfCoreAccountRepository : IAccountRepository
{
    private readonly IDbContextFactory<JXServerDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreAccountRepository"/> class.
    /// </summary>
    public EfCoreAccountRepository(IDbContextFactory<JXServerDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Account?> FindByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
    {
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Account> InsertAsync(Account account, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Accounts.AddAsync(account, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    /// <inheritdoc />
    public async Task<Account> UpdateAsync(Account account, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Accounts.Update(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }
}
