# Forensic Audit Remediation Exploration Report

## 1. Observation

### 1.1 Constructor Parameter Omission in `CharacterItem.cs`
In `d:\Projects\jx-server\src\JXServer.Domain\Characters\CharacterItem.cs`, the parameterized constructor declares:
```csharp
Lines 40-41:
        bool isBroken = false,
        byte enchaseLevel = 0)
```
Lines 43-49 perform guard validation on `enchaseLevel`:
```csharp
        if (enchaseLevel > ItemConsts.MaxEnchaseLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enchaseLevel),
                enchaseLevel,
                $"EnchaseLevel must be between 0 and {ItemConsts.MaxEnchaseLevel}.");
        }
```
However, in lines 51-76, assignments exist for all 20 other parameters (`Id`, `CharacterId`, `Genre`, `DetailType`, `ParticularType`, `Level`, `Series`, `Quality`, `RandomSeed`, `GeneratorVersion`, `Luck`, `CurrentDurability`, `MaxDurability`, `StackCount`, `ContainerPlace`, `GridX`, `GridY`, `BindState`, `ExpireTimeUnixSeconds`, `CreatorName`), followed by `magicAttributes` collection population:
```csharp
Lines 70-76:
        CreatorName = creatorName ?? string.Empty;

        if (magicAttributes is not null)
        {
            _magicAttributes.AddRange(magicAttributes);
        }
```
`IsBroken = isBroken;` and `EnchaseLevel = enchaseLevel;` are completely absent.
Empirical verification command:
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
Write-Host `"Expected: IsBroken=True EnchaseLevel=7`"
Write-Host `"Actual:   IsBroken=`$(`$item.IsBroken) EnchaseLevel=`$(`$item.EnchaseLevel)`"
"@
```
Output:
```
Expected: IsBroken=True EnchaseLevel=7
Actual:   IsBroken=False EnchaseLevel=0
```

### 1.2 Persistence Call Site Traces
Direct examination of all instantiation and persistence paths revealed:

1. **`src/JXServer.Application/Characters/CharacterAppService.cs` (lines 251–280)**:
   In `ApplyWorldSnapshot`:
   ```csharp
   var mappedItems = snap.Items?.Select(i => new CharacterItem(
       i.Id == Guid.Empty ? Guid.NewGuid() : i.Id,
       character.Id,
       i.Genre,
       i.DetailType,
       i.ParticularType,
       i.Level,
       i.Series,
       i.Quality,
       i.RandomSeed,
       i.GeneratorVersion,
       i.Luck,
       i.CurrentDurability,
       i.MaxDurability,
       i.StackCount,
       i.ContainerPlace,
       i.GridX,
       i.GridY,
       i.BindState,
       i.ExpireTimeUnixSeconds,
       i.CreatorName,
       i.MagicAttributes.Select(m => new ItemMagicAttributeValue(
           m.SlotIndex,
           m.AttributeId,
           m.Value0,
           m.Value1,
           m.Value2,
           m.EnchaseLevel)),
       i.IsBroken,
       i.EnchaseLevel)).ToList();
   ```
   Positional arguments 22 and 23 (`i.IsBroken` and `i.EnchaseLevel`) are passed correctly into the constructor. Because the constructor drops them, `mappedItems` always resets `IsBroken` to `false` and `EnchaseLevel` to `0`.

2. **`src/JXServer.Infrastructure/Characters/EfCoreCharacterRepository.cs` (lines 101–125)**:
   In `UpdateAsync`:
   ```csharp
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
   ```
   `cloned` is instantiated via the broken constructor. Even if domain mutation methods `SetBroken(true)` and `SetEnchaseLevel(7)` had been invoked on the input aggregate, `cloned` drops them to `false` and `0` prior to EF Core `AddAsync`.

3. **`src/JXServer.Infrastructure/EntityFrameworkCore/JXServerDbContext.cs` (lines 143–150)**:
   ```csharp
   builder.Property(x => x.IsBroken)
       .IsRequired()
       .HasDefaultValue(false);

   builder.Property(x => x.EnchaseLevel)
       .IsRequired()
       .HasDefaultValue((byte)0);
   ```
   The EF Core model configuration is already complete, with explicit schema properties, non-null requirements, and defaults. The private parameterless constructor `private CharacterItem() { CreatorName = string.Empty; }` is used by EF Core for entity materialization during queries (`FindAsync`), setting properties through their private setters.

4. **`src/JXServer.Application/SessionLeases/SessionLeaseAppService.cs` (lines 322–323)**:
   `MapToDto` reads `i.IsBroken` and `i.EnchaseLevel` from `CharacterItem` to build `CharacterItemDto`. It functions correctly when the underlying entity holds genuine values.

5. **`src/JXServer.Domain/Characters/CharacterManager.cs` (lines 125–142)**:
   Creates starter items with named parameters without `isBroken` or `enchaseLevel`, defaulting to `false` and `0`. This is expected for new characters.

### 1.3 Complete Test Gap Analysis
A recursive search across `d:\Projects\jx-server\tests\` for `IsBroken` or `EnchaseLevel`:
- Pattern `IsBroken|EnchaseLevel` matches **0** occurrences across all test assemblies (`JXServer.Protocol.Tests`, `JXServer.ZoneEngine.Tests`, `JXServer.Integration.Tests`).
- Neither domain unit tests nor EF Core persistence integration tests asserted or exercised item brokenness or enchase levels.

---

## 2. Logic Chain

1. **Defect Origin (Obs 1.1)**:
   `CharacterItem.cs` declared `isBroken` and `enchaseLevel` in its constructor signature and added `ItemConsts.MaxEnchaseLevel` validation, creating the appearance of full implementation. However, without assignment to `IsBroken` and `EnchaseLevel` properties, all constructed instances default to `IsBroken = false` and `EnchaseLevel = 0`.
2. **Propagation to Persistence (Obs 1.2)**:
   When `CharacterAppService.ApplyWorldSnapshot` processes a client detachment snapshot from `MapZoneWorker`, incoming `CharacterItemDto` instances with `IsBroken = true` and `EnchaseLevel > 0` are converted into `CharacterItem` via the broken constructor. They immediately lose both attributes.
   Subsequently, `EfCoreCharacterRepository.UpdateAsync` clones the items via the broken constructor before writing to `JXServerDbContext.CharacterItems`. The database row is committed with `IsBroken = 0` and `EnchaseLevel = 0`.
3. **EF Core Configuration Status (Obs 1.2.3)**:
   Because `JXServerDbContext` already configures `builder.Property(x => x.IsBroken)` and `builder.Property(x => x.EnchaseLevel)`, no database schema migration or EF Core configuration changes are required. The persistence layer already knows how to store and load these columns.
4. **Cause of Undetected Regression (Obs 1.3)**:
   The existing test suite of 105 tests never asserted on `CharacterItem.IsBroken` or `CharacterItem.EnchaseLevel`. Consequently, the facade implementation passed full test execution with 0 failures.
5. **Remediation Requirement**:
   To eliminate the defect and prevent circumvention:
   - Add explicit property assignments `IsBroken = isBroken;` and `EnchaseLevel = enchaseLevel;` in `CharacterItem.cs`.
   - Implement rigorous unit and integration persistence tests that assert roundtrip fidelity across DTO mapping, repository cloning, and EF Core SQLite database storage.

---

## 3. Caveats

- **No Caveats**. The defect is 100% reproducible and isolated. All affected call sites and EF Core configurations have been inspected.

---

## 4. Conclusion & Remediation Blueprint

### 4.1 Exact Code Fix
**Target File**: `src/JXServer.Domain/Characters/CharacterItem.cs`  
**Target Location**: Lines 70–71 in `CharacterItem(Guid id, ...)` constructor  
**Proposed Change**:
```csharp
// BEFORE (lines 69-76):
        ExpireTimeUnixSeconds = expireTimeUnixSeconds;
        CreatorName = creatorName ?? string.Empty;

        if (magicAttributes is not null)
        {
            _magicAttributes.AddRange(magicAttributes);
        }

// AFTER:
        ExpireTimeUnixSeconds = expireTimeUnixSeconds;
        CreatorName = creatorName ?? string.Empty;
        IsBroken = isBroken;
        EnchaseLevel = enchaseLevel;

        if (magicAttributes is not null)
        {
            _magicAttributes.AddRange(magicAttributes);
        }
```

### 4.2 Required Test Suite Specifications

#### Part A: Domain Unit Tests (in `tests/JXServer.ZoneEngine.Tests/CharacterItemDomainTests.cs` or `ZoneSimulationHarnessTests.cs`)
1. **`CharacterItem_Constructor_ShouldAssignIsBrokenAndEnchaseLevel`**:
   - Construct with `isBroken: true, enchaseLevel: 7`.
   - Assert `item.IsBroken == true` and `item.EnchaseLevel == 7`.
2. **`CharacterItem_Constructor_DefaultValues_ShouldBeFalseAndZero`**:
   - Construct without optional `isBroken` and `enchaseLevel` arguments.
   - Assert `item.IsBroken == false` and `item.EnchaseLevel == 0`.
3. **`CharacterItem_Constructor_EnchaseLevelBoundaryValidation`**:
   - Test `enchaseLevel = 0` -> succeeds, `item.EnchaseLevel == 0`.
   - Test `enchaseLevel = 10` (`ItemConsts.MaxEnchaseLevel`) -> succeeds, `item.EnchaseLevel == 10`.
   - Test `enchaseLevel = 11` -> throws `ArgumentOutOfRangeException` with `nameof(enchaseLevel)`.
4. **`CharacterItem_StateMutation_SetBrokenAndSetEnchaseLevel`**:
   - Call `item.SetBroken(true)` -> `item.IsBroken == true`.
   - Call `item.SetEnchaseLevel(5)` -> `item.EnchaseLevel == 5`.
   - Call `item.SetEnchaseLevel(11)` -> throws `ArgumentOutOfRangeException`.
5. **`CharacterAppService_ApplyWorldSnapshot_PreservesIsBrokenAndEnchaseLevel`**:
   - Invoke `CharacterAppService.ApplyWorldSnapshot` with an `UpdateCharacterWorldSnapshotDto` having items with `IsBroken = true` and `EnchaseLevel = 8`.
   - Assert `character.Items.Single().IsBroken == true` and `character.Items.Single().EnchaseLevel == 8`.
6. **`SessionLeaseAppService_MapToDto_ProjectsIsBrokenAndEnchaseLevel`**:
   - Build `Character` containing item with `IsBroken = true, EnchaseLevel = 6`.
   - Call `SessionLeaseAppService.MapToDto(character)`.
   - Assert `dto.Items.Single().IsBroken == true` and `dto.Items.Single().EnchaseLevel == 6`.

#### Part B: EF Core Integration Persistence Tests (in `tests/JXServer.Integration.Tests/CharacterLifecycleIntegrationTests.cs` or new `CharacterItemPersistenceIntegrationTests.cs`)
1. **`CharacterItem_InsertAndReload_ThroughEfCore_PreservesIsBrokenAndEnchaseLevel`**:
   - Spin up `IntegrationTestServerHarness.StartAsync()`.
   - Insert a `Character` via `ICharacterRepository.InsertAsync(character, autoSave: true)` with 4 distinct items:
     - Normal (`IsBroken = false, EnchaseLevel = 0`)
     - Broken (`IsBroken = true, EnchaseLevel = 0`)
     - Enchased (`IsBroken = false, EnchaseLevel = 7`)
     - Broken + Max Enchased (`IsBroken = true, EnchaseLevel = 10`)
   - Re-query via `ICharacterRepository.FindAsync(character.Id)` (which uses `AsNoTracking()` against the underlying SQLite DB).
   - Assert all 4 items retain exact `IsBroken` and `EnchaseLevel` matching initial values.
2. **`CharacterItem_UpdateAndCloning_ThroughEfCoreRepository_PreservesIsBrokenAndEnchaseLevel`**:
   - Insert character with initial clean item.
   - Update character via `character.UpdateWorldSnapshot` replacing item with `IsBroken = true, EnchaseLevel = 9`.
   - Call `ICharacterRepository.UpdateAsync(character, autoSave: true)`.
   - Reload via `ICharacterRepository.FindAsync(character.Id)`.
   - Assert rehydrated item has `IsBroken == true` and `EnchaseLevel == 9` (validating `EfCoreCharacterRepository.UpdateAsync` cloning path).
3. **`CharacterItem_SessionLeaseSnapshotEviction_PersistsToDatabase`**:
   - Create active session lease and character in harness.
   - Call `ISessionLeaseAppService.CompleteLogoutOrEvictAsync` with a `ReleaseSessionLeaseInput` containing `UpdateCharacterWorldSnapshotDto` with item `IsBroken = true, EnchaseLevel = 5`.
   - Query `ICharacterRepository.FindAsync(character.Id)`.
   - Assert persisted item has `IsBroken == true` and `EnchaseLevel == 5`.

---

## 5. Verification Method

### 5.1 Verification Commands
1. **Direct Domain Verification Script**:
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
   Write-Host `"Actual: IsBroken=`$(`$item.IsBroken) EnchaseLevel=`$(`$item.EnchaseLevel)`"
   if (`$item.IsBroken -eq `$true -and `$item.EnchaseLevel -eq 7) {
       Write-Host 'FIX VERIFIED: CharacterItem constructor properly assigned properties.' -ForegroundColor Green
   } else {
       Write-Host 'VERIFICATION FAILED: Parameters still omitted.' -ForegroundColor Red
   }
   "@
   ```

2. **Full Solution Build & Test Suite Execution**:
   ```powershell
   dotnet build d:\Projects\jx-server\JXServer.slnx
   dotnet test d:\Projects\jx-server\JXServer.slnx --no-build
   ```

### 5.2 Invalidation Conditions
- Any constructor invocation `new CharacterItem(..., isBroken: true, enchaseLevel: 7)` returning `IsBroken == false` or `EnchaseLevel != 7`.
- Any `UpdateAsync` or `ApplyWorldSnapshot` call resetting broken state or enchase level of character items.
- Any test suite run in which item persistence tests are omitted or hardcoded.
