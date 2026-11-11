# Forensic Audit Remediation: Automated Regression Test Blueprint for CharacterItem Persistence

**Task**: Explorer 3 — Design comprehensive automated regression tests for `CharacterItem.IsBroken` and `EnchaseLevel` persistence, snapshot parity, and EF Core roundtripping.  
**Target Solution**: `d:\Projects\jx-server\JXServer.slnx`  
**State Folder**: `d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_3\`  
**Auditor Report**: `d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md`  

---

## 1. Observation

### 1.1 Constructor Parameter Omission in `CharacterItem.cs`
In `d:\Projects\jx-server\src\JXServer.Domain\Characters\CharacterItem.cs`:
- Signature (lines 40–41):
  ```csharp
  bool isBroken = false,
  byte enchaseLevel = 0)
  ```
- Validation (lines 43–49):
  ```csharp
  if (enchaseLevel > ItemConsts.MaxEnchaseLevel)
  {
      throw new ArgumentOutOfRangeException(
          nameof(enchaseLevel),
          enchaseLevel,
          $"EnchaseLevel must be between 0 and {ItemConsts.MaxEnchaseLevel}.");
  }
  ```
- Assignment block (lines 51–76):
  ```csharp
  Id = id;
  CharacterId = characterId;
  Genre = genre;
  DetailType = detailType;
  ParticularType = particularType;
  Level = level;
  Series = series;
  Quality = quality;
  RandomSeed = randomSeed;
  GeneratorVersion = generatorVersion;
  Luck = luck;
  CurrentDurability = currentDurability;
  MaxDurability = maxDurability;
  StackCount = stackCount;
  ContainerPlace = containerPlace;
  GridX = gridX;
  GridY = gridY;
  BindState = bindState;
  ExpireTimeUnixSeconds = expireTimeUnixSeconds;
  CreatorName = creatorName ?? string.Empty;

  if (magicAttributes is not null)
  {
      _magicAttributes.AddRange(magicAttributes);
  }
  // VERBATIM DEFECT: IsBroken = isBroken; and EnchaseLevel = enchaseLevel; are absent!
  ```
- Mutator methods (lines 124–140):
  ```csharp
  public void SetBroken(bool isBroken)
  {
      IsBroken = isBroken;
  }

  public void SetEnchaseLevel(byte enchaseLevel)
  {
      if (enchaseLevel > ItemConsts.MaxEnchaseLevel)
      {
          throw new ArgumentOutOfRangeException(
              nameof(enchaseLevel),
              enchaseLevel,
              $"EnchaseLevel must be between 0 and {ItemConsts.MaxEnchaseLevel}.");
      }

      EnchaseLevel = enchaseLevel;
  }
  ```

### 1.2 Downstream Silent Persistence Drop in Application & Repository Layers
1. **World Snapshot Application** (`src/JXServer.Application/Characters/CharacterAppService.cs` lines 251–280):
   ```csharp
   var mappedItems = snap.Items?.Select(i => new CharacterItem(
       i.Id == Guid.Empty ? Guid.NewGuid() : i.Id,
       character.Id,
       ...
       i.IsBroken,
       i.EnchaseLevel)).ToList();
   ```
   When `ApplyWorldSnapshot` processes `UpdateCharacterWorldSnapshotDto`, `mappedItems` are instantiated using the flawed constructor. Every item immediately loses `IsBroken` (reset to `false`) and `EnchaseLevel` (reset to `0`).

2. **Entity Repository Updates** (`src/JXServer.Infrastructure/Characters/EfCoreCharacterRepository.cs` lines 101–125):
   ```csharp
   var cloned = new CharacterItem(
       item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
       character.Id,
       item.Genre,
       ...
       item.IsBroken,
       item.EnchaseLevel);
   freshItems.Add(cloned);
   await dbContext.CharacterItems.AddAsync(cloned, cancellationToken);
   ```
   During `UpdateAsync`, existing items are purged from `DbContext.CharacterItems` and re-added as `cloned` instances. Because `cloned` invokes the flawed constructor, all updated items written to the database have `IsBroken = 0` and `EnchaseLevel = 0`.

### 1.3 Gameplay Consequence in `ZoneEngine`
In `src/JXServer.ZoneEngine/Items/PlayerInventoryGrid.cs`:
- Line 383: `if (!IsMatchingEquipmentSlot(item, part) || item.IsBroken) return false;`
- Line 474: `if (equip is null || equip.IsBroken) continue;`
- Line 491, 495: `!itemA.IsBroken && FiveElementsSeriesMatrix.Generates(...)`
If an item's broken status resets to `false` upon world snapshot re-entry or database reload, a broken item regains full stats, allows equipping, and reactivates Five-Elements hidden affixes, breaking core game balance.

### 1.4 In-Memory Runtime Representation & Durability Zero-Detection
In `src/JXServer.ZoneEngine/Items/PlayerItemSlot.cs`:
- Line 133: `IsBroken = isBroken || (maxDurability > 0 && currentDurability == 0);`
- Line 134: `EnchaseLevel = enchaseLevel;`
- Line 227–228: `ToCharacterItemDto()` exports `IsBroken: IsBroken, EnchaseLevel: EnchaseLevel`.

### 1.5 Database Schema Mapping
In `src/JXServer.Infrastructure/EntityFrameworkCore/JXServerDbContext.cs` lines 143–150:
```csharp
builder.Property(x => x.IsBroken)
    .IsRequired()
    .HasDefaultValue(false);

