using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.Domain.Characters;
using JXServer.Domain.SessionLeases;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.Items;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JXServer.Integration.Tests;

/// <summary>
/// End-to-end integration tests asserting that CharacterItem.IsBroken and EnchaseLevel persist
/// and rehydrate accurately through EF Core and SQLite, specifically targeting EfCoreCharacterRepository.UpdateAsync
/// cloning and SessionLeaseAppService world snapshot commits.
/// </summary>
public sealed class CharacterItemPersistenceIntegrationTests
{
    private static Character CreateBaseCharacter(Guid characterId, Guid accountId, string roleName)
    {
        return new Character(
            id: characterId,
            accountId: accountId,
            slotIndex: 0,
            roleName: roleName,
            gender: CharacterGender.Male,
            series: ElementSeries.Metal,
            mapId: 53,
            revivalMapId: 53,
            revivalPointId: 19,
            mapMpsX: 52000,
            mapMpsY: 101696,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            lucky: 10,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300);
    }

    private static CharacterItem CreateItem(
        Guid id,
        Guid characterId,
        ItemContainerPlace place,
        byte gridX,
        byte gridY,
        bool isBroken,
        byte enchaseLevel)
    {
        return new CharacterItem(
            id: id,
            characterId: characterId,
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Purple,
            randomSeed: 12345u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 0,
            currentDurability: isBroken ? (ushort)0 : (ushort)100,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: place,
            gridX: gridX,
            gridY: gridY,
            bindState: ItemBindState.Unbound,
            expireTimeUnixSeconds: 0,
            creatorName: "ForensicTest",
            magicAttributes: [new ItemMagicAttributeValue(0, 85, 100, 0, 0, enchaseLevel)],
            isBroken: isBroken,
            enchaseLevel: enchaseLevel);
    }

    [Fact]
    public async Task CharacterItem_WithIsBrokenAndEnchaseLevel_ShouldRoundtripThroughDatabase()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        Guid brokenEquipId = Guid.NewGuid();
        Guid enchasedBagId = Guid.NewGuid();
        Guid unbrokenNormalId = Guid.NewGuid();

        var character = CreateBaseCharacter(characterId, accountId, "Độc Cô Kiếm");

        var itemBroken = CreateItem(brokenEquipId, characterId, ItemContainerPlace.Equip, (byte)EquipmentPartKind.Weapon, 0, isBroken: true, enchaseLevel: 7);
        var itemEnchased = CreateItem(enchasedBagId, characterId, ItemContainerPlace.EquipRoom, 0, 0, isBroken: false, enchaseLevel: 10);
        var itemNormal = CreateItem(unbrokenNormalId, characterId, ItemContainerPlace.EquipRoom, 2, 0, isBroken: false, enchaseLevel: 0);

        character.UpdateWorldSnapshot(
            mapId: 53,
            mpsX: 52000,
            mpsY: 101696,
            currentLife: 1000,
            currentMana: 500,
            currentStamina: 300,
            level: 10,
            experience: 0,
            leadExperience: 0,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            unallocatedAttributePoints: 0,
            unallocatedSkillPoints: 0,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300,
            inventoryMoney: 0,
            repositoryMoney: 0,
            pkState: PlayerPkMode.Peace,
            pkValue: 0,
            activeAuraSkillId: 0,
            skills: null,
            items: [itemBroken, itemEnchased, itemNormal]);

        // Insert into SQLite
        await charRepo.InsertAsync(character, autoSave: true);

