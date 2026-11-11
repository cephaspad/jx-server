# Data Model: Core Loot, Item Generation & Ground Objects

**Feature Branch**: `011-core-loot-and-item-generation`  
**Date**: 2026-11-11  

---

## 1. Primitives & Constants (`JXServer.Primitives/Items/`)

| Type Name | Type Kind | Description & Key Values |
| :--- | :--- | :--- |
| `ItemGenreKind` | `enum : byte` | `Equipment = 0`, `Medicine = 1`, `QuestKey = 2`, `TownPortal = 3`, `Material = 4`, `MagicScript = 6`, `PlatinaEquipment = 7`. |
| `ItemQualityKind` | `enum : byte` | `Normal = 0` (White/Blue), `Golden = 1`, `Purple = 2`, `Platina = 3`, `Broken = 4`. |
| `EquipmentPartKind` | `enum : byte` | `Head = 0`, `Body = 1`, `Belt = 2`, `Weapon = 3`, `Foot = 4`, `Cuff = 5`, `Amulet = 6`, `Ring1 = 7`, `Ring2 = 8`, `Pendant = 9`, `Horse = 10`. |
| `ItemContainerPlace` | `enum : byte` | `None = 0`, `Hand = 1`, `Equip = 2`, `EquipRoom = 3`, `RepositoryRoom = 4`, `Trade = 5`, `Immediacy = 7`. |
| `GroundObjectKind` | `enum : byte` | `Item = 0`, `Money = 1`, `Prop = 2`, `Box = 3`, `Switch = 4`, `Door = 5`, `TrapObj = 6`. |
| `ItemConsts` | `static class` | `GeneratorVersion = 4`, `MaxMagicAffixSlots = 6`, `DefaultGroundObjectLifeFrames = 1080`, `DefaultLootOwnerProtectionFrames = 180`, `MaxGroundPickupDistanceMps = 192`. |

---

## 2. Static Catalogs & Item Generation (`JXServer.GameAssets/`)

### 2.1 Loot Catalogs (`LootTables/`)
- **`LootTableTemplate` (`sealed record`)**:
  - `Id`, `Code`, `RandRange` (`1000000`), `MagicRate`.
  - `MoneyDropChance`, `MinMoneyGold`, `MaxMoneyGold`.
  - `MinItemLevel`, `MaxItemLevel`, `MaxDropsPerKill`, `BlueAffixLuckFactor`.
  - `WeightedPools`: List of `WeightedLootPoolEntryTemplate`.
- **`WeightedLootPoolEntryTemplate` (`sealed record`)**:
  - `Weight`, `ItemCode`, `Genre`, `Quality`, `DetailType`, `ParticularType`, `ElementSeries`, `MinLevel`, `MaxLevel`.

### 2.2 Item Catalogs & Generator (`Items/`)
- **`ItemTemplate` (`sealed record`)**:
  - `Id`, `Code`, `Name`, `Genre`, `Quality`, `DetailType`, `ParticularType`, `Level`, `ElementSeries`.
  - `GridWidthCells`, `GridHeightCells`, `ObjDataId`, `AppearanceResId`, `PriceGold`, `MaxStackCount`.
  - `BaseAttributes`: Base durability, defense, damage ranges.
  - `Requirements`: Attribute, level, gender, series limits.
- **`MagicAffixTemplate` (`sealed record`)**:
  - `Id`, `Code`, `AttributeId`, `AttributeCode`, `NameTemplate`.
  - `IsSuffix` (`false` = explicit prefix slot `0, 2, 4`; `true` = hidden suffix slot `1, 3, 5`).
  - `ElementSeries`, `Level`, `DropWeight`, `WeightsByEquipPart`, `WeightsBySeries`.
  - `Param0Min`, `Param0Max`, `Param1Min`, `Param1Max`, `Param2Min`, `Param2Max`.
- **`GroundObjectTemplate` (`sealed record`)**:
  - `Id`, `Code`, `Name`, `Kind`, `LifeFrames`, `ObstacleKind`.
- **`ItemGenerator` (`sealed class`)**:
  - Ports `KItemGenerator.cpp` version 4: generates white equipment, consumables, and 6-affix blue equipment with deterministic seed support.

---

## 3. Zone State & Commands (`JXServer.ZoneEngine/Items/`)

### 3.1 Simulation Structures
- **`GroundObjectSlot` (`sealed class`)**:
  Live in-memory ground object residing in `SpatialRegionSlot.GroundObjects`:
  - `ObjId`: Ephemeral runtime ID.
  - `ObjDataId`: Display template identifier.
  - `Kind`: `GroundObjectKind.Item` or `GroundObjectKind.Money`.
  - `DisplayNameUtf8`: Transcoded to TCVN3 for wire.
  - `MapId`, `MpsX`, `MpsY`, `RegionX`, `RegionY`, `DirIndex`.
  - `MoneyAmount`: Gold amount (if currency).
  - `ItemPayload`: Attached `PlayerItemSlot` (if item).
  - `ColorId`: Quality color code (`0` = White, `1` = Blue, `2` = Purple, `3` = Gold).
  - `OwnerCharacterId`: Killer character ID.
  - `OwnerProtectionExpiryTick`: Tick when killer protection ends.
  - `ExpireTick`: Tick when object despawns.

### 3.2 Zone Commands
- **`PickupGroundObjectZoneCommand`**:
  - `Guid CharacterId`, `long LeaseEpoch`, `int ObjId`, `ItemContainerPlace TargetPlace`, `byte TargetGridX`, `byte TargetGridY`.
- **`RequestGroundObjectSyncZoneCommand`**:
  - `Guid CharacterId`, `long LeaseEpoch`, `int ObjId`.
- **`DropInventoryItemZoneCommand`**:
  - `Guid CharacterId`, `long LeaseEpoch`, `bool ThrowAllMedicine`.

---

## 4. Wire Packets (`JXServer.Adapter.LegacyJX1/Packets/`)

| Packet Name | Opcode | Direction | Size | Layout Summary |
| :--- | :---: | :---: | :---: | :--- |
| `ObjAddS2CPacket` | `0x64` | S2C | `35 + N` | Opcode, Len-1, Dir, ObjId, DataId, MpsX, MpsY, MoneyNum, ItemId, State, ItemWidth, Genre, ItemHeight, ColorId, Flag, NameBytes. |
| `ObjRemoveS2CPacket` | `0x67` | S2C | 6B | `<BiB`: Opcode, ObjId, Reason (`0` = expired, `1` = picked up). |
| `RequestObjC2SPacket` | `0x4A` | C2S | 5B | `<Bi`: Opcode, ObjId. |
| `ObjMouseClickC2SPacket` | `0x72` | C2S | 9B | `<BII`: Opcode, RegionId, ObjId. |
| `PlayerPickupItemC2SPacket` | `0x5A` | C2S | 12B | `<BBBBiI`: Opcode, GridY, GridX, Place, ObjId, Reserved. |
| `PlayerThrowAwayItemC2SPacket` | `0x5E` | C2S | 1B | `<B`: Opcode. Discards item in `pos_hand`. |
| `ThrowAwayAllMedicineC2SPacket` | `0x83` | C2S | 4B | `<BBBB`: Opcode, Reserved. Discards all bag medicines. |