builder.Property(x => x.EnchaseLevel)
    .IsRequired()
    .HasDefaultValue((byte)0);
```
The EF Core model configuration is already mapped and valid for both SQLite and PostgreSQL. The failure was strictly an entity constructor bug unconstrained by automated tests.

### 1.6 Current Test Suite Blindspot
Executing `git grep -n "IsBroken" tests/` returns **0 occurrences**.  
Executing `git grep -n "EnchaseLevel" tests/` returns **0 occurrences**.  
The existing 119 tests (59 in `Protocol.Tests`, 42 in `ZoneEngine.Tests`, 18 in `Integration.Tests`) completely bypassed validating `CharacterItem.IsBroken` and `EnchaseLevel`.

---

## 2. Logic Chain

1. **Defect Etiology**:
   - `CharacterItem.cs` declared `isBroken` and `enchaseLevel` parameters with validation for `enchaseLevel <= ItemConsts.MaxEnchaseLevel` (10).
   - The developer implemented validation and parameter declaration but missed the two property assignments (`IsBroken = isBroken; EnchaseLevel = enchaseLevel;`).
   - Because the properties had default values (`false` and `0`), no compiler error or runtime exception was triggered.

2. **Propagation Chain**:
   - When a player's gear breaks in `ZoneEngine` (`CurrentDurability == 0` -> `PlayerItemSlot.IsBroken == true`), or when gear is enchased (`PlayerItemSlot.EnchaseLevel == 1..10`), `ToCharacterItemDto()` serializes these values correctly into `CharacterItemDto`.
   - When the worker flushes the world snapshot via `SessionLeaseAppService.CompleteLogoutOrEvictAsync` or `CharacterAppService.ApplyWorldSnapshot`, `snap.Items.Select(i => new CharacterItem(..., i.IsBroken, i.EnchaseLevel))` is called.
   - The constructor dropped both parameters. The resulting `CharacterItem` aggregate items had `IsBroken = false` and `EnchaseLevel = 0`.
   - `EfCoreCharacterRepository.UpdateAsync` cloned these entities with the same broken constructor, inserting rows with default values into the database.
   - Upon next login, the player's broken items were magically repaired and all Purple Huyền Tinh enchasing tiers were erased.

3. **Remediation Strategy**:
   - Remediation requires a 2-line fix in `src/JXServer.Domain/Characters/CharacterItem.cs`:
     ```csharp
     IsBroken = isBroken;
     EnchaseLevel = enchaseLevel;
     ```
   - To make this permanently immune to regression, we design a 3-tier automated test suite:
     1. **Domain Unit Tests** (`tests/JXServer.ZoneEngine.Tests/CharacterItemUnitTests.cs`): Direct constructor parameter testing, mutator tests, boundary tests (`0..10` vs `11`), durability zero-detection in `PlayerItemSlot`, and `PlayerInventoryGrid` broken-item equip/stat suppression.
     2. **Snapshot Parity Tests** (`tests/JXServer.ZoneEngine.Tests/ItemSnapshotParityTests.cs`): Roundtrip conversion across `PlayerItemSlot` <-> `CharacterItemDto` <-> `CharacterItem`.
     3. **EF Core Integration Tests** (`tests/JXServer.Integration.Tests/CharacterItemPersistenceIntegrationTests.cs`): Real SQLite database insert, query rehydration, repository update cloning, boundary tiers `0..10`, and end-to-end `SessionLeaseAppService` world snapshot commits.

---

## 3. Caveats

- **Scope & Write Discipline**: In compliance with the Teamwork Explorer role constraints, this report contains pure design and test code blueprints. No files in `src/` or `tests/` were altered.
- **Database Engine Parity**: `IntegrationTestServerHarness` uses in-memory SQLite (`mode=memory&cache=shared`). SQLite stores booleans as integer 0/1 and byte/ushort as integers. Test assertions verify type compatibility in both EF Core high-level entities and raw SQL queries.

---

## 4. Conclusion & Test Suite Implementation Blueprint

The test suite must be implemented in the following 3 files:

### File 1: `tests/JXServer.ZoneEngine.Tests/CharacterItemUnitTests.cs`
**Purpose**: High-speed unit tests covering domain entity invariants, property mutators, parameter bounds, durability zero-detection, and inventory gameplay impacts.

```csharp
using System;
using JXServer.Domain.Characters;
using JXServer.GameAssets.Items;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.PlayerSessions;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

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
    public void Constructor_WithExplicitBrokenAndEnchaseLevel_ShouldAssignPropertiesDirectly()
    {
        // Act: Instantiate with IsBroken=true, EnchaseLevel=7
        CharacterItem item = CreateItem(isBroken: true, enchaseLevel: 7);

        // Assert: Must not be dropped by facade constructor
        Assert.True(item.IsBroken, "CharacterItem.IsBroken must match constructor parameter isBroken=true.");
        Assert.Equal((byte)7, item.EnchaseLevel);
    }

    [Fact]
    public void Constructor_DefaultOptionalParameters_ShouldDefaultToUnbrokenAndZeroEnchase()
    {
        // Act: Use default optional arguments (isBroken = false, enchaseLevel = 0)
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

        // Assert
        Assert.False(item.IsBroken);
        Assert.Equal((byte)0, item.EnchaseLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Constructor_EnchaseLevel_ValidTiers0Through10_ShouldAssignSuccessfully(byte validTier)
    {
        CharacterItem item = CreateItem(enchaseLevel: validTier);
        Assert.Equal(validTier, item.EnchaseLevel);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(255)]
    public void Constructor_EnchaseLevel_ExceedingMax10_ShouldThrowArgumentOutOfRangeException(byte invalidTier)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CreateItem(enchaseLevel: invalidTier));
        Assert.Equal("enchaseLevel", ex.ParamName);
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
    public void SetEnchaseLevel_ValidAndInvalidTiers_ShouldMutateOrThrow()
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
    public void PlayerItemSlot_DurabilityZeroDetection_ShouldForceIsBrokenTrue()
    {
        // When maxDurability > 0 and currentDurability == 0, IsBroken must be true even if isBroken: false passed
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
            currentDurability: 0, // Zero durability
            maxDurability: 100,  // Max > 0
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            isBroken: false,     // Intentionally pass false
            enchaseLevel: 4);

        Assert.True(slot.IsBroken, "PlayerItemSlot must evaluate IsBroken=true when CurrentDurability reaches 0.");
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
    public void PlayerItemSlot_ZeroMaxDurability_NonEquipment_ShouldNotAutoBreak()
    {
        var slotConsumable = new PlayerItemSlot(
            runtimeItemId: 104u,
            domainItemId: Guid.NewGuid(),
            template: DummyEquipmentTemplate,
            genre: ItemGenreKind.TownPortal,
            detailType: 0,
            particularType: 0,
            level: 1,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 1u,
            generatorVersion: 1,
            luck: 0,
            currentDurability: 0,
            maxDurability: 0, // Consumables have 0 max durability
            stackCount: 5,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            isBroken: false);

        Assert.False(slotConsumable.IsBroken);
    }

    [Fact]
    public void PlayerInventoryGrid_BrokenItem_CannotBeEquipped()
    {
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
            outboundSink: new RecordingOutboundSink());

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

        // Assert: Broken weapon cannot be equipped via CanEquipItem
        bool canEquip = PlayerInventoryGrid.CanEquipItem(player, brokenSword, EquipmentPartKind.Weapon);
        Assert.False(canEquip, "PlayerInventoryGrid.CanEquipItem must reject items with IsBroken=true.");
    }
}
```

---

### File 2: `tests/JXServer.ZoneEngine.Tests/ItemSnapshotParityTests.cs`
**Purpose**: Snapshot parity tests verifying seamless translation between `PlayerItemSlot`, `CharacterItemDto`, `CharacterItem`, and `SessionLeaseAppService` handoff tickets.

```csharp
using System;
using System.Collections.Generic;
using JXServer.Contracts.Characters;
using JXServer.Domain.Characters;
using JXServer.GameAssets.Items;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.Items;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

