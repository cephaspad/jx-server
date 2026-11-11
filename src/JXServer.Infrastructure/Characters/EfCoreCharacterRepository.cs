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
            .Include(x => x.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Character?> FindByNormalizedRoleNameAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
    {
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Characters
            .Include(x => x.Skills)
            .Include(x => x.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedRoleName == normalizedRoleName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Character>> GetListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Characters
            .Include(x => x.Skills)
            .Include(x => x.Items)
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
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == character.Id, cancellationToken);

        if (existing is null)
        {
            dbContext.Characters.Update(character);
            await dbContext.SaveChangesAsync(cancellationToken);
            return character;
        }

        // Remove existing CharacterItems from DbSet before replacing the collection so newly added items are tracked as Added
        if (existing.Items.Count > 0)
        {
            dbContext.CharacterItems.RemoveRange(existing.Items);
        }

        var freshItems = new List<CharacterItem>(character.Items.Count);
        foreach (CharacterItem item in character.Items)
        {
            var cloned = new CharacterItem(
                item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                character.Id,
                item.Genre,
                item.DetailType,
                item.ParticularType,
                item.Level,
                item.Series,
                item.Quality,
                item.RandomSeed,
                item.GeneratorVersion,
                item.Luck,
                item.CurrentDurability,
                item.MaxDurability,
                item.StackCount,
                item.ContainerPlace,
                item.GridX,
                item.GridY,
                item.BindState,
                item.ExpireTimeUnixSeconds,
                item.CreatorName,
                item.MagicAttributes,
                item.IsBroken,
                item.EnchaseLevel);
            freshItems.Add(cloned);
            await dbContext.CharacterItems.AddAsync(cloned, cancellationToken);
        }

        existing.UpdateWorldSnapshot(
            character.MapId,
            character.MapMpsX,
            character.MapMpsY,
            character.CurrentLife,
            character.CurrentMana,
            character.CurrentStamina,
            character.Level,
            character.Experience,
            character.LeadExperience,
            character.Strength,
            character.Dexterity,
            character.Vitality,
            character.Energy,
            character.UnallocatedAttributePoints,
            character.UnallocatedSkillPoints,
            character.MaxLife,
            character.MaxMana,
            character.MaxStamina,
            character.InventoryMoney,
            character.RepositoryMoney,
            character.PkState,
            character.PkValue,
            character.ActiveAuraSkillId,
            character.Skills,
            freshItems,
            character.UpdatedAtUtc,
            character.TaskValues,
            character.RepositoryPasswordHash,
            updateRepositoryPasswordHash: true);

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Character character, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        await using JXServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        Character? existing = await dbContext.Characters
            .Include(x => x.Skills)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == character.Id, cancellationToken);

        if (existing is not null)
        {
            dbContext.Characters.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
