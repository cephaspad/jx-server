using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Shops;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;
using MemoryPack;

namespace JXServer.GameAssets.Snapshots;

/// <summary>
/// Loads or compiles a <see cref="GameAssetsSnapshotBundle"/> from <c>artifacts/jxdata.pack</c> (Tier 2)
/// or <c>data/**/*.json</c> (Tier 1) per <c>JXData Standard v1</c> (`FR-001`).
/// </summary>
public static class GameAssetsSnapshotLoader
{
    public const string DefaultSnapshotFileName = "jxdata.pack";

    private static readonly ConcurrentDictionary<(string DataDir, string PackPath, bool PreferBinary, long PackWriteTicks, long DataStamp), (GameAssetsSnapshotBundle Bundle, string SourceTier)> BundleCache = new();

    public static string ComputeDataDirectorySha256Hex(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
        {
            return string.Empty;
        }

        string[] jsonFiles = Directory.GetFiles(dataDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(p => Path.GetRelativePath(dataDirectory, p).Replace('\\', '/'), StringComparer.Ordinal)
            .ToArray();

        if (jsonFiles.Length == 0)
        {
            return string.Empty;
        }

        using var sha = SHA256.Create();
        foreach (string file in jsonFiles)
        {
            string relPath = Path.GetRelativePath(dataDirectory, file).Replace('\\', '/');
            byte[] pathBytes = Encoding.UTF8.GetBytes(relPath);
            sha.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);

            byte[] contentBytes = File.ReadAllBytes(file);
            sha.TransformBlock(contentBytes, 0, contentBytes.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash ?? []);
    }

    public static bool TryLoadBundle(
        GameAssetsOptions options,
        out GameAssetsSnapshotBundle? bundle,
        out string sourceTier)
    {
        ArgumentNullException.ThrowIfNull(options);
        bundle = null;
        sourceTier = "LegacyFallback";

        string dataDir = ResolveDirectory(options.DataDirectory);
        string artifactsDir = ResolveDirectory(options.ArtifactsDirectory);
        string packPath = Path.Combine(artifactsDir, DefaultSnapshotFileName);

        long packWriteTicks = File.Exists(packPath) ? File.GetLastWriteTimeUtc(packPath).Ticks : -1L;
        long dataStamp = ComputeDataDirectoryFastStamp(dataDir);
        var cacheKey = (dataDir, packPath, options.PreferBinarySnapshots, packWriteTicks, dataStamp);
        if (BundleCache.TryGetValue(cacheKey, out (GameAssetsSnapshotBundle Bundle, string SourceTier) cached))
        {
            bundle = cached.Bundle;
            sourceTier = cached.SourceTier;
            return true;
        }

        string currentSha = ComputeDataDirectorySha256Hex(dataDir);

        if (options.PreferBinarySnapshots && packWriteTicks >= 0)
        {
            try
            {
                byte[] packBytes = File.ReadAllBytes(packPath);
                GameAssetsSnapshotBundle? deserialized = MemoryPackSerializer.Deserialize<GameAssetsSnapshotBundle>(packBytes);
                if (deserialized is not null &&
                    (string.IsNullOrEmpty(currentSha) ||
                     string.Equals(deserialized.SourceJsonSha256Hex, currentSha, StringComparison.OrdinalIgnoreCase)))
                {
                    bundle = deserialized;
                    sourceTier = $"Tier2:MemoryPack({packPath})";
                    BundleCache.TryAdd(cacheKey, (bundle, sourceTier));
                    return true;
                }
            }
            catch
            {
                // Fall back to Tier 1 JSON if binary snapshot is corrupt or outdated
            }
        }

        if (!string.IsNullOrEmpty(currentSha))
        {
            bundle = LoadFromCanonicalJsonDirectory(dataDir, currentSha);
            sourceTier = $"Tier1:CanonicalJson({dataDir})";
            BundleCache.TryAdd(cacheKey, (bundle, sourceTier));
            return true;
        }

        return false;
    }

    private static long ComputeDataDirectoryFastStamp(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
        {
            return -1L;
        }

        long hash = 17L;
        foreach (string file in Directory.EnumerateFiles(dataDirectory, "*.json", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            hash = unchecked((hash * 31L) ^ info.Length ^ info.LastWriteTimeUtc.Ticks);
        }

        return hash;
    }

    public static GameAssetsSnapshotBundle LoadFromCanonicalJsonDirectory(string dataDirectory, string? precomputedSha256 = null)
    {
        string sha256 = precomputedSha256 ?? ComputeDataDirectorySha256Hex(dataDirectory);

        var skills = new List<SkillTemplate>();
        var missiles = new List<MissileTemplate>();
        string skillsDir = Path.Combine(dataDirectory, "skills");
        if (Directory.Exists(skillsDir))
        {
            foreach (string file in Directory.GetFiles(skillsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
                ParseSkillCatalogJson(doc.RootElement, skills, missiles);
            }
        }

        var items = new List<ItemTemplate>();
        var magicAffixes = new List<MagicAffixTemplate>();
        var groundObjects = new List<GroundObjectTemplate>();
        string itemsDir = Path.Combine(dataDirectory, "items");
        if (Directory.Exists(itemsDir))
        {
            foreach (string file in Directory.GetFiles(itemsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
                ParseItemCatalogJson(doc.RootElement, items, magicAffixes, groundObjects);
            }
        }

        var lootTables = new List<LootTableTemplate>();
        string lootDir = Path.Combine(dataDirectory, "loot");
        if (Directory.Exists(lootDir))
        {
            foreach (string file in Directory.GetFiles(lootDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
                ParseLootCatalogJson(doc.RootElement, lootTables);
            }
        }

        var npcs = new List<NpcTemplate>();
        var portalTrapBindings = new List<PortalTrapBindingTemplate>();
        var autoExecSpawns = new List<StaticNpcSpawnTemplate>();
        bool hasWorldSpawnGroups = false;
        string npcsDir = Path.Combine(dataDirectory, "npcs");
        if (Directory.Exists(npcsDir))
        {
            foreach (string file in Directory.GetFiles(npcsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
                if (ParseNpcCatalogJson(doc.RootElement, npcs, lootTables, autoExecSpawns, portalTrapBindings))
                {
                    hasWorldSpawnGroups = true;
                }
            }
        }

        var charTemplates = new List<CharacterBaseTemplate>();
        var starterVillages = new List<StarterVillageTemplate>();
        var growthRules = new List<PlayerSeriesGrowthRuleTemplate>();
        var levelExpTable = new List<long>();
        var defaultStarterItems = new List<StarterItemTemplate>();
        string playersDir = Path.Combine(dataDirectory, "players");
        if (Directory.Exists(playersDir))
        {
            foreach (string file in Directory.GetFiles(playersDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
                ParsePlayerCatalogJson(
                    doc.RootElement,
                    items,
                    charTemplates,
                    starterVillages,
                    growthRules,
                    levelExpTable,
                    defaultStarterItems);
            }
        }

        var mapDescriptors = new List<MapDescriptor>();
        string mapsDir = Path.Combine(dataDirectory, "maps");
        if (Directory.Exists(mapsDir))
        {
            Dictionary<int, string> npcNamesById = npcs
                .GroupBy(n => n.TemplateId)
                .ToDictionary(g => g.Key, g => g.First().Name);

            foreach (string file in Directory.GetFiles(mapsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
                ParseMapJson(doc.RootElement, npcNamesById, mapDescriptors, portalTrapBindings, autoExecSpawns, !hasWorldSpawnGroups);
            }
        }

        var shops = new List<ShopTemplate>();
        string shopsDir = Path.Combine(dataDirectory, "shops");
        if (Directory.Exists(shopsDir))
        {
            foreach (string file in Directory.GetFiles(shopsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
                ParseShopCatalogJson(doc.RootElement, shops);
            }
        }

        return new GameAssetsSnapshotBundle(
            "1.0.0",
            sha256,
            skills,
            missiles,
            items,
            magicAffixes,
            groundObjects,
            lootTables,
            npcs,
            autoExecSpawns,
            charTemplates,
            starterVillages,
            growthRules,
            levelExpTable,
            defaultStarterItems,
            mapDescriptors,
            portalTrapBindings,
            shops);
    }

    public static string ResolveDirectory(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(configuredPath) && Directory.Exists(configuredPath))
        {
            return configuredPath;
        }

        string current = Directory.GetCurrentDirectory();
        for (int depth = 0; depth < 6; depth++)
        {
            string candidate = Path.GetFullPath(Path.Combine(current, configuredPath));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return Path.GetFullPath(configuredPath);
    }

    private static void ParseSkillCatalogJson(
        JsonElement root,
        List<SkillTemplate> skills,
        List<MissileTemplate> missiles)
    {
        if (root.TryGetProperty("skills", out JsonElement skillsArr) && skillsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement s in skillsArr.EnumerateArray())
            {
                int id = s.GetProperty("id").GetInt32();
                string code = s.GetProperty("code").GetString() ?? $"skill-{id}";
                string name = s.GetProperty("name").GetString() ?? code;
                string styleStr = s.GetProperty("style").GetString() ?? "melee";
                SkillStyleKind style = ParseSkillStyle(styleStr);
                int seriesVal = s.GetProperty("elementSeries").GetInt32();
                ElementSeries series = (ElementSeries)Math.Clamp(seriesVal, 0, 4);

                bool isOffensive = GetBool(s, "isOffensive", true);
                bool isPhysical = GetBool(s, "isPhysical", false);
                bool isMelee = GetBool(s, "isMelee", style == SkillStyleKind.Melee);
                bool isAura = GetBool(s, "isAura", style == SkillStyleKind.Aura);
                bool isWeaponSkill = GetBool(s, "isWeaponSkill", false);
                bool isExpSkill = GetBool(s, "isExpSkill", false);
                bool useAttackRating = GetBool(s, "useAttackRating", true);
                bool doHurtAnimation = GetBool(s, "doHurtAnimation", true);
                bool peaceCanUse = GetBool(s, "peaceCanUse", !isOffensive);

                int reqLevel = GetInt(s, "reqLevel", 0);
                int maxLevel = GetInt(s, "maxLevel", 20);
                int equipLimit = GetInt(s, "equipLimit", -2);
                int horseLimit = GetInt(s, "horseLimit", 0);
                int castRangeMps = GetInt(s, "castRangeMps", isMelee ? 96 : 512);
                int waitFrames = GetInt(s, "waitFrames", 18);
                int missileId = GetInt(s, "missileId", 0);
                string missileForm = GetString(s, "missileForm", "line");

                int childSkillId = 0;
                int childSkillLevel = 0;
                int childSkillCount = 1;
                if (s.TryGetProperty("childSkill", out JsonElement childObj) && childObj.ValueKind == JsonValueKind.Object)
                {
                    childSkillId = GetInt(childObj, "skillId", 0);
                    childSkillLevel = GetInt(childObj, "skillLevel", -1);
                    childSkillCount = GetInt(childObj, "count", 1);
                }

                var costScaling = new SkillCostScalingTemplate("mana", 5, 1);
                if (s.TryGetProperty("costScaling", out JsonElement costObj) && costObj.ValueKind == JsonValueKind.Object)
                {
                    string costType = GetString(costObj, "costType", "mana");
                    int baseMana = GetInt(costObj, "baseMana", 0);
                    int manaPerLevel = GetInt(costObj, "manaPerLevel", 0);
                    int baseStamina = GetInt(costObj, "baseStamina", 0);
                    int baseLife = GetInt(costObj, "baseLife", 0);
                    IReadOnlyList<int>? values = TryReadIntList(costObj, "valuesByLevel");
                    costScaling = new SkillCostScalingTemplate(costType, baseMana, manaPerLevel, baseStamina, baseLife, values);
                }

                var effects = new List<SkillEffectScalingTemplate>();
                if (s.TryGetProperty("effectScaling", out JsonElement effArr) && effArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement eff in effArr.EnumerateArray())
                    {
                        int attrId = GetInt(eff, "attributeId", 0);
                        string attrCode = GetString(eff, "attributeCode", "physics_damage_v");
                        string slotCat = GetString(eff, "slotCategory", "damage");
                        string curveKind = GetString(eff, "curve", "linear");

                        SkillParameterCurveTemplate? p0 = ParseParameterCurve(eff, "param0")
                            ?? new SkillParameterCurveTemplate(
                                curveKind,
                                GetInt(eff, "baseMin", 10),
                                GetInt(eff, "perLevelMin", 5),
                                TryReadIntList(eff, "valuesByLevel"));

                        SkillParameterCurveTemplate? p1 = ParseParameterCurve(eff, "param1")
                            ?? new SkillParameterCurveTemplate(
                                curveKind,
                                GetInt(eff, "durationFrames", 0),
                                0);

                        SkillParameterCurveTemplate? p2 = ParseParameterCurve(eff, "param2")
                            ?? new SkillParameterCurveTemplate(
                                curveKind,
                                GetInt(eff, "baseMax", GetInt(eff, "baseMin", 15)),
                                GetInt(eff, "perLevelMax", GetInt(eff, "perLevelMin", 8)));

                        effects.Add(new SkillEffectScalingTemplate(attrId, attrCode, slotCat, p0, p1, p2));
                    }
                }

                skills.Add(new SkillTemplate(
                    id,
                    code,
                    name,
                    style,
                    series,
                    isOffensive,
                    isPhysical,
                    isMelee,
                    isAura,
                    isWeaponSkill,
                    isExpSkill,
                    useAttackRating,
                    doHurtAnimation,
                    peaceCanUse,
                    reqLevel,
                    maxLevel,
                    equipLimit,
                    horseLimit,
                    castRangeMps,
                    waitFrames,
                    missileId,
                    missileForm,
                    childSkillId,
                    childSkillLevel,
                    childSkillCount,
                    costScaling,
                    effects));
            }
        }

        if (root.TryGetProperty("missiles", out JsonElement missilesArr) && missilesArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement m in missilesArr.EnumerateArray())
            {
                int id = m.GetProperty("id").GetInt32();
                string code = m.GetProperty("code").GetString() ?? $"missile-{id}";
                string name = m.GetProperty("name").GetString() ?? code;
                MissileMoveKind moveKind = ParseMissileMoveKind(GetString(m, "moveKind", "line64"));
                string followKind = GetString(m, "followKind", "none");
                int speed = GetInt(m, "speedMpsPerFrame", 24);
                int height = GetInt(m, "heightMps", 0);
                int zSpeed = GetInt(m, "zSpeedMpsPerFrame", 0);
                int zAccel = GetInt(m, "zAccelMpsPerFrame2", 0);
                int lifeFrames = GetInt(m, "lifeFrames", 36);
                int hitRadius = GetInt(m, "hitRadiusMps", 64);
                int collideCells = GetInt(m, "collideRadiusCells", 1);
                int damageCells = GetInt(m, "damageRadiusCells", 1);
                bool isRangeDamage = GetBool(m, "isRangeDamage", false);
                int damageInterval = GetInt(m, "damageIntervalFrames", 0);
                int pierceCount = GetInt(m, "pierceCount", 1);
                bool collideVanish = GetBool(m, "collideVanish", true);
                bool canCollideFriend = GetBool(m, "canCollideFriend", false);
                bool autoExplode = GetBool(m, "autoExplode", false);
                bool mustHit = GetBool(m, "mustHit", false);
                int subMissileId = GetInt(m, "subMissileId", 0);
                int param1 = GetInt(m, "param1", 0);
                int param2 = GetInt(m, "param2", 0);
                int param3 = GetInt(m, "param3", 0);

                missiles.Add(new MissileTemplate(
                    id,
                    code,
                    name,
                    moveKind,
                    followKind,
                    speed,
                    height,
                    zSpeed,
                    zAccel,
                    lifeFrames,
                    hitRadius,
                    collideCells,
                    damageCells,
                    isRangeDamage,
                    damageInterval,
                    pierceCount,
                    collideVanish,
                    canCollideFriend,
                    autoExplode,
                    mustHit,
                    subMissileId,
                    param1,
                    param2,
                    param3));
            }
        }
    }

    private static void ParseItemCatalogJson(
        JsonElement root,
        List<ItemTemplate> items,
        List<MagicAffixTemplate> magicAffixes,
        List<GroundObjectTemplate> groundObjects)
    {
        if (root.TryGetProperty("items", out JsonElement itemsArr) && itemsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement it in itemsArr.EnumerateArray())
            {
                int id = GetInt(it, "rowId", GetInt(it, "id", items.Count + 1));
                string code = it.GetProperty("code").GetString() ?? $"item-{id}";
                string name = it.GetProperty("name").GetString() ?? code;
                int rawGenre = GetInt(it, "genre", 0);
                ItemGenreKind genre = rawGenre == 5 ? ItemGenreKind.TownPortal : (ItemGenreKind)rawGenre;
                ItemQualityKind quality = (ItemQualityKind)GetInt(it, "quality", 0);
                int detailType = GetInt(it, "detailType", 0);
                int particularType = GetInt(it, "particularType", 0);
                byte level = (byte)Math.Clamp(GetInt(it, "level", 1), 0, 30);
                int seriesVal = GetInt(it, "elementSeries", 0);
                ElementSeries series = (ElementSeries)Math.Clamp(seriesVal, 0, 4);
                byte width = (byte)Math.Clamp(GetInt(it, "gridWidthCells", 1), 1, 6);
                byte height = (byte)Math.Clamp(GetInt(it, "gridHeightCells", 1), 1, 10);
                int objDataId = GetInt(it, "objDataId", 1);
                int resId = GetInt(it, "appearanceResId", 0);
                int price = GetInt(it, "priceGold", 100);
                int maxStack = GetInt(it, "maxStackCount", 1);

                var baseAttrs = new List<ItemBaseAttributeTemplate>();
                if (it.TryGetProperty("baseAttributes", out JsonElement baArr) && baArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement ba in baArr.EnumerateArray())
                    {
                        baseAttrs.Add(new ItemBaseAttributeTemplate(
                            GetInt(ba, "attributeId", 0),
                            GetString(ba, "attributeCode", "attribute"),
                            GetInt(ba, "minValue", 0),
                            GetInt(ba, "maxValue", 0),
                            GetInt(ba, "durationFrames", 0)));
                    }
                }

                var reqs = new List<ItemRequirementTemplate>();
                if (it.TryGetProperty("requirements", out JsonElement reqArr) && reqArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement rq in reqArr.EnumerateArray())
                    {
                        reqs.Add(new ItemRequirementTemplate(
                            GetInt(rq, "requirementType", 0),
                            GetString(rq, "requirementCode", "level"),
                            GetInt(rq, "minValue", 0)));
                    }
                }

                items.Add(new ItemTemplate(
                    id,
                    code,
                    name,
                    genre,
                    quality,
                    detailType,
                    particularType,
                    level,
                    series,
                    width,
                    height,
                    objDataId,
                    resId,
                    price,
                    maxStack,
                    baseAttrs,
                    reqs));
            }
        }

        if (root.TryGetProperty("magicAffixes", out JsonElement maArr) && maArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement ma in maArr.EnumerateArray())
            {
                int id = GetInt(ma, "rowId", GetInt(ma, "id", magicAffixes.Count + 1));
                string code = ma.GetProperty("code").GetString() ?? $"affix-{id}";
                int attrId = GetInt(ma, "attributeId", id);
                string attrCode = GetString(ma, "attributeCode", code);
                string nameTpl = GetString(ma, "nameTemplate", code);
                bool isSuffix = GetBool(ma, "isSuffix", false);
                bool isGoldenOnly = GetBool(ma, "isGoldenOnly", false);
                string affixPool = GetString(ma, "affixPool", isGoldenOnly ? "goldenFixed" : "blueRandom");
                ElementSeries series = (ElementSeries)Math.Clamp(GetInt(ma, "elementSeries", 0), 0, 4);
                int level = Math.Clamp(GetInt(ma, "level", 1), 1, 30);
                int dropWeight = GetInt(ma, "dropWeight", 100);

                var equipWeights = new EquipPartWeightsTemplate(100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100);
                if (ma.TryGetProperty("weightsByEquipPart", out JsonElement eqObj) && eqObj.ValueKind == JsonValueKind.Object)
                {
                    int melee = GetInt(eqObj, "meleeWeapon", 0);
                    int range = GetInt(eqObj, "rangeWeapon", 0);
                    equipWeights = new EquipPartWeightsTemplate(
                        Sword: GetInt(eqObj, "sword", melee),
                        Blade: GetInt(eqObj, "blade", melee),
                        Club: GetInt(eqObj, "staff", melee),
                        Spear: GetInt(eqObj, "spear", melee),
                        Hammer: GetInt(eqObj, "hammer", melee),
                        DualBlade: GetInt(eqObj, "dualBlades", melee),
                        Dart: GetInt(eqObj, "darts", range),
                        Knife: GetInt(eqObj, "throwingKnife", range),
                        Crossbow: GetInt(eqObj, "crossbow", range),
                        Armor: GetInt(eqObj, "armor", 0),
                        Ring: GetInt(eqObj, "ring", 0),
                        Amulet: GetInt(eqObj, "amulet", GetInt(eqObj, "necklace", 0)),
                        Boot: GetInt(eqObj, "boot", 0),
                        Belt: GetInt(eqObj, "belt", 0),
                        Helm: GetInt(eqObj, "helm", 0),
                        Cuff: GetInt(eqObj, "cuff", 0),
                        Pendant: GetInt(eqObj, "pendant", GetInt(eqObj, "sachet", 0)),
                        Horse: GetInt(eqObj, "horse", 0));
                }

                var seriesWeights = new SeriesWeightsTemplate(100, 100, 100, 100, 100);
                if (ma.TryGetProperty("weightsBySeries", out JsonElement swObj) && swObj.ValueKind == JsonValueKind.Object)
                {
                    seriesWeights = new SeriesWeightsTemplate(
                        GetInt(swObj, "metal", 100),
                        GetInt(swObj, "wood", 100),
                        GetInt(swObj, "water", 100),
                        GetInt(swObj, "fire", 100),
                        GetInt(swObj, "earth", 100));
                }

                magicAffixes.Add(new MagicAffixTemplate(
                    id,
                    code,
                    attrId,
                    attrCode,
                    nameTpl,
                    isSuffix,
                    isGoldenOnly,
                    series,
                    level,
                    dropWeight,
                    equipWeights,
                    seriesWeights,
                    GetInt(ma, "param0Min", 1),
                    GetInt(ma, "param0Max", 10),
                    GetInt(ma, "param1Min", -1),
                    GetInt(ma, "param1Max", -1),
                    GetInt(ma, "param2Min", 0),
                    GetInt(ma, "param2Max", 0),
                    affixPool));
            }
        }

        if (root.TryGetProperty("groundObjects", out JsonElement goArr) && goArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement go in goArr.EnumerateArray())
            {
                int id = go.GetProperty("id").GetInt32();
                string code = go.GetProperty("code").GetString() ?? $"obj-{id}";
                string name = go.GetProperty("name").GetString() ?? code;
                string kindStr = GetString(go, "kind", "Item");
                GroundObjectKind kind = Enum.TryParse<GroundObjectKind>(kindStr, ignoreCase: true, out GroundObjectKind parsedKind)
                    ? parsedKind
                    : GroundObjectKind.Item;
                int lifeFrames = GetInt(go, "lifeFrames", 1080);
                int obsKind = GetInt(go, "obstacleKind", 0);
                string? scriptPath = go.TryGetProperty("scriptPath", out JsonElement sp) ? sp.GetString() : null;

                groundObjects.Add(new GroundObjectTemplate(id, code, name, kind, lifeFrames, obsKind, scriptPath));
            }
        }
    }

    private static void ParseLootCatalogJson(JsonElement root, List<LootTableTemplate> lootTables)
    {
        if (root.TryGetProperty("lootTables", out JsonElement ltArr) && ltArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement lt in ltArr.EnumerateArray())
            {
                int id = GetInt(lt, "id", lootTables.Count + 1);
                string code = lt.GetProperty("code").GetString() ?? $"loot-{id}";
                int randRange = GetInt(lt, "randRange", 1000000);
                int magicRate = GetInt(lt, "magicRate", 1);
                double moneyChance = lt.TryGetProperty("moneyDropChance", out JsonElement mc) ? mc.GetDouble() : 0.5;
                int minMoney = GetInt(lt, "minMoneyGold", 10);
                int maxMoney = GetInt(lt, "maxMoneyGold", 100);
                byte minItemLvl = (byte)Math.Clamp(GetInt(lt, "minItemLevel", 1), 1, 20);
                byte maxItemLvl = (byte)Math.Clamp(GetInt(lt, "maxItemLevel", 10), 1, 20);
                int maxDrops = GetInt(lt, "maxDropsPerKill", 2);
                double luckFactor = lt.TryGetProperty("blueAffixLuckFactor", out JsonElement lf) ? lf.GetDouble() : 1.0;

                var pools = new List<WeightedLootPoolEntryTemplate>();
                if (lt.TryGetProperty("weightedPools", out JsonElement wpArr) && wpArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement wp in wpArr.EnumerateArray())
                    {
                        int weight = GetInt(wp, "weight", 100);
                        string? itemCode = wp.TryGetProperty("itemCode", out JsonElement ic) ? ic.GetString() : null;
                        ItemGenreKind? genre = null;
                        if (wp.TryGetProperty("genre", out JsonElement g) && g.TryGetInt32(out int rawPoolGenre))
                        {
                            genre = rawPoolGenre == 5 ? ItemGenreKind.TownPortal : (ItemGenreKind)rawPoolGenre;
                        }
                        ItemQualityKind quality = (ItemQualityKind)GetInt(wp, "quality", 0);
                        int? detail = wp.TryGetProperty("detailType", out JsonElement dt) ? dt.GetInt32() : null;
                        int? particular = wp.TryGetProperty("particularType", out JsonElement pt) ? pt.GetInt32() : null;
                        ElementSeries series = (ElementSeries)Math.Clamp(GetInt(wp, "elementSeries", 0), 0, 4);
                        byte minL = (byte)Math.Clamp(GetInt(wp, "minLevel", minItemLvl), 0, 20);
                        byte maxL = (byte)Math.Clamp(GetInt(wp, "maxLevel", maxItemLvl), 0, 20);
                        int? goldId = wp.TryGetProperty("goldEquipId", out JsonElement ge) ? ge.GetInt32() : null;

                        pools.Add(new WeightedLootPoolEntryTemplate(
                            weight,
                            itemCode,
                            genre,
                            quality,
                            detail,
                            particular,
                            series,
                            minL,
                            maxL,
                            goldId));
                    }
                }

                lootTables.Add(new LootTableTemplate(
                    id,
                    code,
                    randRange,
                    magicRate,
                    moneyChance,
                    minMoney,
                    maxMoney,
                    minItemLvl,
                    maxItemLvl,
                    maxDrops,
                    luckFactor,
                    pools));
            }
        }
    }

    private static bool ParseNpcCatalogJson(
        JsonElement root,
        List<NpcTemplate> npcs,
        List<LootTableTemplate> lootTables,
        List<StaticNpcSpawnTemplate> autoExecSpawns,
        List<PortalTrapBindingTemplate> portalTrapBindings)
    {
        ParseLootCatalogJson(root, lootTables);
        bool foundWorldSpawns = false;

        if (root.TryGetProperty("npcs", out JsonElement npcArr) && npcArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement n in npcArr.EnumerateArray())
            {
                int id = n.GetProperty("id").GetInt32();
                string code = n.GetProperty("code").GetString() ?? $"npc-{id}";
                string name = n.GetProperty("name").GetString() ?? code;
                string kindStr = GetString(n, "entityKind", "Monster");
                EntityKind kind = Enum.TryParse<EntityKind>(kindStr, ignoreCase: true, out EntityKind parsedKind)
                    ? parsedKind
                    : EntityKind.Monster;
                EntityCamp camp = (EntityCamp)Math.Clamp(GetInt(n, "defaultCamp", 0), 0, 7);
                ElementSeries series = (ElementSeries)Math.Clamp(GetInt(n, "elementSeries", 0), 0, 4);
                int walkSpeed = Math.Clamp(GetInt(n, "walkSpeedMpsPerFrame", 10), 1, 64);
                int runSpeed = Math.Clamp(GetInt(n, "runSpeedMpsPerFrame", 20), 1, 64);
                int baseLife = Math.Max(100, GetInt(n, "baseLife", 500));
                int dialogRadius = GetInt(n, "dialogRadiusCells", 4);
                int atkSpeed = GetInt(n, "attackSpeedFrames", 18);
                int castSpeed = GetInt(n, "castSpeedFrames", 18);
                int hitRecover = GetInt(n, "hitRecoverFrames", 6);
                int atkRadius = GetInt(n, "attackRadiusMps", 96);
                int actRadius = GetInt(n, "activeRadiusMps", 400);
                int visRadius = GetInt(n, "visionRadiusMps", 320);
                int purRadius = GetInt(n, "pursuitRadiusMps", 800);
                int respawnFrames = GetInt(n, "respawnFrames", 180);
                int baseExp = GetInt(n, "baseExp", 80);
                int dropCount = GetInt(n, "treasureDropCount", 2);
                string lootCode = GetString(n, "lootTableCode", "default-monster-loot");

                var skillSlots = new List<NpcSkillSlotTemplate>();
                if (n.TryGetProperty("skills", out JsonElement skArr) && skArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement sk in skArr.EnumerateArray())
                    {
                        skillSlots.Add(new NpcSkillSlotTemplate(
                            GetInt(sk, "skillId", 1),
                            (byte)Math.Clamp(GetInt(sk, "baseLevel", 1), 1, 64),
                            GetInt(sk, "castWeight", 25)));
                    }
                }

                var aiConfig = new NpcAiConfigTemplate();
                if (n.TryGetProperty("aiConfig", out JsonElement aiObj) && aiObj.ValueKind == JsonValueKind.Object)
                {
                    aiConfig = new NpcAiConfigTemplate(
                        GetInt(aiObj, "aiMode", 3),
                        0,
                        0,
                        0,
                        GetInt(aiObj, "aiMaxTimeFrames", 36));
                }

                var scaling = new NpcCombatScalingTemplate();
                if (n.TryGetProperty("combatScaling", out JsonElement csObj) && csObj.ValueKind == JsonValueKind.Object)
                {
                    int lifePerLvl = ReadScalingParam(csObj, "lifeParams", 45);
                    int expPerLvl = ReadScalingParam(csObj, "expParams", 35);
                    int arPerLvl = ReadScalingParam(csObj, "attackRatingParams", 12);
                    int defPerLvl = ReadScalingParam(csObj, "defenseParams", 8);
                    int minDmgPerLvl = ReadScalingParam(csObj, "minDamageParams", 3);
                    int maxDmgPerLvl = ReadScalingParam(csObj, "maxDamageParams", 5);
                    scaling = new NpcCombatScalingTemplate(
                        LifePerLevel: lifePerLvl,
                        ExpPerLevel: expPerLvl,
                        AttackRatingPerLevel: arPerLvl,
                        DefensePerLevel: defPerLvl,
                        MinDamagePerLevel: minDmgPerLvl,
                        MaxDamagePerLevel: maxDmgPerLvl);
                }

                npcs.Add(new NpcTemplate(
                    id,
                    name,
                    kind,
                    camp,
                    series,
                    walkSpeed,
                    runSpeed,
                    baseLife,
                    dialogRadius,
                    code,
                    atkSpeed,
                    castSpeed,
                    hitRecover,
                    atkRadius,
                    actRadius,
                    visRadius,
                    purRadius,
                    respawnFrames,
                    baseExp,
                    dropCount,
                    lootCode,
                    skillSlots,
                    aiConfig,
                    scaling));
            }
        }

        if (root.TryGetProperty("worldSpawnGroups", out JsonElement wsgArr) && wsgArr.ValueKind == JsonValueKind.Array)
        {
            foundWorldSpawns = true;
            foreach (JsonElement wsg in wsgArr.EnumerateArray())
            {
                string cat = GetString(wsg, "spawnCategory", "globalPermanent");
                bool enabled = GetBool(wsg, "enabledByDefault", string.Equals(cat, "globalPermanent", StringComparison.OrdinalIgnoreCase));
                if (!enabled || !string.Equals(cat, "globalPermanent", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ushort groupMapId = (ushort)GetInt(wsg, "mapId", 0);
                int tplId = GetInt(wsg, "npcTemplateId", 0);
                string entityKindStr = GetString(wsg, "entityKind", "DialogNpc");
                EntityKind kind = Enum.TryParse<EntityKind>(entityKindStr, ignoreCase: true, out EntityKind parsedKind)
                    ? parsedKind
                    : EntityKind.DialogNpc;
                short level = (short)Math.Max(1, GetInt(wsg, "level", 1));
                ElementSeries series = (ElementSeries)Math.Clamp(GetInt(wsg, "series", 0), 0, 4);
                EntityCamp camp = (EntityCamp)Math.Clamp(GetInt(wsg, "camp", 0), 0, 7);
                bool isSpecial = GetBool(wsg, "isSpecialNpc", false);
                string groupName = GetString(wsg, "name", GetString(wsg, "nameOverride", $"NPC #{tplId}"));
                string groupScript = GetString(wsg, "scriptPath", string.Empty);

                if (wsg.TryGetProperty("placements", out JsonElement plArr) && plArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement pl in plArr.EnumerateArray())
                    {
                        ushort placementMapId = (ushort)GetInt(pl, "mapId", groupMapId);
                        if (placementMapId == 0)
                        {
                            continue;
                        }

                        autoExecSpawns.Add(new StaticNpcSpawnTemplate(
                            tplId,
                            placementMapId,
                            GetInt(pl, "mpsX", 0),
                            GetInt(pl, "mpsY", 0),
                            level,
                            kind,
                            camp,
                            series,
                            isSpecial,
                            GetString(pl, "nameOverride", groupName),
                            GetString(pl, "scriptPathOverride", groupScript)));
                    }
                }
                else if (wsg.TryGetProperty("spawns", out JsonElement spArr) && spArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement sp in spArr.EnumerateArray())
                    {
                        ushort spawnMapId = (ushort)GetInt(sp, "mapId", groupMapId);
                        if (spawnMapId == 0)
                        {
                            continue;
                        }

                        autoExecSpawns.Add(new StaticNpcSpawnTemplate(
                            tplId,
                            spawnMapId,
                            GetInt(sp, "mpsX", 0),
                            GetInt(sp, "mpsY", 0),
                            level,
                            kind,
                            camp,
                            series,
                            GetBool(sp, "isSpecialNpc", isSpecial),
                            GetString(sp, "nameOverride", groupName),
                            GetString(sp, "scriptPath", groupScript)));
                    }
                }
            }
        }

        if (root.TryGetProperty("worldPortals", out JsonElement wpArr) && wpArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement p in wpArr.EnumerateArray())
            {
                string cat = GetString(p, "spawnCategory", "globalPermanent");
                bool enabled = GetBool(p, "enabledByDefault", string.Equals(cat, "globalPermanent", StringComparison.OrdinalIgnoreCase));
                if (!enabled)
                {
                    continue;
                }

                ushort mapId = (ushort)GetInt(p, "mapId", 0);
                TryAddPortalBinding(p, mapId, portalTrapBindings);
            }
        }

        return foundWorldSpawns;
    }

    private static void ParsePlayerCatalogJson(
        JsonElement root,
        IReadOnlyList<ItemTemplate> loadedItems,
        List<CharacterBaseTemplate> charTemplates,
        List<StarterVillageTemplate> starterVillages,
        List<PlayerSeriesGrowthRuleTemplate> growthRules,
        List<long> levelExpTable,
        List<StarterItemTemplate> defaultStarterItems)
    {
        Dictionary<string, ItemTemplate> itemsByCode = loadedItems
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("characterTemplates", out JsonElement ctArr) && ctArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement ct in ctArr.EnumerateArray())
            {
                ElementSeries series = (ElementSeries)Math.Clamp(GetInt(ct, "elementSeries", 0), 0, 4);
                CharacterGender gender = (CharacterGender)Math.Clamp(GetInt(ct, "gender", 0), 0, 1);
                ushort str = (ushort)GetInt(ct, "baseStrength", 25);
                ushort dex = (ushort)GetInt(ct, "baseDexterity", 25);
                ushort vit = (ushort)GetInt(ct, "baseVitality", 25);
                ushort eng = (ushort)GetInt(ct, "baseEnergy", 25);
                ushort luck = (ushort)GetInt(ct, "baseLucky", 0);
                ushort life = (ushort)GetInt(ct, "initialLifeMax", 100);
                ushort mana = (ushort)GetInt(ct, "initialManaMax", 100);
                ushort stam = (ushort)GetInt(ct, "initialStaminaMax", 100);

                var skills = new List<StarterSkillEntry>();
                if (ct.TryGetProperty("starterSkills", out JsonElement skArr) && skArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement sk in skArr.EnumerateArray())
                    {
                        skills.Add(new StarterSkillEntry(
                            (ushort)GetInt(sk, "skillId", 1),
                            (byte)Math.Clamp(GetInt(sk, "level", 1), 1, 63)));
                    }
                }

                var starterItems = new List<StarterItemTemplate>();
                if (ct.TryGetProperty("starterItems", out JsonElement siArr) && siArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement si in siArr.EnumerateArray())
                    {
                        string itemCode = GetString(si, "itemCode", "kim-sang-duoc");
                        string containerStr = GetString(si, "container", "equipRoom");
                        ItemContainerPlace place = containerStr switch
                        {
                            "equip" => ItemContainerPlace.Equip,
                            "immediacy" => ItemContainerPlace.Immediacy,
                            _ => ItemContainerPlace.EquipRoom,
                        };
                        byte gx = (byte)GetInt(si, "gridX", 0);
                        byte gy = (byte)GetInt(si, "gridY", 0);
                        ushort stack = (ushort)Math.Max(1, GetInt(si, "stackCount", 1));

                        ItemGenreKind genre = ItemGenreKind.Medicine;
                        short detail = 0;
                        short particular = 0;
                        byte lvl = 1;
                        if (itemsByCode.TryGetValue(itemCode, out ItemTemplate? tpl))
                        {
                            genre = tpl.Genre;
                            detail = (short)tpl.DetailType;
                            particular = (short)tpl.ParticularType;
                            lvl = tpl.Level;
                        }
                        else if (itemCode.Contains("tho-dia-phu", StringComparison.OrdinalIgnoreCase))
                        {
                            genre = ItemGenreKind.TownPortal;
                            detail = 0;
                            particular = 1;
                            stack = 1;
                        }
                        else if (itemCode.Contains("ngung-than-dan", StringComparison.OrdinalIgnoreCase))
                        {
                            genre = ItemGenreKind.Medicine;
                            detail = 1;
                            particular = 2;
                        }
                        else
                        {
                            genre = ItemGenreKind.Medicine;
                            detail = 0;
                            particular = 1;
                        }

                        var entry = new StarterItemTemplate(itemCode, genre, detail, particular, lvl, stack, place, gx, gy);
                        starterItems.Add(entry);
                        if (defaultStarterItems.Count < 3)
                        {
                            defaultStarterItems.Add(entry);
                        }
                    }
                }

                charTemplates.Add(new CharacterBaseTemplate(
                    series,
                    gender,
                    str,
                    dex,
                    vit,
                    eng,
                    luck,
                    life,
                    mana,
                    stam,
                    skills,
                    starterItems));
            }
        }

        if (root.TryGetProperty("seriesGrowthRules", out JsonElement grArr) && grArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement gr in grArr.EnumerateArray())
            {
                ElementSeries series = (ElementSeries)Math.Clamp(GetInt(gr, "elementSeries", 0), 0, 4);
                growthRules.Add(new PlayerSeriesGrowthRuleTemplate(
                    series,
                    GetInt(gr, "lifePerLevel", 10),
                    GetInt(gr, "manaPerLevel", 5),
                    GetInt(gr, "staminaMalePerLevel", 4),
                    GetInt(gr, "lifePerVitality", 5),
                    GetInt(gr, "staminaPerVitality", 2),
                    GetInt(gr, "manaPerEnergy", 3),
                    4,
                    1,
                    GetInt(gr, "attributePointsPerLevel", 5),
                    GetInt(gr, "skillPointsPerLevel", 1)));
            }
        }

        if (root.TryGetProperty("levelExpTable", out JsonElement expArr) && expArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement ex in expArr.EnumerateArray())
            {
                long expReq = ex.TryGetProperty("expRequired", out JsonElement er) ? er.GetInt64() : 100;
                levelExpTable.Add(expReq);
            }
        }

        if (root.TryGetProperty("starterVillages", out JsonElement svArr) && svArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement sv in svArr.EnumerateArray())
            {
                ushort mapId = (ushort)GetInt(sv, "mapId", 53);
                string name = GetString(sv, "name", $"Village #{mapId}");
                int defaultRevId = GetInt(sv, "defaultRevivalPointId", 19);
                int spawnX = 52032;
                int spawnY = 101696;
                if (sv.TryGetProperty("revivalPoints", out JsonElement rpArr) &&
                    rpArr.ValueKind == JsonValueKind.Array &&
                    rpArr.GetArrayLength() > 0)
                {
                    JsonElement firstRp = rpArr[0];
                    spawnX = GetInt(firstRp, "mpsX", spawnX);
                    spawnY = GetInt(firstRp, "mpsY", spawnY);
                }

                starterVillages.Add(new StarterVillageTemplate(mapId, name, defaultRevId, spawnX, spawnY));
            }
        }
    }

    private static void ParseMapJson(
        JsonElement root,
        IReadOnlyDictionary<int, string> npcNamesById,
        List<MapDescriptor> mapDescriptors,
        List<PortalTrapBindingTemplate> portalTrapBindings,
        List<StaticNpcSpawnTemplate> autoExecSpawns,
        bool includeMapDialogNpcsInAutoExec)
    {
        if (!root.TryGetProperty("id", out JsonElement idProp))
        {
            return;
        }

        ushort mapId = (ushort)idProp.GetInt32();
        string code = GetString(root, "code", $"map-{mapId}");
        string name = GetString(root, "name", $"Map #{mapId}");

        int leftX = 96;
        int topY = 88;
        int rightX = 111;
        int bottomY = 107;
        if (root.TryGetProperty("regionBounds", out JsonElement rb) && rb.ValueKind == JsonValueKind.Object)
        {
            leftX = GetInt(rb, "leftRegionX", leftX);
            topY = GetInt(rb, "topRegionY", topY);
            rightX = GetInt(rb, "rightRegionX", rightX);
            bottomY = GetInt(rb, "bottomRegionY", bottomY);
        }

        var mapSpawns = new List<StaticNpcSpawnTemplate>();
        if (root.TryGetProperty("spawnGroups", out JsonElement sgArr) && sgArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement sg in sgArr.EnumerateArray())
            {
                int tplId = GetInt(sg, "npcTemplateId", 0);
                string entityKindStr = GetString(sg, "entityKind", "Monster");
                EntityKind kind = Enum.TryParse<EntityKind>(entityKindStr, ignoreCase: true, out EntityKind parsedKind)
                    ? parsedKind
                    : EntityKind.Monster;
                short level = (short)Math.Max(1, GetInt(sg, "level", 1));
                ElementSeries series = (ElementSeries)Math.Clamp(GetInt(sg, "series", 0), 0, 4);
                EntityCamp camp = (EntityCamp)Math.Clamp(GetInt(sg, "camp", 0), 0, 7);
                bool isSpecial = GetBool(sg, "isSpecialNpc", false);
                string fallbackNpcName = npcNamesById.TryGetValue(tplId, out string? tplName) && !string.IsNullOrWhiteSpace(tplName)
                    ? tplName
                    : $"NPC #{tplId}";
                string groupName = GetString(sg, "nameOverride", fallbackNpcName);
                string groupScript = GetString(sg, "scriptPath", string.Empty);

                if (sg.TryGetProperty("spawns", out JsonElement spArr) && spArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement sp in spArr.EnumerateArray())
                    {
                        int mpsX = GetInt(sp, "mpsX", 0);
                        int mpsY = GetInt(sp, "mpsY", 0);
                        string spawnName = GetString(sp, "nameOverride", groupName);
                        string spawnScript = GetString(sp, "scriptPath", groupScript);
                        bool spawnSpecial = GetBool(sp, "isSpecialNpc", isSpecial);

                        var spawnTpl = new StaticNpcSpawnTemplate(
                            tplId,
                            mapId,
                            mpsX,
                            mpsY,
                            level,
                            kind,
                            camp,
                            series,
                            spawnSpecial,
                            spawnName,
                            spawnScript);

                        mapSpawns.Add(spawnTpl);
                        if (includeMapDialogNpcsInAutoExec && kind == EntityKind.DialogNpc)
                        {
                            autoExecSpawns.Add(spawnTpl);
                        }
                    }
                }
            }
        }

        if (root.TryGetProperty("portals", out JsonElement pArr) && pArr.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement p in pArr.EnumerateArray())
            {
                TryAddPortalBinding(p, mapId, portalTrapBindings);
            }
        }

        mapDescriptors.Add(new MapDescriptor(
            mapId,
            name,
            code,
            leftX,
            topY,
            Math.Max(1, rightX - leftX + 1),
            Math.Max(1, bottomY - topY + 1),
            code)
        {
            RegionNpcSpawns = mapSpawns,
        });
    }

    private static void TryAddPortalBinding(JsonElement p, ushort mapId, List<PortalTrapBindingTemplate> portalTrapBindings)
    {
        uint trapId = GetUInt(p, "trapId", 0u);
        string scriptPath = GetString(p, "scriptPath", string.Empty);
        if (trapId == 0u && !string.IsNullOrWhiteSpace(scriptPath))
        {
            trapId = Archives.PakFilePathHasher.ComputeHash(scriptPath);
        }

        string actionStr = GetString(p, "action", "teleport");
        if (string.Equals(actionStr, "unmapped", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PortalTrapActionKind action = actionStr switch
        {
            "toggleFightMode" => PortalTrapActionKind.ToggleFightStateAndSetPos,
            _ => PortalTrapActionKind.ChangeMapPortal,
        };
        ushort targetMapId = (ushort)GetInt(p, "targetMapId", mapId);
        int targetMpsX = GetInt(p, "targetMpsX", 0);
        int targetMpsY = GetInt(p, "targetMpsY", 0);
        byte targetFightMode = (byte)GetInt(p, "targetFightMode", 0);
        int altMpsX = GetInt(p, "alternateMpsX", 0);
        int altMpsY = GetInt(p, "alternateMpsY", 0);
        byte altFightMode = (byte)GetInt(p, "alternateFightMode", 0);
        ushort minLvl = (ushort)Math.Max(1, GetInt(p, "minLevel", 1));
        int bounceX = GetInt(p, "bounceMpsX", 0);
        int bounceY = GetInt(p, "bounceMpsY", 0);
        string? deniedMsg = p.TryGetProperty("deniedMessage", out JsonElement dm) ? dm.GetString() : null;

        portalTrapBindings.Add(new PortalTrapBindingTemplate(
            trapId,
            scriptPath,
            action,
            targetMapId,
            targetMpsX,
            targetMpsY,
            targetFightMode,
            altMpsX,
            altMpsY,
            altFightMode,
            minLvl,
            bounceX,
            bounceY,
            deniedMsg));
    }

    private static void ParseShopCatalogJson(JsonElement root, List<ShopTemplate> shops)
    {
        if (!root.TryGetProperty("shops", out JsonElement shopsArr) || shopsArr.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement sh in shopsArr.EnumerateArray())
        {
            int id = GetInt(sh, "id", shops.Count + 1);
            string code = GetString(sh, "code", $"shop-{id}");
            string name = GetString(sh, "name", code);
            string currencyType = GetString(sh, "currencyType", "gold");
            int buyScale = GetInt(sh, "buyPriceScalePercent", 100);
            int sellDiv = GetInt(sh, "sellBackDivisor", 4);
            var goods = new List<ShopGoodTemplate>();
            if (sh.TryGetProperty("goods", out JsonElement gArr) && gArr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement g in gArr.EnumerateArray())
                {
                    goods.Add(new ShopGoodTemplate(
                        GoodsId: GetInt(g, "goodsId", 1),
                        ItemCode: g.TryGetProperty("itemCode", out JsonElement ic) && ic.ValueKind == JsonValueKind.String ? ic.GetString() : null,
                        Genre: GetInt(g, "genre", 0),
                        DetailType: GetInt(g, "detailType", 0),
                        ParticularType: GetInt(g, "particularType", 0),
                        Level: (byte)Math.Clamp(GetInt(g, "level", 1), 0, 30),
                        ElementSeries: Math.Clamp(GetInt(g, "elementSeries", -1), -1, 6),
                        Price: GetInt(g, "price", 0),
                        DiscountPrice: GetInt(g, "discountPrice", 0),
                        CurrencyTypeOverride: g.TryGetProperty("currencyTypeOverride", out JsonElement co) && co.ValueKind == JsonValueKind.String ? co.GetString() : null,
                        ConsumeType: GetInt(g, "consumeType", 0),
                        ExpireWallClockMinutes: GetInt(g, "expireWallClockMinutes", 0),
                        ExpireOnlineMinutes: GetInt(g, "expireOnlineMinutes", 0)));
                }
            }

            shops.Add(new ShopTemplate(id, code, name, currencyType, buyScale, sellDiv, goods));
        }
    }

    private static SkillParameterCurveTemplate? ParseParameterCurve(JsonElement parent, string propName)
    {
        if (!parent.TryGetProperty(propName, out JsonElement obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string curve = GetString(obj, "curve", "linear");
        int baseVal = GetInt(obj, "base", 0);
        int perLevel = GetInt(obj, "perLevel", 0);
        IReadOnlyList<int>? values = TryReadIntList(obj, "valuesByLevel");
        List<SkillCurveControlPoint>? points = null;
        if (obj.TryGetProperty("points", out JsonElement ptArr) && ptArr.ValueKind == JsonValueKind.Array)
        {
            points = [];
            foreach (JsonElement pt in ptArr.EnumerateArray())
            {
                points.Add(new SkillCurveControlPoint(GetInt(pt, "level", 1), GetInt(pt, "value", 0)));
            }
        }

        return new SkillParameterCurveTemplate(curve, baseVal, perLevel, values, points);
    }

    private static IReadOnlyList<int>? TryReadIntList(JsonElement parent, string propName)
    {
        if (!parent.TryGetProperty(propName, out JsonElement arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<int>(arr.GetArrayLength());
        foreach (JsonElement el in arr.EnumerateArray())
        {
            if (el.TryGetInt32(out int val))
            {
                list.Add(val);
            }
        }

        return list;
    }

    private static int ReadScalingParam(JsonElement csObj, string propName, int fallback)
    {
        if (csObj.TryGetProperty(propName, out JsonElement q) && q.ValueKind == JsonValueKind.Object)
        {
            return GetInt(q, "param1", fallback);
        }

        return fallback;
    }

    private static SkillStyleKind ParseSkillStyle(string style) =>
        style.ToLowerInvariant() switch
        {
            "melee" => SkillStyleKind.Melee,
            "projectile" => SkillStyleKind.Projectile,
            "selfbuff" => SkillStyleKind.SelfBuff,
            "targetbuff" => SkillStyleKind.TargetBuff,
            "aura" => SkillStyleKind.Aura,
            "passive" => SkillStyleKind.Passive,
            "summonnpc" => SkillStyleKind.SummonNpc,
            "jump" => SkillStyleKind.Jump,
            "jumpandattack" => SkillStyleKind.JumpAndAttack,
            "runandattack" => SkillStyleKind.RunAndAttack,
            "multiattack" => SkillStyleKind.MultiAttack,
            "corpseexplode" => SkillStyleKind.CorpseExplode,
            "strideobstacle" => SkillStyleKind.StrideObstacle,
            "steal" => SkillStyleKind.Steal,
            _ => SkillStyleKind.Melee,
        };

    private static MissileMoveKind ParseMissileMoveKind(string moveKind) =>
        moveKind.ToLowerInvariant() switch
        {
            "stand" => MissileMoveKind.Stand,
            "stationary" => MissileMoveKind.Stationary,
            "line64" => MissileMoveKind.Line64,
            "random" => MissileMoveKind.Random,
            "circle" => MissileMoveKind.Circle,
            "spread" => MissileMoveKind.Spread,
            "spiral" => MissileMoveKind.Spiral,
            "helix" => MissileMoveKind.Helix,
            "follow" => MissileMoveKind.Follow,
            "motion" => MissileMoveKind.Motion,
            "parabola" => MissileMoveKind.Parabola,
            "singleline" => MissileMoveKind.SingleLine,
            "rollback" => MissileMoveKind.RollBack,
            "toss" => MissileMoveKind.Toss,
            _ => MissileMoveKind.Line64,
        };

    private static int GetInt(JsonElement el, string name, int fallback) =>
        el.TryGetProperty(name, out JsonElement p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out int v)
            ? v
            : fallback;

    private static uint GetUInt(JsonElement el, string name, uint fallback)
    {
        if (!el.TryGetProperty(name, out JsonElement p) || p.ValueKind != JsonValueKind.Number)
        {
            return fallback;
        }

        if (p.TryGetUInt32(out uint uv))
        {
            return uv;
        }

        if (p.TryGetInt64(out long lv) && lv is >= 0 and <= uint.MaxValue)
        {
            return (uint)lv;
        }

        return fallback;
    }

    private static bool GetBool(JsonElement el, string name, bool fallback) =>
        el.TryGetProperty(name, out JsonElement p) && (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False)
            ? p.GetBoolean()
            : fallback;

    private static string GetString(JsonElement el, string name, string fallback) =>
        el.TryGetProperty(name, out JsonElement p) && p.ValueKind == JsonValueKind.String
            ? (p.GetString() ?? fallback)
            : fallback;
}