/// <summary>
/// Regression tests verifying bidirectional conversion parity for IsBroken and EnchaseLevel
/// across runtime slots (PlayerItemSlot), network/service DTOs (CharacterItemDto), and domain entities (CharacterItem).
/// </summary>
public sealed class ItemSnapshotParityTests
{
    private static readonly ItemTemplate DummyTemplate = new(
        Id: 8001,
        Code: "ao-giap-test",
        Name: "Áo Giáp Test",
        Genre: ItemGenreKind.Equipment,
        Quality: ItemQualityKind.Purple,
        DetailType: 2, // Body armor
        ParticularType: 1,
        Level: 3,
        ElementSeries: ElementSeries.Fire,
        GridWidthCells: 2,
        GridHeightCells: 3,
        ObjDataId: 2,
        AppearanceResId: 2,
        PriceGold: 100,
        MaxStackCount: 1,
        BaseAttributes: [],
        Requirements: []);

    [Fact]
    public void PlayerItemSlot_ToCharacterItemDto_ShouldPreserve_IsBroken_And_EnchaseLevel()
    {
        var affixes = new[]
        {
            new ItemMagicAttributeSlot(0, 85, 100, 0, 0, 7),
            new ItemMagicAttributeSlot(1, 103, 30, 0, 0, 7),
        };

        var slot = new PlayerItemSlot(
            runtimeItemId: 501u,
            domainItemId: Guid.NewGuid(),
            template: DummyTemplate,
            genre: ItemGenreKind.Equipment,
            detailType: 2,
            particularType: 1,
            level: 3,
            series: ElementSeries.Fire,
            quality: ItemQualityKind.Purple,
            randomSeed: 9999u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 5,
            currentDurability: 0, // Broken
            maxDurability: 80,
            stackCount: 1,
            containerPlace: ItemContainerPlace.Equip,
            gridX: (byte)EquipmentPartKind.Body,
            gridY: 0,
            magicAttributes: affixes,
            isBroken: true,
            enchaseLevel: 8);

        CharacterItemDto dto = slot.ToCharacterItemDto();

        Assert.Equal(slot.DomainItemId, dto.Id);
        Assert.True(dto.IsBroken);
        Assert.Equal((byte)8, dto.EnchaseLevel);
        Assert.Equal(2, dto.MagicAttributes.Count);
        Assert.Equal((byte)7, dto.MagicAttributes[0].EnchaseLevel);
    }

