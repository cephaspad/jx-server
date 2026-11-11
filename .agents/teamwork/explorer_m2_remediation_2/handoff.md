# Forensic Audit Remediation Investigation Report (Explorer 2)

**Work Product**: Milestone M2 Forensic Audit Remediation Investigation  
**Author**: Explorer 2 (`explorer_m2_remediation_2`)  
**Target Solution**: `d:\Projects\jx-server\JXServer.slnx`  
**Reference Report**: `d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md`  
**Integrity Mode**: Development (Read-Only Exploration)  
**Status**: INVESTIGATION COMPLETE (Remediation Blueprint Ready)

---

## 1. Observation

### Observation 1.1: Exact Defect in `CharacterItem.cs` Constructor
In `d:\Projects\jx-server\src\JXServer.Domain\Characters\CharacterItem.cs`:
The constructor signature defines parameters `bool isBroken = false` and `byte enchaseLevel = 0` at lines 40–41:
```csharp
Lines 39-41:
        IEnumerable<ItemMagicAttributeValue>? magicAttributes = null,
        bool isBroken = false,
        byte enchaseLevel = 0)
```
Lines 43–49 validate `enchaseLevel`:
```csharp
Lines 43-49:
        if (enchaseLevel > ItemConsts.MaxEnchaseLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enchaseLevel),
                enchaseLevel,
                $"EnchaseLevel must be between 0 and {ItemConsts.MaxEnchaseLevel}.");
        }
```
Lines 51–76 assign all prior properties (`Id`, `CharacterId`, `Genre`, `DetailType`, `ParticularType`, `Level`, `Series`, `Quality`, `RandomSeed`, `GeneratorVersion`, `Luck`, `CurrentDurability`, `MaxDurability`, `StackCount`, `ContainerPlace`, `GridX`, `GridY`, `BindState`, `ExpireTimeUnixSeconds`, `CreatorName`, and `_magicAttributes`), but **completely omit** assigning `isBroken` and `enchaseLevel`:
```csharp
Lines 72-76:
        if (magicAttributes is not null)
        {
            _magicAttributes.AddRange(magicAttributes);
        }
        // MISSING: IsBroken = isBroken;
        // MISSING: EnchaseLevel = enchaseLevel;
    }
```
Properties `IsBroken` (line 118) and `EnchaseLevel` (line 120) have private setters:
```csharp
Lines 118-120:
    public bool IsBroken { get; private set; }

    public byte EnchaseLevel { get; private set; }
```
And mutator methods `SetBroken(bool isBroken)` (lines 124–127) and `SetEnchaseLevel(byte enchaseLevel)` (lines 129–140) exist, but the constructor leaves both properties at default values (`false` and `0`).

### Observation 1.2: Investigation of `PlayerItemSlot` Interactions
In `d:\Projects\jx-server\src\JXServer.ZoneEngine\Items\PlayerItemSlot.cs`:
1. **Property Declarations** (lines 43–46):
   ```csharp
   public bool IsBroken { get; set; }
   public byte EnchaseLevel { get; set; }
   ```
2. **Constructor Initialization** (lines 114–115, 133–134):
   ```csharp
   bool isBroken = false,
   byte enchaseLevel = 0)
   {
       ...
       IsBroken = isBroken || (maxDurability > 0 && currentDurability == 0);
       EnchaseLevel = enchaseLevel;
       ...
   ```
   `PlayerItemSlot` **genuinely assigns** both parameters and additionally enforces the domain rule that an item with 0 durability is marked broken.
3. **Export to `CharacterItemDto`** (lines 206–229):
   ```csharp
   public CharacterItemDto ToCharacterItemDto()
   {
       ...
       return new CharacterItemDto(
           Id: DomainItemId,
           ...
           IsBroken: IsBroken,
           EnchaseLevel: EnchaseLevel);
   }
   ```
   `PlayerItemSlot` correctly preserves both fields when exporting to `CharacterItemDto`.
4. **Gameplay Enforcement in Zone Engine**:
   - In `PlayerInventoryGrid.cs` line 383:
     ```csharp
     public static bool CanEquipItem(PlayerZoneState player, PlayerItemSlot item, EquipmentPartKind part)
     {
         if (!IsMatchingEquipmentSlot(item, part) || item.IsBroken)
         {
             return false;
         }
     ```
     Broken items cannot be equipped.
   - In `PlayerInventoryGrid.cs` lines 474, 491, 495:
     ```csharp
     if (equip is null || equip.IsBroken) { continue; }
     ...
     bool aGeneratesTarget = itemA is not null && !itemA.IsBroken && FiveElementsSeriesMatrix.Generates(itemA.Series, equip.Series);
     ```
     Broken items are excluded from Five-Elements passive activation.

