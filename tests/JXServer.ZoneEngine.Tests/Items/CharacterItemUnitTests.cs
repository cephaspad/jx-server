using System;
using System.Collections.Generic;
using JXServer.Contracts.Characters;
using JXServer.Domain.Characters;
using JXServer.GameAssets.Items;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.PlayerSessions;
using Xunit;

namespace JXServer.ZoneEngine.Tests.Items;

/// <summary>
/// Unit regression tests for CharacterItem and PlayerItemSlot invariants, specifically verifying
/// that IsBroken and EnchaseLevel are properly assigned, mutated, bounded, and evaluated.
/// </summary>
public sealed class CharacterItemUnitTests
{
    private static readonly ItemTemplate DummyEquipmentTemplate = new(
        Id: 9001,
        Code: "kiem-go-test",
        Name: "Kiếm Gỗ Test",
        Genre: ItemGenreKind.Equipment,
        Quality: ItemQualityKind.Normal,
        DetailType: 0, // Melee weapon
        ParticularType: 1,
        Level: 1,
        ElementSeries: ElementSeries.Metal,
        GridWidthCells: 1,
        GridHeightCells: 2,
        ObjDataId: 1,
        AppearanceResId: 1,
        PriceGold: 10,
        MaxStackCount: 1,
        BaseAttributes:
        [
            new ItemBaseAttributeTemplate(1, "damage_v", 10, 20),
        ],
        Requirements: []);

    private static CharacterItem CreateItem(
        bool isBroken = false,
        byte enchaseLevel = 0,
        ushort currentDurability = 100,
        ushort maxDurability = 100,
        ItemContainerPlace containerPlace = ItemContainerPlace.EquipRoom,
        byte gridX = 0,
        byte gridY = 0)
    {
        return new CharacterItem(
            id: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 42u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 0,
            currentDurability: currentDurability,
            maxDurability: maxDurability,
            stackCount: 1,
            containerPlace: containerPlace,
            gridX: gridX,
            gridY: gridY,
            bindState: ItemBindState.Unbound,
            expireTimeUnixSeconds: 0,
            creatorName: "ForensicAuditor",
            magicAttributes: null,
            isBroken: isBroken,
            enchaseLevel: enchaseLevel);
    }