        // Rehydrate in fresh query
        Character? loaded = await charRepo.FindAsync(characterId);
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.Items.Count);

        CharacterItem loadedBroken = Assert.Single(loaded.Items, i => i.Id == brokenEquipId);
        Assert.True(loadedBroken.IsBroken, "Persisted item must retain IsBroken=true upon database reload.");
        Assert.Equal((byte)7, loadedBroken.EnchaseLevel);

        CharacterItem loadedEnchased = Assert.Single(loaded.Items, i => i.Id == enchasedBagId);
        Assert.False(loadedEnchased.IsBroken);
        Assert.Equal((byte)10, loadedEnchased.EnchaseLevel);

        CharacterItem loadedNormal = Assert.Single(loaded.Items, i => i.Id == unbrokenNormalId);
        Assert.False(loadedNormal.IsBroken);
        Assert.Equal((byte)0, loadedNormal.EnchaseLevel);
    }

    [Fact]
    public async Task ApplyWorldSnapshot_WithBrokenAndEnchasedItems_ShouldPersistCorrectlyAcrossEvictions()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var leaseAppService = harness.Services.GetRequiredService<ISessionLeaseAppService>();
        var leaseRepo = harness.Services.GetRequiredService<ISessionLeaseRepository>();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        var character = CreateBaseCharacter(characterId, accountId, "Kiếm Thế Vô Song");
        await charRepo.InsertAsync(character, autoSave: true);

        // Setup active lease epoch 1
        var lease = new SessionLease(accountId, "127.0.0.1", 1, DateTimeOffset.UtcNow);
        lease.BeginGatewaySession(1, DateTimeOffset.UtcNow);
        Guid ticket = Guid.NewGuid();
        lease.IssueWorldHandoffTicket(characterId, "Kiếm Thế Vô Song", 53, ticket, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        lease.ConsumeWorldHandoffTicket(ticket, DateTimeOffset.UtcNow, "gs-conn-test");
        await leaseRepo.UpsertAsync(lease);

        // Worker flushes world snapshot on logout with broken & enchased weapon
        Guid weaponId = Guid.NewGuid();
        var snapshotDto = new UpdateCharacterWorldSnapshotDto(
            CharacterId: characterId,
            MapId: 53,
            MapMpsX: 52100,
            MapMpsY: 101700,
            CurrentLife: 800,
            CurrentMana: 400,
            CurrentStamina: 200,
            LeaseEpoch: 1,
            Items:
            [
                new CharacterItemDto(
                    Id: weaponId,
                    Genre: ItemGenreKind.Equipment,
                    DetailType: 0,
                    ParticularType: 1,
                    Level: 1,
                    Series: ElementSeries.Metal,
                    Quality: ItemQualityKind.Purple,
                    RandomSeed: 888u,
                    GeneratorVersion: ItemConsts.GeneratorVersion,
                    Luck: 10,
                    CurrentDurability: 0,
                    MaxDurability: 100,
                    StackCount: 1,
                    ContainerPlace: ItemContainerPlace.Equip,
                    GridX: (byte)EquipmentPartKind.Weapon,
                    GridY: 0,
                    MagicAttributes: [new CharacterItemMagicAttributeDto(0, 85, 100, 0, 0, 10)],
                    BindState: ItemBindState.Unbound,
                    ExpireTimeUnixSeconds: 0,
                    CreatorName: "MasterSmith",
                    IsBroken: true,
                    EnchaseLevel: 10)
            ]);

        var releaseInput = new ReleaseSessionLeaseInput(
            AccountId: accountId,
            LeaseEpoch: 1,
            CharacterSnapshot: snapshotDto);

        // Commit snapshot via lease app service
        await leaseAppService.CompleteLogoutOrEvictAsync(releaseInput);

        // Verify SQLite database
        Character? refreshed = await charRepo.FindAsync(characterId);
        Assert.NotNull(refreshed);
        CharacterItem savedWeapon = Assert.Single(refreshed!.Items, i => i.Id == weaponId);

        Assert.True(savedWeapon.IsBroken, "SessionLeaseAppService snapshot flush must persist IsBroken=true to database.");
        Assert.Equal((byte)10, savedWeapon.EnchaseLevel);
        Assert.Equal("MasterSmith", savedWeapon.CreatorName);
    }

    [Fact]
    public async Task EfCoreCharacterRepository_UpdateAsync_ClonedEntities_MustNotDrop_IsBroken_Or_EnchaseLevel()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        Guid weaponId = Guid.NewGuid();

        var character = CreateBaseCharacter(characterId, accountId, "Vô Danh Tăng");
        var initialItem = CreateItem(weaponId, characterId, ItemContainerPlace.Equip, (byte)EquipmentPartKind.Weapon, 0, isBroken: false, enchaseLevel: 0);

        character.UpdateWorldSnapshot(
            mapId: 53,
            mpsX: 52000,
            mpsY: 101696,
            currentLife: 1000,
            currentMana: 500,
            currentStamina: 300,
            level: 10,
            experience: 0,
            leadExperience: 0,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            unallocatedAttributePoints: 0,
            unallocatedSkillPoints: 0,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300,
            inventoryMoney: 0,
            repositoryMoney: 0,
            pkState: PlayerPkMode.Peace,
            pkValue: 0,
            activeAuraSkillId: 0,
            skills: null,
            items: [initialItem]);

        await charRepo.InsertAsync(character, autoSave: true);

        // Fetch character, abrade weapon to broken and upgrade enchase level to 9
        Character? existing = await charRepo.FindAsync(characterId);
        Assert.NotNull(existing);

        var updatedItem = CreateItem(weaponId, characterId, ItemContainerPlace.Equip, (byte)EquipmentPartKind.Weapon, 0, isBroken: true, enchaseLevel: 9);
        existing!.UpdateWorldSnapshot(
            mapId: existing.MapId,
            mpsX: existing.MapMpsX,
            mpsY: existing.MapMpsY,
            currentLife: existing.CurrentLife,
            currentMana: existing.CurrentMana,
            currentStamina: existing.CurrentStamina,
            level: existing.Level,
            experience: existing.Experience,
            leadExperience: existing.LeadExperience,
            strength: existing.Strength,
            dexterity: existing.Dexterity,
            vitality: existing.Vitality,
            energy: existing.Energy,
            unallocatedAttributePoints: existing.UnallocatedAttributePoints,
            unallocatedSkillPoints: existing.UnallocatedSkillPoints,
            maxLife: existing.MaxLife,
            maxMana: existing.MaxMana,
            maxStamina: existing.MaxStamina,
            inventoryMoney: existing.InventoryMoney,
            repositoryMoney: existing.RepositoryMoney,
            pkState: existing.PkState,
            pkValue: existing.PkValue,
            activeAuraSkillId: existing.ActiveAuraSkillId,
            skills: existing.Skills,
            items: [updatedItem]);

        // EfCoreCharacterRepository.UpdateAsync executes the cloning block
        await charRepo.UpdateAsync(existing, autoSave: true);

        // Re-read from database
        Character? refreshed = await charRepo.FindAsync(characterId);
        Assert.NotNull(refreshed);
        CharacterItem savedItem = Assert.Single(refreshed!.Items, i => i.Id == weaponId);

        Assert.True(savedItem.IsBroken, "EfCoreCharacterRepository.UpdateAsync must preserve IsBroken=true during entity cloning.");
        Assert.Equal((byte)9, savedItem.EnchaseLevel);
    }

    [Fact]
    public async Task EnchaseLevel_AllBoundaryTiers0Through10_ShouldPersistAccurately()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        var character = CreateBaseCharacter(characterId, accountId, "Luyện Đan Sư");

        var items = new List<CharacterItem>();
        for (byte lvl = 0; lvl <= ItemConsts.MaxEnchaseLevel; lvl++)
        {
            items.Add(CreateItem(Guid.NewGuid(), characterId, ItemContainerPlace.RepositoryRoom, (byte)(lvl % 10), (byte)(lvl / 10), isBroken: (lvl % 2 != 0), enchaseLevel: lvl));
        }

        character.UpdateWorldSnapshot(
            mapId: 53,
            mpsX: 52000,
            mpsY: 101696,
            currentLife: 1000,
            currentMana: 500,
            currentStamina: 300,
            level: 10,
            experience: 0,
            leadExperience: 0,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            unallocatedAttributePoints: 0,
            unallocatedSkillPoints: 0,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300,
            inventoryMoney: 0,
            repositoryMoney: 0,
            pkState: PlayerPkMode.Peace,
            pkValue: 0,
            activeAuraSkillId: 0,
            skills: null,
            items: items);

        await charRepo.InsertAsync(character, autoSave: true);

        Character? loaded = await charRepo.FindAsync(characterId);
        Assert.NotNull(loaded);
        Assert.Equal(11, loaded!.Items.Count);

        for (byte lvl = 0; lvl <= ItemConsts.MaxEnchaseLevel; lvl++)
        {
            byte targetLvl = lvl;
            CharacterItem item = Assert.Single(loaded.Items, i => i.EnchaseLevel == targetLvl);
            Assert.Equal(targetLvl % 2 != 0, item.IsBroken);
        }
    }
}