### Observation 1.3: End-to-End Persistence Pipeline Trace
Tracing the complete round-trip across all layers revealed:
1. **Zone World Ingestion** (`MapZoneWorker.cs:2577–2602`):
   ```csharp
   private PlayerItemSlot CreatePlayerItemSlotFromDto(uint runtimeItemId, CharacterItemDto dto) =>
       new(
           ...
           isBroken: dto.IsBroken,
           enchaseLevel: dto.EnchaseLevel);
   ```
   `CharacterItemDto` fields are passed into `PlayerItemSlot` constructor (intact).
2. **Zone World Snapshot Export** (`MapZoneWorker.cs:3273`):
   ```csharp
   Items: state.Inventory.ToCharacterItemDtos(),
   ```
   Calls `slot.ToCharacterItemDto()`, producing `CharacterItemDto` with `IsBroken` and `EnchaseLevel` (intact).
3. **Application Snapshot Application** (`CharacterAppService.cs:251–280`):
   ```csharp
   var mappedItems = snap.Items?.Select(i => new CharacterItem(
       ...
       i.IsBroken,
       i.EnchaseLevel)).ToList();
   ```
   **BREAK OCCURS HERE**: `snap.Items` passes `i.IsBroken` and `i.EnchaseLevel`, but `CharacterItem` drops them.
4. **Infrastructure Persistence** (`EfCoreCharacterRepository.cs:101–125`):
   ```csharp
   var cloned = new CharacterItem(
       ...
       item.IsBroken,
       item.EnchaseLevel);
   freshItems.Add(cloned);
   await dbContext.CharacterItems.AddAsync(cloned, cancellationToken);
   ```
   **SECOND BREAK OCCURS HERE**: `EfCoreCharacterRepository` invokes `new CharacterItem(...)`, which again drops them, saving `IsBroken = false` and `EnchaseLevel = 0` to the database.
5. **Database Schema Mapping** (`JXServerDbContext.cs:143–150`):
   ```csharp
   builder.Property(x => x.IsBroken)
       .IsRequired()
       .HasDefaultValue(false);

   builder.Property(x => x.EnchaseLevel)
       .IsRequired()
       .HasDefaultValue((byte)0);
   ```
   EF Core is already configured with dedicated database columns for `IsBroken` and `EnchaseLevel`.
6. **World Handoff Projection** (`SessionLeaseAppService.cs:322–323`):
   ```csharp
   i.IsBroken,
   i.EnchaseLevel
   ```
   `SessionLeaseAppService.MapToDto` already maps `i.IsBroken` and `i.EnchaseLevel` to `CharacterItemDto`.

**Crucial Finding**: Every single link in the entire chain (ZoneEngine -> DTO -> Application -> Repository -> EF Core -> Handoff -> ZoneEngine) was already implemented to route `IsBroken` and `EnchaseLevel`. The **sole breaking point** in the entire software architecture is lines 66–76 of `CharacterItem.cs`.

### Observation 1.4: Codebase-Wide Constructor & Property Audit
To ensure no other domain entities or slot models have similar constructor parameter omissions, an AST-based parser was executed across all classes in `src/JXServer.Domain/` and `src/JXServer.ZoneEngine/`.
Results:
- **`src/JXServer.Domain/Accounts/Account.cs`**:
  Constructor parameters (`id`, `userName`, `passwordOrMd5Hex`, `secondaryPasswordOrMd5Hex`, `status`, `remainingTimeSeconds`, `createdAtUtc`) are all assigned 1:1. `LastLoginAtUtc` is nullable and intentionally updated at login via `AdvanceLeaseEpoch`. **Clean**.
- **`src/JXServer.Domain/Characters/Character.cs`**:
  Constructor parameters (23 parameters including `starterSkills`, `starterItems`, `starterTaskValues`, `repositoryPasswordHash`) are all assigned 1:1 or delegated to dedicated domain methods (`ReplaceTaskValues`, `SetRepositoryPasswordHash`). `UpdateWorldSnapshot` also handles all snapshot fields. **Clean**.
