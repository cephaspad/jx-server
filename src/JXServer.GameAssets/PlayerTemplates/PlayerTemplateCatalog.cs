using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.Snapshots;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.Items;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.PlayerTemplates;

/// <summary>
/// Singleton read-only registry providing starter character templates,
/// series growth rules, level experience thresholds, starter village spawn coordinates,
/// and role name filter validation (<c>JXData Standard v1</c> Dual-Tier loader).
/// </summary>
public sealed class PlayerTemplateCatalog : IPlayerTemplateCatalog
{
    private readonly FrozenDictionary<(ElementSeries Series, CharacterGender Gender), CharacterBaseTemplate> _templates;
    private readonly FrozenDictionary<ushort, StarterVillageTemplate> _starterVillages;
    private readonly FrozenDictionary<ElementSeries, PlayerSeriesGrowthRuleTemplate> _growthRules;
    private readonly long[] _levelExpTable;
    private readonly IReadOnlyList<StarterItemTemplate> _defaultStarterItems;
    private readonly List<(string Pattern, bool IgnoreCase, bool ExactMatch)> _prohibitedNameRules;

    public IReadOnlyCollection<CharacterBaseTemplate> CharacterTemplates => _templates.Values;

    public IReadOnlyCollection<StarterVillageTemplate> StarterVillages => _starterVillages.Values;

    public IReadOnlyList<StarterItemTemplate> DefaultStarterItems => _defaultStarterItems;

    public PlayerTemplateCatalog(IOptions<GameAssetsOptions> options, ILogger<PlayerTemplateCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        GameAssetsOptions cfg = options.Value;

        string serverRoot = cfg.ServerRootPath;
        if (!string.IsNullOrWhiteSpace(serverRoot) && !Directory.Exists(serverRoot))
        {
            throw new DirectoryNotFoundException($"ServerRootPath '{serverRoot}' does not exist (FR-012).");
        }

        if (GameAssetsSnapshotLoader.TryLoadBundle(cfg, out GameAssetsSnapshotBundle? bundle, out _) &&
            bundle is not null &&
            bundle.CharacterTemplates.Count > 0)
        {
            _templates = bundle.CharacterTemplates.ToFrozenDictionary(t => (t.Series, t.Gender));
            _starterVillages = bundle.StarterVillages.ToFrozenDictionary(v => v.MapId);
            _growthRules = bundle.SeriesGrowthRules.ToFrozenDictionary(g => g.Series);
            _levelExpTable = bundle.LevelExpTable.Count > 0 ? bundle.LevelExpTable.ToArray() : CreateDefaultLevelExpTable();
            _defaultStarterItems = bundle.DefaultStarterItems.Count > 0 ? bundle.DefaultStarterItems : CreateDefaultStarterItems();
        }
        else
        {
            _defaultStarterItems = CreateDefaultStarterItems();
            _templates = CreateDefaultTemplates(_defaultStarterItems).ToFrozenDictionary(t => (t.Series, t.Gender));
            _starterVillages = CreateDefaultStarterVillages().ToFrozenDictionary(v => v.MapId);
            _growthRules = CreateDefaultGrowthRules().ToFrozenDictionary(g => g.Series);
            _levelExpTable = CreateDefaultLevelExpTable();
        }

        _prohibitedNameRules = LoadRoleNameFilterRules(cfg.GatewayRootPath, serverRoot);

        GameAssetsLogMessages.LogPlayerTemplatesLoaded(
            logger,
            _templates.Count,
            _starterVillages.Count,
            _prohibitedNameRules.Count);
    }

    public bool TryGetCharacterTemplate(ElementSeries series, CharacterGender gender, out CharacterBaseTemplate? template) =>
        _templates.TryGetValue((series, gender), out template);

    public bool TryGetStarterVillage(ushort villageMapId, out StarterVillageTemplate? village) =>
        _starterVillages.TryGetValue(villageMapId, out village);

