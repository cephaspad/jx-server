using System.Collections.Frozen;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.PlayerTemplates;

/// <summary>
/// Singleton read-only registry providing starter character templates (<c>newplayerini00..09.ini</c>),
/// starter village spawn coordinates (<c>nativeplacelist.ini</c>, <c>revivalid.ini</c>, <c>revivepos.ini</c>),
/// and role name filter validation (<c>goddess_rolename.flt</c>).
/// </summary>
public sealed class PlayerTemplateCatalog : IPlayerTemplateCatalog
{
    private readonly FrozenDictionary<(ElementSeries Series, CharacterGender Gender), CharacterBaseTemplate> _templates;
    private readonly FrozenDictionary<ushort, StarterVillageTemplate> _starterVillages;
    private readonly List<(string Pattern, bool IgnoreCase, bool ExactMatch)> _prohibitedNameRules;

    public IReadOnlyCollection<CharacterBaseTemplate> CharacterTemplates => _templates.Values;

    public IReadOnlyCollection<StarterVillageTemplate> StarterVillages => _starterVillages.Values;

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
                templates[(series, gender)] = ParseNewPlayerIni(File.ReadAllBytes(iniPath), series, gender);
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

    private static CharacterBaseTemplate ParseNewPlayerIni(
        ReadOnlySpan<byte> iniBytes,
        ElementSeries series,
        CharacterGender gender)
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
            skills);
    }

    private static Dictionary<ushort, StarterVillageTemplate> LoadStarterVillages(
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