- **`src/JXServer.Domain/SessionLeases/SessionLease.cs`**:
  Constructor parameters (`accountId`, `userName`, `leaseEpoch`, `nowUtc`, `bishopConnectionId`) are all assigned 1:1. Handoff properties (`ActiveCharacterId`, `HandoffTicketGuid`, etc.) are intentionally null until ticket issuance. **Clean**.
- **Value Objects**:
  - `CharacterSkillValue.cs`: Positional record class `(ushort SkillId, byte BaseLevel, byte BonusLevel, ushort ExpPercent)`. Automatically mapped. **Clean**.
  - `ItemMagicAttributeValue.cs`: Positional record class `(int SlotIndex, short AttributeId, int Value0, int Value1, int Value2, byte EnchaseLevel)`. Automatically mapped. **Clean**.
- **Slot Models in `src/JXServer.ZoneEngine/`**:
  - `ActiveMissileSlot.cs`: Pooled slot initialized with `slotIndex`, mutable simulation properties updated per frame. **Clean**.
  - `GroundObjectSlot.cs`: 14 constructor parameters assigned 1:1 to properties. **Clean**.
  - `SpatialRegionSlot.cs`: `RegionX` and `RegionY` assigned 1:1; collections initialized inline. **Clean**.
  - `PlayerFeatureSlotKey.cs`: `Name` assigned; `Index` allocated via monotonic allocator. **Clean**.

**Audit Verdict**: `CharacterItem.cs` is the **only entity** in the entire codebase with omitted constructor parameter assignments.

---

## 2. Logic Chain

1. **Root Cause**:
   In `CharacterItem.cs`, `isBroken` and `enchaseLevel` parameters were added to the constructor signature and validated (`if (enchaseLevel > ItemConsts.MaxEnchaseLevel)`), but the assignment statements `IsBroken = isBroken;` and `EnchaseLevel = enchaseLevel;` were accidentally omitted before closing the constructor block.
2. **Failure Mechanism**:
   - Any broken item (`IsBroken = true`) or enchased item (`EnchaseLevel = 1..10`) entering `CharacterAppService.ApplyWorldSnapshot` or `EfCoreCharacterRepository.UpdateAsync` undergoes re-instantiation via `new CharacterItem(...)`.
   - The unassigned properties fall back to default values (`false` and `0`).
   - The repository replaces database rows with these reset values, permanently wiping the player's item state on every world snapshot or character save.
3. **Escaped Detection**:
   - Across `d:\Projects\jx-server\tests\`, there were **0 assertions** checking `CharacterItem.IsBroken` or `CharacterItem.EnchaseLevel`.
   - Existing integration and unit tests created test items using default parameter values (`isBroken = false, enchaseLevel = 0`), which coincidentally matched the unassigned default values.
   - Consequently, all 105 tests passed despite core persistence being fatally compromised.
4. **Remediation Feasibility**:
   - Because `PlayerItemSlot`, `CharacterItemDto`, `CharacterDto`, `MapZoneWorker`, and `JXServerDbContext` are already completely wired to support `IsBroken` and `EnchaseLevel`, fixing `CharacterItem.cs` constructor immediately resolves the defect across the entire system.
   - Zero database migration or contract modification is needed.
   - Dedicated unit, boundary, snapshot, and integration tests must be added to permanently fence this against regressions.

---

## 3. Caveats

- **Client Display Protocol**: In the legacy JX1 client wire protocol, client-side item rendering is driven by packet `0x5F` (`PlayerItemSyncSnapshot`), where durability and affix socket levels are packed into fixed-size structures. The wire protocol is decoupled from server-side domain entity construction.
- **Read-Only Investigation**: In compliance with the Explorer role and system instructions, no source code or test code has been altered during this investigation.
- **Enchase Level Hard Cap**: `ItemConsts.MaxEnchaseLevel` is constant `10`. Any test or application code attempting to pass `enchaseLevel > 10` will and should throw `ArgumentOutOfRangeException`.

---

## 4. Conclusion & Remediation Blueprint

### Conclusion
The forensic audit failure is completely diagnosed. The failure is strictly localized to lines 66–76 of `src/JXServer.Domain/Characters/CharacterItem.cs`. All upstream systems (`PlayerItemSlot`, `PlayerInventoryGrid`, `MapZoneWorker`) and downstream systems (`CharacterAppService`, `EfCoreCharacterRepository`, `JXServerDbContext`, `SessionLeaseAppService`) are already fully compatible and awaiting the 2-line domain constructor fix.

---

### Remediation Blueprint for Implementer Worker

#### Step 1: Code Fix in `src/JXServer.Domain/Characters/CharacterItem.cs`
In `src/JXServer.Domain/Characters/CharacterItem.cs`, lines 72–76:
```csharp
<<<< BEFORE
        if (magicAttributes is not null)
        {
            _magicAttributes.AddRange(magicAttributes);
        }
    }
