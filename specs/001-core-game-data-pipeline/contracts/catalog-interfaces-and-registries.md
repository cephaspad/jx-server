# Contract 02: Immutable Runtime Catalog Interfaces & Registry Contracts

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Standard**: [docs/data-architecture-standard.md](../../../docs/data-architecture-standard.md)  

---

## 1. Immutable Runtime Catalog Interfaces (`JXServer.GameAssets`)

The simulation engine and zone workers depend strictly on read-only catalog interfaces:

### 1.1 `IMapCatalog`
```csharp
public interface IMapCatalog
{
    bool TryGetMap(ushort mapId, out MapDescriptor? map);
    bool TryGetRegionObstacles(ushort mapId, int regionX, int regionY, out RegionObstacleGrid? grid);
    bool TryGetPortals(ushort mapId, out IReadOnlyList<PortalTrapBindingTemplate>? portals);
    bool IsWalkable(ushort mapId, int cellX, int cellY);
}
```

### 1.2 `INpcTemplateCatalog`
```csharp
public interface INpcTemplateCatalog
{
    bool TryGetTemplate(int templateId, out NpcTemplate? template);
    bool TryGetTemplateByCode(string code, out NpcTemplate? template);
    IReadOnlyCollection<NpcTemplate> GetAllTemplates();
}
```

### 1.3 `IPlayerTemplateCatalog`
```csharp
public interface IPlayerTemplateCatalog
{
    bool TryGetBaseTemplate(ElementSeries series, CharacterGender gender, out CharacterBaseTemplate? template);
    bool TryGetStarterVillage(ushort nativePlaceId, out StarterVillageTemplate? village);
    bool IsProhibitedRoleName(string roleName);
}
```

### 1.4 `ISkillTemplateCatalog` & `IMissileTemplateCatalog`
```csharp
public interface ISkillTemplateCatalog
{
    bool TryGetSkill(ushort skillId, out SkillTemplate? skill);
    bool TryGetSkillByCode(string code, out SkillTemplate? skill);
}

public interface IMissileTemplateCatalog
{
    bool TryGetMissile(ushort missileId, out MissileTemplate? missile);
    bool TryGetMissileByCode(string code, out MissileTemplate? missile);
}
```

### 1.5 `IItemTemplateCatalog`
```csharp
public interface IItemTemplateCatalog
{
    bool TryGetItemByRowId(int quality, int rowId, out ItemTemplate? item);
    bool TryGetItemByCompositeKey(int quality, int genre, int detailType, int particularType, int level, out ItemTemplate? item);
    bool TryGetItemByCode(string code, out ItemTemplate? item);
    bool TryGetMagicAffix(int rowId, out MagicAffixTemplate? affix);
    bool TryGetGroundObject(ushort dataId, out GroundObjectTemplate? groundObj);
}
```

### 1.6 `ILootTableCatalog` & `IShopCatalog`
```csharp
public interface ILootTableCatalog
{
    bool TryGetLootTable(string code, out LootTableTemplate? lootTable);
}

public interface IShopCatalog
{
    bool TryGetShop(int shopId, out ShopTemplate? shop);
    bool TryGetShopByCode(string code, out ShopTemplate? shop);
}
```

---

## 2. In-Memory Registry Implementation Guarantees

1. **`FrozenDictionary` Storage**: All underlying dictionaries in catalog implementations use `System.Collections.Frozen.FrozenDictionary<TKey, TValue>` created during startup, ensuring thread-safe, lock-free, zero-allocation reads.
2. **Dual-Key Lookups**: Entities with both integer IDs and semantic slugs support $O(1)$ lookups across both keys without memory duplication.
3. **Zone Access Consolidation (`ZoneAssetServices`)**: Zone workers receive a single unified `ZoneAssetServices` container injected at construction time, avoiding multiple parameter passes across the 18-Hz simulation loop.
