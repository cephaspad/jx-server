using JXServer.Domain.Characters;
using JXServer.Infrastructure.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JXServer.Infrastructure.Characters;

/// <summary>
/// EF Core 10 repository implementation for the <see cref="Character"/> aggregate root.
/// </summary>
public sealed class EfCoreCharacterRepository : ICharacterRepository
{
    private readonly IDbContextFactory<JXServerDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreCharacterRepository"/> class.
    /// </summary>
    public EfCoreCharacterRepository(IDbContextFactory<JXServerDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<Character?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Characters
            .Include(x => x.Skills)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Character?> FindByNormalizedRoleNameAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
    {
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Characters
            .Include(x => x.Skills)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedRoleName == normalizedRoleName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Character>> GetListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Characters
            .Include(x => x.Skills)
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.SlotIndex)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetCountByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Characters
            .CountAsync(x => x.AccountId == accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Character> InsertAsync(Character character, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Characters.AddAsync(character, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return character;
    }

    /// <inheritdoc />
    public async Task<Character> UpdateAsync(Character character, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        Character? existing = await dbContext.Characters
            .Include(x => x.Skills)
            .FirstOrDefaultAsync(x => x.Id == character.Id, cancellationToken);

        if (existing is null)
        {
            dbContext.Characters.Update(character);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(character);
            await dbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return character;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Character character, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        Character? existing = await dbContext.Characters
            .Include(x => x.Skills)
            .FirstOrDefaultAsync(x => x.Id == character.Id, cancellationToken);

        if (existing is not null)
        {
            dbContext.Characters.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