==== AFTER
        if (magicAttributes is not null)
        {
            _magicAttributes.AddRange(magicAttributes);
        }

        IsBroken = isBroken;
        EnchaseLevel = enchaseLevel;
    }
>>>>
```

#### Step 2: Add Dedicated Unit & Roundtrip Test Suite
Create a new test file:  
`d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\SimulationHarnesses\CharacterItemPersistenceAndSlotTests.cs`  
Containing the following comprehensive test cases:

```csharp
using JXServer.Contracts.Characters;
using JXServer.Domain.Characters;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.Items;
using Xunit;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

public sealed class CharacterItemPersistenceAndSlotTests
{
    [Fact]
    public void CharacterItem_Constructor_ShouldAssignIsBrokenAndEnchaseLevel()
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
            randomSeed: 12345u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 0,
            currentDurability: 50,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.Equip,
            gridX: 0,
            gridY: 0,
            isBroken: true,
            enchaseLevel: 7);

        Assert.True(item.IsBroken);
        Assert.Equal(7, item.EnchaseLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void CharacterItem_EnchaseLevel_ValidRange_ShouldSucceed(byte level)
    {
        var item = new CharacterItem(
            id: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Wood,
            quality: ItemQualityKind.Normal,
            randomSeed: 1u,
            generatorVersion: 1,
            luck: 0,
            currentDurability: 10,
            maxDurability: 10,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            enchaseLevel: level);

        Assert.Equal(level, item.EnchaseLevel);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(255)]
    public void CharacterItem_EnchaseLevel_ExceedingMax_ShouldThrow(byte invalidLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterItem(
            id: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Water,
            quality: ItemQualityKind.Normal,
            randomSeed: 1u,
            generatorVersion: 1,
            luck: 0,
            currentDurability: 10,
            maxDurability: 10,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0,
            enchaseLevel: invalidLevel));
    }

    [Fact]
    public void CharacterItem_Mutators_SetBrokenAndSetEnchaseLevel_ShouldUpdateProperties()
    {
        var item = new CharacterItem(
            id: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 1,
            series: ElementSeries.Fire,
            quality: ItemQualityKind.Normal,
            randomSeed: 1u,
            generatorVersion: 1,
            luck: 0,
            currentDurability: 10,
            maxDurability: 10,
            stackCount: 1,
            containerPlace: ItemContainerPlace.EquipRoom,
            gridX: 0,
            gridY: 0);

        Assert.False(item.IsBroken);
        Assert.Equal(0, item.EnchaseLevel);

        item.SetBroken(true);
        Assert.True(item.IsBroken);

        item.SetEnchaseLevel(9);
        Assert.Equal(9, item.EnchaseLevel);

        Assert.Throws<ArgumentOutOfRangeException>(() => item.SetEnchaseLevel(15));
    }

    [Fact]
    public void PlayerItemSlot_ToCharacterItemDto_AndBack_PreservesIsBrokenAndEnchaseLevel()
    {
        var template = new ItemTemplate(
            Id: 100,
            Code: "test-sword",
            Name: "Kiếm Thí Nghiệm",
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: 0,
            ParticularType: 1,
            Level: 1,
            ElementSeries: ElementSeries.Metal,
            GridWidthCells: 1,
            GridHeightCells: 2,
            ObjDataId: 1,
            AppearanceResId: 1,
            PriceGold: 100,
            MaxStackCount: 1,
            BaseAttributes: [],
            Requirements: []);

        var slot = new PlayerItemSlot(
            runtimeItemId: 501,
            domainItemId: Guid.NewGuid(),
            template: template,
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
        Assert.Equal(8, slot.EnchaseLevel);

        CharacterItemDto dto = slot.ToCharacterItemDto();
        Assert.True(dto.IsBroken);
        Assert.Equal(8, dto.EnchaseLevel);

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
        Assert.Equal(8, domainItem.EnchaseLevel);
    }
}
```

#### Step 3: Add Database Roundtrip Integration Test
In `tests/JXServer.Integration.Tests/InventoryAndLootIntegrationTests.cs` (or a dedicated test class), add an integration test asserting that `CharacterItem.IsBroken` and `CharacterItem.EnchaseLevel` survive roundtripping through `EfCoreCharacterRepository.InsertAsync`, `EfCoreCharacterRepository.UpdateAsync`, and `EfCoreCharacterRepository.FindAsync`:
```csharp
    [Fact]
    public async Task CharacterItem_IsBrokenAndEnchaseLevel_PersistAndRehydrateThroughDatabase()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid charId = Guid.NewGuid();

        var brokenEnchasedItem = new CharacterItem(
            id: Guid.NewGuid(),
            characterId: charId,
            genre: ItemGenreKind.Equipment,
            detailType: 0,
            particularType: 1,
            level: 5,
            series: ElementSeries.Metal,
            quality: ItemQualityKind.Normal,
            randomSeed: 54321u,
            generatorVersion: ItemConsts.GeneratorVersion,
            luck: 10,
            currentDurability: 0,
            maxDurability: 100,
            stackCount: 1,
            containerPlace: ItemContainerPlace.Equip,
            gridX: 0,
            gridY: 0,
            isBroken: true,
            enchaseLevel: 8);

        var character = new Character(
            id: charId,
            accountId: accountId,
            slotIndex: 0,
            roleName: "Kiếm Ma",
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
            maxStamina: 300,
            starterItems: [brokenEnchasedItem]);

        await charRepo.InsertAsync(character);

        Character? retrieved = await charRepo.FindAsync(charId);
        Assert.NotNull(retrieved);
        CharacterItem savedItem = Assert.Single(retrieved!.Items);
        Assert.True(savedItem.IsBroken, "IsBroken must persist as true in database");
        Assert.Equal(8, savedItem.EnchaseLevel);

        // Update item to repaired and enchase level 10
        var updatedItem = new CharacterItem(
            savedItem.Id,
            charId,
            savedItem.Genre,
            savedItem.DetailType,
            savedItem.ParticularType,
            savedItem.Level,
            savedItem.Series,
            savedItem.Quality,
            savedItem.RandomSeed,
            savedItem.GeneratorVersion,
            savedItem.Luck,
            100,
            100,
            1,
            savedItem.ContainerPlace,
            savedItem.GridX,
            savedItem.GridY,
            savedItem.BindState,
            savedItem.ExpireTimeUnixSeconds,
            savedItem.CreatorName,
            null,
            isBroken: false,
            enchaseLevel: 10);

        character.UpdateWorldSnapshot(
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
            [updatedItem]);

        await charRepo.UpdateAsync(character);

        Character? reloaded = await charRepo.FindAsync(charId);
        Assert.NotNull(reloaded);
        CharacterItem reloadedItem = Assert.Single(reloaded!.Items);
        Assert.False(reloadedItem.IsBroken, "IsBroken must update to false");
        Assert.Equal(10, reloadedItem.EnchaseLevel);
    }
```

---

## 5. Verification Method

### 5.1 Pre-Fix Reproduction Command
Run the following PowerShell script to confirm the current failure:
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

if (`$item.IsBroken -ne `$true -or `$item.EnchaseLevel -ne 7) {
    Write-Host 'INTEGRITY VIOLATION CONFIRMED: Constructor dropped parameters.' -ForegroundColor Red
} else {
    Write-Host 'INTEGRITY VERIFIED: Constructor correctly assigned parameters.' -ForegroundColor Green
}
"@
```

### 5.2 Post-Fix Invalidation & Verification Conditions
After the implementer applies the blueprint:
1. Running the script in 5.1 **MUST** output:
   `INTEGRITY VERIFIED: Constructor correctly assigned parameters.`
2. Running the solution build command **MUST** succeed with 0 warnings:
   `dotnet build d:\Projects\jx-server\JXServer.slnx`
3. Running the test command **MUST** pass 100% of tests (including the new persistence and adversarial tests):
   `dotnet test d:\Projects\jx-server\JXServer.slnx --no-build`