    [Fact]
    public void DtoToDomainEntity_ShouldMaintainExact_IsBroken_And_EnchaseLevel()
    {
        var dto = new CharacterItemDto(
            Id: Guid.NewGuid(),
            Genre: ItemGenreKind.Equipment,
            DetailType: 2,
            ParticularType: 1,
            Level: 3,
            Series: ElementSeries.Fire,
            Quality: ItemQualityKind.Purple,
            RandomSeed: 9999u,
            GeneratorVersion: ItemConsts.GeneratorVersion,
            Luck: 5,
            CurrentDurability: 0,
            MaxDurability: 80,
            StackCount: 1,
            ContainerPlace: ItemContainerPlace.Equip,
            GridX: (byte)EquipmentPartKind.Body,
            GridY: 0,
            MagicAttributes:
            [
                new CharacterItemMagicAttributeDto(0, 85, 100, 0, 0, 9),
            ],
            BindState: ItemBindState.BindOnEquip,
            ExpireTimeUnixSeconds: 0,
            CreatorName: "Blacksmith",
            IsBroken: true,
            EnchaseLevel: 9);

        // Simulate CharacterAppService.ApplyWorldSnapshot mapping
        var entity = new CharacterItem(
            id: dto.Id,
            characterId: Guid.NewGuid(),
            genre: dto.Genre,
            detailType: dto.DetailType,
            particularType: dto.ParticularType,
            level: dto.Level,
            series: dto.Series,
            quality: dto.Quality,
            randomSeed: dto.RandomSeed,
            generatorVersion: dto.GeneratorVersion,
            luck: dto.Luck,
            currentDurability: dto.CurrentDurability,
            maxDurability: dto.MaxDurability,
            stackCount: dto.StackCount,
            containerPlace: dto.ContainerPlace,
            gridX: dto.GridX,
            gridY: dto.GridY,
            bindState: dto.BindState,
            expireTimeUnixSeconds: dto.ExpireTimeUnixSeconds,
            creatorName: dto.CreatorName,
            magicAttributes: [new ItemMagicAttributeValue(0, 85, 100, 0, 0, 9)],
            isBroken: dto.IsBroken,
            enchaseLevel: dto.EnchaseLevel);

        Assert.True(entity.IsBroken, "Domain entity CharacterItem.IsBroken must be true after mapping from DTO.");
        Assert.Equal((byte)9, entity.EnchaseLevel);
    }