    [Fact]
    public void Constructor_ShouldAssign_IsBrokenAndEnchaseLevel_Correctly()
    {
        // Act: Instantiate with IsBroken=true, EnchaseLevel=7
        CharacterItem item = CreateItem(isBroken: true, enchaseLevel: 7);

        // Assert: Must not be dropped by constructor
        Assert.True(item.IsBroken, "CharacterItem.IsBroken must match constructor parameter isBroken=true.");
        Assert.Equal((byte)7, item.EnchaseLevel);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(255)]
    public void Constructor_WhenEnchaseLevelExceedsMax_ShouldThrowArgumentOutOfRangeException(byte invalidTier)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CreateItem(enchaseLevel: invalidTier));
        Assert.Equal("enchaseLevel", ex.ParamName);
    }

    [Fact]
    public void SetDurability_WhenDurabilityZero_ShouldMarkIsBroken()
    {
        // Intact item with durability 100/100
        CharacterItem item = CreateItem(isBroken: false, currentDurability: 100, maxDurability: 100);
        Assert.False(item.IsBroken);

        // Abrade to zero durability
        item.SetDurability(0);
        Assert.Equal((ushort)0, item.CurrentDurability);
        Assert.True(item.IsBroken, "SetDurability(0) must automatically mark item as IsBroken = true.");

        // Also verify PlayerItemSlot behavior
        var slot = new PlayerItemSlot(
            runtimeItemId: 100u,
            domainItemId: Guid.NewGuid(),
            template: DummyEquipmentTemplate,
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 123u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 0,
            currentDurability: 100,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            isBroken: false);

        Assert.False(slot.IsBroken);
        slot.SetDurability(0);
        Assert.Equal((ushort)0, slot.CurrentDurability);
        Assert.True(slot.IsBroken, "PlayerItemSlot.SetDurability(0) must mark IsBroken = true.");
    }

    [Fact]
    public void SetEnchaseLevel_ShouldUpdateEnchaseLevel_AndThrowOnOverflow()
    {
        CharacterItem item = CreateItem(enchaseLevel: 0);

        for (byte lvl = 1; lvl <= ItemConsts.MaxEnchaseLevel; lvl++)
        {
            item.SetEnchaseLevel(lvl);
            Assert.Equal(lvl, item.EnchaseLevel);
        }

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => item.SetEnchaseLevel(11));
        Assert.Equal("enchaseLevel", ex.ParamName);
        Assert.Equal(ItemConsts.MaxEnchaseLevel, item.EnchaseLevel);
    }

    [Fact]
    public void Constructor_DefaultOptionalParameters_ShouldDefaultToUnbrokenAndZeroEnchase()
    {
        var item = new CharacterItem(
            id: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 100u,
            generatorVersion: 1,
            luck: 0,
            currentDurability: 50,
            maxDurability: 50,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0);

        Assert.False(item.IsBroken);
        Assert.Equal((byte)0, item.EnchaseLevel);
    }

    [Fact]
    public void SetBroken_ShouldMutatePropertyCorrectly()
    {
        CharacterItem item = CreateItem(isBroken: false);
        Assert.False(item.IsBroken);

        item.SetBroken(true);
        Assert.True(item.IsBroken);

        item.SetBroken(false);
        Assert.False(item.IsBroken);
    }

    [Fact]
    public void PlayerItemSlot_DurabilityZeroDetection_ShouldForceIsBrokenTrue()
    {
        var slot = new PlayerItemSlot(
            runtimeItemId: 101u,
            domainItemId: Guid.NewGuid(),
            template: DummyEquipmentTemplate,
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 123u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 0,
            currentDurability: 0,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            isBroken: false,
            enchaseLevel: 4);

        Assert.True(slot.IsBroken, "PlayerItemSlot must evaluate IsBroken=true when CurrentDurability is 0.");
        Assert.Equal((byte)4, slot.EnchaseLevel);
    }

    [Fact]
    public void PlayerItemSlot_PositiveDurability_ShouldPreserveExplicitIsBrokenFlag()
    {
        var slotBroken = new PlayerItemSlot(
            runtimeItemId: 102u,
            domainItemId: Guid.NewGuid(),
            template: DummyEquipmentTemplate,
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 123u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 0,
            currentDurability: 50,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            isBroken: true,
            enchaseLevel: 2);

        var slotIntact = new PlayerItemSlot(
            runtimeItemId: 103u,
            domainItemId: Guid.NewGuid(),
            template: DummyEquipmentTemplate,
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 123u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 0,
            currentDurability: 50,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            isBroken: false,
            enchaseLevel: 2);

        Assert.True(slotBroken.IsBroken);
        Assert.False(slotIntact.IsBroken);
    }

    [Fact]
    public void PlayerItemSlot_ToCharacterItemDto_AndBack_PreservesIsBrokenAndEnchaseLevel()
    {
        var slot = new PlayerItemSlot(
            runtimeItemId: 501u,
            domainItemId: Guid.NewGuid(),
            template: DummyEquipmentTemplate,
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 9999u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 5,
            currentDurability: 0,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.Equip,
            gridX: 0,
            gridY: 0,
            isBroken: true,
            enchaseLevel: 8);

        Assert.True(slot.IsBroken);
        Assert.Equal((byte)8, slot.EnchaseLevel);

        CharacterItemDto dto = slot.ToCharacterItemDto();
        Assert.True(dto.IsBroken);
        Assert.Equal((byte)8, dto.EnchaseLevel);

        var domainItem = new CharacterItem(
            dto.Id,
            Guid.NewGuid(),
            dto.Genre,
            dto.DetailType,
            dto.ParticularType,
            dto.Level,
            dto.Series,
            dto.Quality,
            dto.RandomSeed,
            dto.GeneratorVersion,
            dto.Luck,
            dto.CurrentDurability,
            dto.MaxDurability,
            dto.StackCount,
            dto.ContainerPlace,
            dto.GridX,
            dto.GridY,
            dto.BindState,
            dto.ExpireTimeUnixSeconds,
            dto.CreatorName,
            null,
            dto.IsBroken,
            dto.EnchaseLevel);

        Assert.True(domainItem.IsBroken);
        Assert.Equal((byte)8, domainItem.EnchaseLevel);
    }

    [Fact]
    public void PlayerInventoryGrid_BrokenItem_CannotBeEquipped()
    {
        var sink = new RecordingOutboundSink();
        var player = new PlayerZoneState(
            runtimeNpcId: 1u,
            accountId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            leaseEpoch: 1L,
            roleNameUnicode: "Hiệp Khách",
            gender: CharacterGender.Male,
            series: ElementSeries.Metal,
            level: 10,
            experience: 0,
            leadExperience: 0,
            mapId: 53,
            currentMpsX: 52000,
            currentMpsY: 101696,
            currentLife: 100,
            maxLife: 100,
            currentMana: 100,
            maxMana: 100,
            currentStamina: 100,
            maxStamina: 100,
            strength: 50,
            dexterity: 50,
            vitality: 50,
            energy: 50,
            lucky: 0,
            unallocatedAttributePoints: 0,
            unallocatedSkillPoints: 0,
            inventoryMoney: 0,
            repositoryMoney: 0,
            currentFactionId: 255,
            firstFactionId: 255,
            factionJoinCount: 0,
            initialTick: 1L,
            outboundSink: sink);

        var brokenSword = new PlayerItemSlot(
            runtimeItemId: 105u,
            domainItemId: Guid.NewGuid(),
            template: DummyEquipmentTemplate,
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 123u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 0,
            currentDurability: 0,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            isBroken: true);

        bool canEquip = PlayerInventoryGrid.CanEquipItem(player, brokenSword, EquipmentPartKind.Weapon);
        Assert.False(canEquip, "PlayerInventoryGrid.CanEquipItem must reject items with IsBroken = true.");
    }
}