    public PlayerSeriesGrowthRuleTemplate GetSeriesGrowthRule(ElementSeries series) =>
        _growthRules.TryGetValue(series, out PlayerSeriesGrowthRuleTemplate? rule)
            ? rule
            : new PlayerSeriesGrowthRuleTemplate(series, 10, 5, 4, 5, 2, 3);

    public long GetRequiredExpForNextLevel(int currentLevel)
    {
        int idx = Math.Clamp(currentLevel - 1, 0, _levelExpTable.Length - 1);
        return Math.Max(100, _levelExpTable[idx]);
    }

    public bool IsRoleNameProhibited(string roleNameUnicode)
    {
        if (string.IsNullOrWhiteSpace(roleNameUnicode))
        {
            return true;
        }

        foreach (char ch in roleNameUnicode)
        {
            if (char.IsControl(ch) || ch is '\t' or '\r' or '\n' or '/' or '\\' or '\'' or '"' or ';' or ',' or '%')
            {
                return true;
            }
        }

        string tcvnAsciiDecoded = Tcvn3Encoding.DecodeNullTerminated(Tcvn3Encoding.EncodeBytes(roleNameUnicode));
        foreach (var (pattern, ignoreCase, exactMatch) in _prohibitedNameRules)
        {
            StringComparison cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (exactMatch)
            {
                if (string.Equals(roleNameUnicode, pattern, cmp) ||
                    string.Equals(tcvnAsciiDecoded, pattern, cmp))
                {
                    return true;
                }
            }
            else
            {
                if (roleNameUnicode.Contains(pattern, cmp) ||
                    tcvnAsciiDecoded.Contains(pattern, cmp))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static IReadOnlyList<StarterItemTemplate> CreateDefaultStarterItems() =>
    [
        new StarterItemTemplate("kim-sang-duoc", ItemGenreKind.Medicine, 0, 1, 1, 5, ItemContainerPlace.EquipRoom, 0, 0),
        new StarterItemTemplate("ngung-than-dan", ItemGenreKind.Medicine, 1, 2, 1, 5, ItemContainerPlace.EquipRoom, 1, 0),
        new StarterItemTemplate("tho-dia-phu", ItemGenreKind.TownPortal, 0, 1, 1, 1, ItemContainerPlace.EquipRoom, 2, 0),
    ];

    private static List<CharacterBaseTemplate> CreateDefaultTemplates(IReadOnlyList<StarterItemTemplate> starterItems)
    {
        var list = new List<CharacterBaseTemplate>(10);
        (ElementSeries Series, CharacterGender Gender, ushort Str, ushort Dex, ushort Vit, ushort Eng, ushort Life, ushort Mana, ushort Stam, ushort SkillCount)[] data =
        [
            (ElementSeries.Metal, CharacterGender.Male, 35, 25, 25, 15, 204, 16, 100, 7),
            (ElementSeries.Metal, CharacterGender.Female, 30, 25, 25, 20, 204, 16, 100, 7),
            (ElementSeries.Wood, CharacterGender.Male, 20, 35, 20, 25, 100, 100, 100, 7),
            (ElementSeries.Wood, CharacterGender.Female, 20, 30, 20, 30, 100, 100, 100, 7),
            (ElementSeries.Water, CharacterGender.Male, 20, 25, 25, 30, 100, 200, 100, 7),
            (ElementSeries.Water, CharacterGender.Female, 15, 25, 25, 35, 153, 77, 100, 7),
            (ElementSeries.Fire, CharacterGender.Male, 25, 25, 25, 25, 100, 100, 100, 7),
            (ElementSeries.Fire, CharacterGender.Female, 25, 25, 25, 25, 100, 100, 100, 7),
            (ElementSeries.Earth, CharacterGender.Male, 25, 25, 25, 25, 100, 100, 100, 7),
            (ElementSeries.Earth, CharacterGender.Female, 25, 25, 25, 25, 100, 100, 100, 7),
        ];

        foreach (var entry in data)
        {
            var skills = new List<StarterSkillEntry>();
            for (ushort s = 1; s <= entry.SkillCount; s++)
            {
                skills.Add(new StarterSkillEntry(s, 1));
            }

            list.Add(new CharacterBaseTemplate(
                entry.Series,
                entry.Gender,
                entry.Str,
                entry.Dex,
                entry.Vit,
                entry.Eng,
                0,
                entry.Life,
                entry.Mana,
                entry.Stam,
                skills,
                starterItems));
        }

        return list;
    }

    private static List<StarterVillageTemplate> CreateDefaultStarterVillages() =>
    [
        new StarterVillageTemplate(53, "Ba Lăng Huyện", 19, 52032, 101696),
        new StarterVillageTemplate(54, "Giang Tân Thôn", 20, 52032, 101696),
        new StarterVillageTemplate(70, "Vĩnh Lạc Trấn", 21, 52032, 101696),
        new StarterVillageTemplate(74, "Chu Tiên Trấn", 22, 52032, 101696),
        new StarterVillageTemplate(101, "Đạo Hương Thôn", 23, 52032, 101696),
        new StarterVillageTemplate(121, "Long Tuyền Thôn", 24, 52032, 101696),
        new StarterVillageTemplate(131, "Bạch Hà Trấn", 25, 52032, 101696),
        new StarterVillageTemplate(141, "Long Môn Trấn", 26, 52032, 101696),
    ];

    private static List<PlayerSeriesGrowthRuleTemplate> CreateDefaultGrowthRules() =>
    [
        new PlayerSeriesGrowthRuleTemplate(ElementSeries.Metal, 10, 5, 4, 5, 2, 3),
        new PlayerSeriesGrowthRuleTemplate(ElementSeries.Wood, 10, 5, 4, 5, 2, 3),
        new PlayerSeriesGrowthRuleTemplate(ElementSeries.Water, 10, 5, 4, 5, 2, 3),
        new PlayerSeriesGrowthRuleTemplate(ElementSeries.Fire, 10, 5, 4, 5, 2, 3),
        new PlayerSeriesGrowthRuleTemplate(ElementSeries.Earth, 10, 5, 4, 5, 2, 3),
    ];

    private static long[] CreateDefaultLevelExpTable()
    {
        var table = new long[200];
        for (int i = 0; i < table.Length; i++)
        {
            int lvl = i + 1;
            table[i] = 100L * lvl * lvl;
        }

        return table;
    }

    private static List<(string Pattern, bool IgnoreCase, bool ExactMatch)> LoadRoleNameFilterRules(
        string gatewayRootPath,
        string serverRootPath)
    {
        string[] candidates =
        [
            Path.Combine(gatewayRootPath, "settings", "goddess_rolename.flt"),
            Path.Combine(gatewayRootPath, "goddess_rolename.flt"),
            Path.Combine(serverRootPath, "settings", "rolename.flt"),
            Path.Combine(serverRootPath, "settings", "goddess_rolename.flt"),
            Path.Combine(serverRootPath, "goddess_rolename.flt"),
        ];

        string? fltPath = candidates.FirstOrDefault(File.Exists);
        var rules = new List<(string Pattern, bool IgnoreCase, bool ExactMatch)>();
        if (fltPath is null)
        {
            rules.Add(("GM", true, false));
            rules.Add(("GameMaster", true, false));
            return rules;
        }

        byte[] rawBytes = File.ReadAllBytes(fltPath);
        string text = Tcvn3Encoding.DecodeNullTerminated(rawBytes);
        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }

            bool ignoreCase = false;
            if (line.StartsWith('@'))
            {
                ignoreCase = true;
                line = line[1..];
            }

            bool exactMatch = false;
            if (line.StartsWith('+'))
            {
                line = line[1..];
            }
            else if (line.StartsWith('='))
            {
                exactMatch = true;
                line = line[1..];
            }

            if (!string.IsNullOrEmpty(line))
            {
                rules.Add((line, ignoreCase, exactMatch));
            }
        }

        return rules;
    }
}