    [Fact]
    public void FullRoundtrip_RuntimeToDtoToDomainToDtoToRuntime_MustHaveZeroLoss()
    {
        Guid itemId = Guid.NewGuid();
        var originalSlot = new PlayerItemSlot(
            runtimeItemId: 777u,
            domainItemId: itemId,
            template: DummyTemplate,
            genre: ItemGenreKind.Equipment,
            detailType: 2,
            particularType: 1,
            level: 3,
            series: ElementSeries.Fire,
            quality: ItemQualityKind.Purple,
            randomSeed: 555u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 10,
            currentDurability: 25,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 2,
            gridY: 3,
            magicAttributes: [new ItemMagicAttributeSlot(0, 10, 50, 0, 0, 6)],
            isBroken: true,
            enchaseLevel: 6);

        // 1. Runtime -> DTO
        CharacterItemDto exportedDto = originalSlot.ToCharacterItemDto();

        // 2. DTO -> Domain Entity (ApplyWorldSnapshot)
        var domainItem = new CharacterItem(
            id: exportedDto.Id,
            characterId: Guid.NewGuid(),
            genre: exportedDto.Genre,
            detailType: exportedDto.DetailType,
            particularType: exportedDto.ParticularType,
            level: exportedDto.Level,
            series: exportedDto.Series,
            quality: exportedDto.Quality,
            randomSeed: exportedDto.RandomSeed,
            generatorVersion: exportedDto.GeneratorVersion,
            luck: exportedDto.Luck,
            currentDurability: exportedDto.CurrentDurability,
            maxDurability: exportedDto.MaxDurability,
            stackCount: exportedDto.StackCount,
            containerPlace: exportedDto.ContainerPlace,
            gridX: exportedDto.GridX,
            gridY: exportedDto.GridY,
            bindState: exportedDto.BindState,
            expireTimeUnixSeconds: exportedDto.ExpireTimeUnixSeconds,
            creatorName: exportedDto.CreatorName,
            magicAttributes: [new ItemMagicAttributeValue(0, 10, 50, 0, 0, 6)],
            isBroken: exportedDto.IsBroken,
            enchaseLevel: exportedDto.EnchaseLevel);

        // 3. Domain Entity -> DTO (SessionLeaseAppService handoff ticket creation)
        var handoffDto = new CharacterItemDto(
            Id: domainItem.Id,
            Genre: domainItem.Genre,
            DetailType: domainItem.DetailType,
            ParticularType: domainItem.ParticularType,
            Level: domainItem.Level,
            Series: domainItem.Series,
            Quality: domainItem.Quality,
            RandomSeed: domainItem.RandomSeed,
            GeneratorVersion: domainItem.GeneratorVersion,
            Luck: domainItem.Luck,
            CurrentDurability: domainItem.CurrentDurability,
            MaxDurability: domainItem.MaxDurability,
            StackCount: domainItem.StackCount,
            ContainerPlace: domainItem.ContainerPlace,
            GridX: domainItem.GridX,
            GridY: domainItem.GridY,
            MagicAttributes: [new CharacterItemMagicAttributeDto(0, 10, 50, 0, 0, domainItem.MagicAttributes[0].EnchaseLevel)],
            BindState: domainItem.BindState,
            ExpireTimeUnixSeconds: domainItem.ExpireTimeUnixSeconds,
            CreatorName: domainItem.CreatorName,
            IsBroken: domainItem.IsBroken,
            EnchaseLevel: domainItem.EnchaseLevel);

        // 4. DTO -> Runtime Slot (MapZoneWorker.CreatePlayerItemSlotFromDto)
        var rehydratedSlot = new PlayerItemSlot(
            runtimeItemId: 888u,
            domainItemId: handoffDto.Id,
            template: DummyTemplate,
            genre: handoffDto.Genre,
            detailType: handoffDto.DetailType,
            particularType: handoffDto.ParticularType,
            level: handoffDto.Level,
            series: handoffDto.Series,
            quality: handoffDto.Quality,
            randomSeed: handoffDto.RandomSeed,
            generatorVersion: handoffDto.GeneratorVersion,
            luck: handoffDto.Luck,
            currentDurability: handoffDto.CurrentDurability,
            maxDurability: handoffDto.MaxDurability,
            stackCount: handoffDto.StackCount,
            containerPlace: handoffDto.ContainerPlace,
            gridX: handoffDto.GridX,
            gridY: handoffDto.GridY,
            magicAttributes: [new ItemMagicAttributeSlot(0, 10, 50, 0, 0, handoffDto.MagicAttributes[0].EnchaseLevel)],
            isBroken: handoffDto.IsBroken,
            enchaseLevel: handoffDto.EnchaseLevel);

        // Verify total fidelity
        Assert.Equal(originalSlot.DomainItemId, rehydratedSlot.DomainItemId);
        Assert.Equal(originalSlot.IsBroken, rehydratedSlot.IsBroken);
        Assert.Equal(originalSlot.EnchaseLevel, rehydratedSlot.EnchaseLevel);
        Assert.Equal(originalSlot.CurrentDurability, rehydratedSlot.CurrentDurability);
        Assert.Equal(originalSlot.MagicAttributes[0].EnchaseLevel, rehydratedSlot.MagicAttributes[0].EnchaseLevel);
    }
}
```

---

### File 3: `tests/JXServer.Integration.Tests/CharacterItemPersistenceIntegrationTests.cs`
**Purpose**: Full-stack EF Core SQLite integration tests asserting database table insertion, rehydration, entity cloning inside `EfCoreCharacterRepository.UpdateAsync`, boundary tiers `0..10`, and world snapshot commits via `ISessionLeaseAppService`.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.Domain.Characters;
using JXServer.Domain.SessionLeases;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using Microsoft.Data.Sqlite;
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
    public async Task EfCoreDirectInsertAndQuery_ShouldRoundtrip_IsBroken_And_EnchaseLevel()
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

        // EfCoreCharacterRepository.UpdateAsync executes the cloning block (lines 101-125)
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
            items.Add(CreateItem(Guid.NewGuid(), characterId, ItemContainerPlace.Repository, (byte)(lvl % 10), (byte)(lvl / 10), isBroken: (lvl % 2 != 0), enchaseLevel: lvl));
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

    [Fact]
    public async Task SessionLease_CompleteLogoutOrEvictAsync_WorldSnapshotWithBrokenItems_MustPersistToDatabase()
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
}
```

