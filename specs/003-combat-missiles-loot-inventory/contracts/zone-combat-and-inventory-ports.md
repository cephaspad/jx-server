# Internal Architecture Contract: `JXData Standard v1` Catalogs, Combat/Inventory Zone Ports & Persistence

**Feature Branch**: `003-combat-missiles-loot-inventory`  
**Date**: 2026-11-09  

---

## 1. `JXServer.GameAssets` Immutable Catalog Ports (`JXData Standard v1`)

```csharp
namespace JXServer.GameAssets.Skills;

public interface ISkillTemplateCatalog
{
    bool TryGetSkill(int skillId, out SkillTemplate? skill);
    bool TryGetSkillByCode(string code, out SkillTemplate? skill);
    int GetBasicWeaponSkillId(int weaponDetailOrParticular);
    IReadOnlyCollection<SkillTemplate> GetAllSkills();
}

public interface IMissileTemplateCatalog
{
    bool TryGetMissile(int missileId, out MissileTemplate? missile);
    bool TryGetMissileByCode(string code, out MissileTemplate? missile);
}
```

```csharp
namespace JXServer.GameAssets.Items;

public interface IItemTemplateCatalog
{
    bool TryGetItemByCode(string code, out ItemTemplate? item);
    bool TryGetItem(ItemGenreKind genre, int detailType, int particularType, byte level, out ItemTemplate? item);
    bool TryGetMagicAffix(int affixId, out MagicAffixTemplate? affix);
    IReadOnlyList<MagicAffixTemplate> GetCandidateMagicAffixes(
        bool isSuffix,
        int equipDetailType,
        int weaponParticularType,
        ElementSeries series,
        int maxLevel);
    bool TryGetGroundObject(int objDataId, out GroundObjectTemplate? objTemplate);
}
```

```csharp
namespace JXServer.GameAssets.LootTables;

public interface ILootTableCatalog
{
    bool TryGetLootTable(string code, out LootTableTemplate? lootTable);
}
```

---

## 2. `JXServer.ZoneEngine` Outbound Sink Extensions (`IPlayerOutboundSink`)

`IPlayerOutboundSink` in `JXServer.ZoneEngine/PlayerSessions/IPlayerOutboundSink.cs` is extended with zero-allocation methods for combat, progression, ground objects, and inventory synchronization:

```csharp
// Combat, Missiles, Death & Revival
void SendSkillCastBroadcast(uint casterNpcId, uint skillId, uint skillLevel, int targetMpsX, int targetMpsYOrNpcId, bool isDirectCast = false);
void SendHurtBroadcast(uint targetNpcId, int mpsX, int mpsY, int hurtRecoverFrames);
void SendNpcDeathBroadcast(uint deadNpcId);
void SendNpcReviveBroadcast(uint revivedNpcId, byte reviveType);

// Progression, Attributes & Skill Points
void SendPlayerExpSync(int currentExp);
void SendPlayerLevelUpSync(int unallocatedAttributePoints, int unallocatedSkillPoints, int newLevel, int maxExpForNextLevel);
void SendPlayerAttributeSync(BaseAttributeKind attribute, int basePoint, int curPoint, int leavePoint);
void SendPlayerSkillLevelSync(int skillId, int skillLevel, int leavePoint, short skillExpPercent = 0);

// Ground Objects & Inventory
void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot);
void SendGroundObjectRemove(int objId, byte reason);
void SendItemSync(in PlayerItemSyncSnapshot snapshot);
void SendItemRemove(uint runtimeItemId);
void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0);
void SendPlayerMoveItemConfirm(byte downPos, byte downX, byte downY, byte upPos, byte upX, byte upY);
void SendItemAutoMoveUnlock();
```

---

## 3. Control-Plane Persistence Port (`ICharacterAppService`)

`ICharacterAppService` in `JXServer.Contracts/Characters/ICharacterAppService.cs` persists character progression, learned skills, gold balances, and `CharacterItem` rows atomically under lease-epoch fencing:

```csharp
Task<bool> UpdateCharacterWorldSnapshotAsync(
    UpdateCharacterWorldSnapshotDto input,
    CancellationToken cancellationToken = default);
```
When a character logs in (`EnterWorldAsync`), `WorldEntryResultDto` supplies the character's persisted `Level`, `Experience`, `UnallocatedAttributePoints`, `UnallocatedSkillPoints`, `ActiveAuraSkillId`, `PkState`, `PkValue`, `Skills`, and `Items` so `MapZoneWorker` immediately populates `PlayerInventoryGrid`, recalculates Five-Elements equipment activations, and emits `0x5F` (`s2c_syncitem`) and `0x61` (`s2c_syncmoney`) during world entry synchronization.
