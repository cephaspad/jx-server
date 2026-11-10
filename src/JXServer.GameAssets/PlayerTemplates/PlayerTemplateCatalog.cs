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
/// Singleton read-only registry providing starter character templates (<c>newplayerini00..09.ini</c>),
/// series growth rules (<c>level_add.txt</c>), level experience thresholds (<c>level_exp.txt</c>),
/// starter village spawn coordinates (<c>nativeplacelist.ini</c>, <c>revivalid.ini</c>, <c>revivepos.ini</c>),
/// and role name filter validation (<c>goddess_rolename.flt</c>).
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
        if (string.IsNullOrWhiteSpace(serverRoot) || !Directory.Exists(serverRoot))
        {
            throw new DirectoryNotFoundException($"ServerRootPath '{serverRoot}' does not exist (FR-012).");
        }

        string playerDir = Path.Combine(serverRoot, "settings", "npc", "player");
        string levelAddPath = Path.Combine(playerDir, "level_add.txt");
        if (!File.Exists(levelAddPath))
        {
            throw new FileNotFoundException($"Required level_add.txt was not found at '{levelAddPath}' (FR-012).", levelAddPath);
        }

        _defaultStarterItems = CreateDefaultStarterItems();

        var templates = new Dictionary<(ElementSeries, CharacterGender), CharacterBaseTemplate>(10);
        for (int seriesIdx = 0; seriesIdx < 5; seriesIdx++)
        {
            for (int genderIdx = 0; genderIdx < 2; genderIdx++)
            {
                int fileIdx = (seriesIdx * 2) + genderIdx;
                string iniPath = Path.Combine(playerDir, $"newplayerini{fileIdx:D2}.ini");
                if (!File.Exists(iniPath))
                {
                    throw new FileNotFoundException($"Required starter template '{iniPath}' was not found (FR-012).", iniPath);
                }

                ElementSeries series = (ElementSeries)seriesIdx;
                CharacterGender gender = (CharacterGender)genderIdx;
                templates[(series, gender)] = ParseNewPlayerIni(File.ReadAllBytes(iniPath), series, gender, _defaultStarterItems);
            }
        }

        _templates = templates.ToFrozenDictionary();

        string nativePlacePath = Path.Combine(serverRoot, "settings", "nativeplacelist.ini");
        string revivalIdPath = Path.Combine(playerDir, "revivalid.ini");
        string revivePosPath = Path.Combine(serverRoot, "settings", "revivepos.ini");

        if (!File.Exists(nativePlacePath))
        {
            throw new FileNotFoundException($"Required nativeplacelist.ini was not found at '{nativePlacePath}' (FR-012).", nativePlacePath);
        }

        if (!File.Exists(revivalIdPath))
        {
            throw new FileNotFoundException($"Required revivalid.ini was not found at '{revivalIdPath}' (FR-012).", revivalIdPath);
        }

        if (!File.Exists(revivePosPath))
        {
            throw new FileNotFoundException($"Required revivepos.ini was not found at '{revivePosPath}' (FR-012).", revivePosPath);
        }

        _starterVillages = LoadStarterVillages(
            File.ReadAllBytes(nativePlacePath),
            File.ReadAllBytes(revivalIdPath),
            File.ReadAllBytes(revivePosPath)).ToFrozenDictionary();

        _growthRules = ParseLevelAddTxt(File.ReadAllBytes(levelAddPath)).ToFrozenDictionary(x => x.Series);

        string levelExpPath = Path.Combine(playerDir, "level_exp.txt");
        _levelExpTable = File.Exists(levelExpPath)
            ? ParseLevelExpTxt(File.ReadAllBytes(levelExpPath))
            : CreateDefaultLevelExpTable();

        if (GameAssetsSnapshotLoader.TryLoadBundle(cfg, out GameAssetsSnapshotBundle? bundle, out _) &&
            bundle is not null &&
            bundle.DefaultStarterItems.Count > 0)
        {
            _defaultStarterItems = bundle.DefaultStarterItems;
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

    public static CharacterBaseTemplate ParseNewPlayerIni(
        ReadOnlySpan<byte> iniBytes,
        ElementSeries series,
        CharacterGender gender,
        IReadOnlyList<StarterItemTemplate>? starterItems = null)
    {
        Dictionary<string, Dictionary<string, string>> sections = IniFileParser.ParseSections(iniBytes);
        if (!sections.TryGetValue("ROLE", out Dictionary<string, string>? roleSection))
        {
            throw new InvalidDataException($"Missing [ROLE] section in newplayerini for ({series}, {gender}).");
        }

        ushort power = GetUShort(roleSection, "ipower", 25);
        ushort agility = GetUShort(roleSection, "iagility", 25);
        ushort outer = GetUShort(roleSection, "iouter", 25);
        ushort inside = GetUShort(roleSection, "iinside", 25);
        ushort luck = GetUShort(roleSection, "iluck", 0);
        ushort maxLife = GetUShort(roleSection, "imaxlife", 100);
        ushort maxInner = GetUShort(roleSection, "imaxinner", 100);
        ushort maxStamina = GetUShort(roleSection, "imaxstamina", 100);

        var skills = new List<StarterSkillEntry>();
        if (sections.TryGetValue("FSKILLS", out Dictionary<string, string>? skillSection))
        {
            int count = GetUShort(skillSection, "COUNT", 0);
            for (int i = 1; i <= count; i++)
            {
                ushort skillId = GetUShort(skillSection, $"S{i}", 0);
                byte level = (byte)Math.Clamp((int)GetUShort(skillSection, $"L{i}", 1), 1, 63);
                if (skillId > 0)
                {
                    skills.Add(new StarterSkillEntry(skillId, level));
                }
            }
        }

        return new CharacterBaseTemplate(
            series,
            gender,
            power,
            agility,
            outer,
            inside,
            luck,
            maxLife,
            maxInner,
            maxStamina,
            skills,
            starterItems ?? CreateDefaultStarterItems());
    }

    public static Dictionary<ushort, StarterVillageTemplate> LoadStarterVillages(
        ReadOnlySpan<byte> nativePlaceBytes,
        ReadOnlySpan<byte> revivalIdBytes,
        ReadOnlySpan<byte> revivePosBytes)
    {
        Dictionary<string, Dictionary<string, string>> nativeSections = IniFileParser.ParseSections(nativePlaceBytes);
        Dictionary<string, Dictionary<string, string>> revivalIdSections = IniFileParser.ParseSections(revivalIdBytes);
        Dictionary<string, Dictionary<string, string>> revivePosSections = IniFileParser.ParseSections(revivePosBytes);

        var villages = new Dictionary<ushort, StarterVillageTemplate>();
        int count = nativeSections.TryGetValue("List", out Dictionary<string, string>? listSec)
            ? GetUShort(listSec, "Count", 8)
            : 8;

        for (int i = 0; i < count; i++)
        {
            if (!nativeSections.TryGetValue(i.ToString(), out Dictionary<string, string>? entrySec))
            {
                continue;
            }

            ushort mapId = GetUShort(entrySec, "Id", 0);
            if (mapId == 0)
            {
                continue;
            }

            string villageName = entrySec.GetValueOrDefault("Name", $"Village #{mapId}");
            int revivalId = 19;
            if (revivalIdSections.TryGetValue(mapId.ToString(), out Dictionary<string, string>? revSec) &&
                revSec.TryGetValue("RevivalId00", out string? revIdStr) &&
                int.TryParse(revIdStr, out int parsedRevId))
            {
                revivalId = parsedRevId;
            }

            int spawnX = 52032;
            int spawnY = 101696;
            if (revivePosSections.TryGetValue(mapId.ToString(), out Dictionary<string, string>? posSec) &&
                posSec.TryGetValue(revivalId.ToString(), out string? coordsStr))
            {
                string[] parts = coordsStr.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0], out int x) &&
                    int.TryParse(parts[1], out int y))
                {
                    spawnX = x;
                    spawnY = y;
                }
            }

            villages[mapId] = new StarterVillageTemplate(mapId, villageName, revivalId, spawnX, spawnY);
        }

        return villages;
    }

    public static IReadOnlyList<PlayerSeriesGrowthRuleTemplate> ParseLevelAddTxt(ReadOnlySpan<byte> rawBytes)
    {
        string text = Tcvn3Encoding.DecodeNullTerminated(rawBytes);
        string[] lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries);
        var rules = new Dictionary<ElementSeries, PlayerSeriesGrowthRuleTemplate>();

        for (int i = 1; i < lines.Length && rules.Count < 5; i++)
        {
            string[] cols = lines[i].Split('\t');
            if (cols.Length < 8)
            {
                continue;
            }

            ElementSeries series = (ElementSeries)Math.Clamp(i - 1, 0, 4);
            int lifePerLevel = ParseInt(cols, 1, 10);
            int staminaPerLevel = ParseInt(cols, 2, 4);
            int manaPerLevel = ParseInt(cols, 3, 5);
            int lifePerVit = ParseInt(cols, 4, 5);
            int stamPerVit = ParseInt(cols, 5, 2);
            int manaPerEng = ParseInt(cols, 6, 3);

            rules[series] = new PlayerSeriesGrowthRuleTemplate(
                series,
                Math.Max(1, lifePerLevel),
                Math.Max(1, manaPerLevel),
                Math.Max(1, staminaPerLevel),
                Math.Max(1, lifePerVit),
                Math.Max(1, stamPerVit),
                Math.Max(1, manaPerEng));
        }

        for (int s = 0; s < 5; s++)
        {
            ElementSeries series = (ElementSeries)s;
            if (!rules.ContainsKey(series))
            {
                rules[series] = new PlayerSeriesGrowthRuleTemplate(series, 10, 5, 4, 5, 2, 3);
            }
        }

        return rules.Values.OrderBy(r => r.Series).ToArray();
    }

    public static long[] ParseLevelExpTxt(ReadOnlySpan<byte> rawBytes)
    {
        string text = Tcvn3Encoding.DecodeNullTerminated(rawBytes);
        string[] lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries);
        var list = new List<long>();
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split('\t');
            if (cols.Length >= 2 && long.TryParse(cols[1].Trim(), out long exp) && exp > 0)
            {
                list.Add(exp);
            }
        }

        return list.Count > 0 ? list.ToArray() : CreateDefaultLevelExpTable();
    }

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

    private static int ParseInt(string[] cols, int idx, int fallback) =>
        (uint)idx < (uint)cols.Length && int.TryParse(cols[idx].Trim(), out int v) ? v : fallback;

    private static List<(string Pattern, bool IgnoreCase, bool ExactMatch)> LoadRoleNameFilterRules(
        string gatewayRootPath,
        string serverRootPath)
    {
        string[] candidates =
        [
            Path.Combine(gatewayRootPath, "settings", "goddess_rolename.flt"),
            Path.Combine(gatewayRootPath, "goddess_rolename.flt"),
            Path.Combine(serverRootPath, "settings", "rolename.flt")
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

    private static ushort GetUShort(Dictionary<string, string> section, string key, ushort defaultValue) =>
        section.TryGetValue(key, out string? raw) && ushort.TryParse(raw, out ushort value) ? value : defaultValue;
}