---

## 5. Verification Method

### 5.1 Verification of Defect (Before Remediation)
Run the following PowerShell verification script against the pre-remediation workspace:
```powershell
pwsh -Command @"
Add-Type -Path 'd:\Projects\jx-server\src\JXServer.Primitives\bin\Debug\net10.0\JXServer.Primitives.dll'
Add-Type -Path 'd:\Projects\jx-server\src\JXServer.Domain\bin\Debug\net10.0\JXServer.Domain.dll'

`$item = [JXServer.Domain.Characters.CharacterItem]::new(
    [Guid]::NewGuid(), [Guid]::NewGuid(),
    [JXServer.Primitives.Items.ItemGenreKind]::Equipment,
    1, 1, 1, [JXServer.Primitives.Characters.ElementSeries]::Wood,
    [JXServer.Primitives.Items.ItemQualityKind]::Normal,
    12345u, 1us, 0, 100us, 100us, 1us,
    [JXServer.Primitives.Items.ItemContainerPlace]::Equip,
    0, 0, [JXServer.Primitives.Items.ItemBindState]::Unbound,
    0u, 'Master', `$null,
    `$true, # isBroken = true
    [byte]7  # enchaseLevel = 7
)

Write-Host `"Expected: IsBroken=True, EnchaseLevel=7`"
Write-Host `"Actual:   IsBroken=`$(`$item.IsBroken), EnchaseLevel=`$(`$item.EnchaseLevel)`"
if (`$item.IsBroken -ne `$true -or `$item.EnchaseLevel -ne 7) {
    Write-Host 'INTEGRITY VIOLATION DETECTED: Constructor dropped parameters.' -ForegroundColor Red
} else {
    Write-Host 'REMEDIATION VERIFIED: Constructor correctly assigned parameters.' -ForegroundColor Green
}
"@
```
**Current Result**: Fails with `Actual: IsBroken=False, EnchaseLevel=0`.

### 5.2 Verification After Remediation
Once the implementer fixes `src/JXServer.Domain/Characters/CharacterItem.cs` by adding:
```csharp
IsBroken = isBroken;
EnchaseLevel = enchaseLevel;
```
And adds the 3 test files detailed above:

1. **Run Unit Tests**:
   ```powershell
   dotnet test d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj --filter "FullyQualifiedName~CharacterItem|FullyQualifiedName~ItemSnapshot"
   ```
   **Expected**: All unit and snapshot parity tests pass.

2. **Run Integration Tests**:
   ```powershell
   dotnet test d:\Projects\jx-server\tests\JXServer.Integration.Tests\JXServer.Integration.Tests.csproj --filter "FullyQualifiedName~CharacterItemPersistence"
   ```
   **Expected**: All EF Core SQLite database persistence roundtrip tests pass.

3. **Run Full Solution Verification**:
   ```powershell
   dotnet test d:\Projects\jx-server\JXServer.slnx
   ```
   **Expected**: Solution builds cleanly with 0 warnings (`TreatWarningsAsErrors=true`), and all test assemblies pass (119 existing + ~20 new regression tests = ~139 total tests).

4. **Invalidation Conditions**:
   - Any test failure where `item.IsBroken` evaluates to `false` when constructed or snapshotted as `true`.
   - Any test failure where `item.EnchaseLevel` resets to `0` when constructed or persisted with level `1..10`.
   - Any build warning or failure under net10.0.
