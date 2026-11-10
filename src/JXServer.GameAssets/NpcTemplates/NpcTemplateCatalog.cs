using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.Snapshots;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.WorldZones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.NpcTemplates;

/// <summary>
/// Singleton read-only catalog loading NPC templates from <c>JXData Standard v1</c> (<c>artifacts/jxdata.pack</c> / <c>data/npcs/*.json</c>)
/// or legacy <c>settings/npcs.txt</c> and <c>script/global/autoexec.lua</c> (`FR-001`, `FR-004`).
/// </summary>
public sealed partial class NpcTemplateCatalog : INpcTemplateCatalog
{
    private readonly FrozenDictionary<int, NpcTemplate> _templates;
    private readonly FrozenDictionary<ushort, IReadOnlyList<StaticNpcSpawnTemplate>> _autoExecSpawnsByMap;

    public int Count => _templates.Count;

    public NpcTemplateCatalog(IOptions<GameAssetsOptions> options, ILogger<NpcTemplateCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        string serverRoot = options.Value.ServerRootPath;
        string npcsTxtPath = Path.Combine(serverRoot, "settings", "npcs.txt");
        if (!File.Exists(npcsTxtPath))
        {
            if (GameAssetsSnapshotLoader.TryLoadBundle(options.Value, out GameAssetsSnapshotBundle? bundle, out string sourceTier) &&
                bundle is not null &&
                bundle.NpcTemplates.Count > 0)
            {
                _templates = bundle.NpcTemplates.ToFrozenDictionary(x => x.TemplateId);
                _autoExecSpawnsByMap = bundle.AutoExecSpawns
                    .GroupBy(x => x.MapId)
                    .ToFrozenDictionary(g => g.Key, g => (IReadOnlyList<StaticNpcSpawnTemplate>)g.ToArray());
                GameAssetsLogMessages.LogNpcTemplatesLoaded(logger, _templates.Count, sourceTier, bundle.AutoExecSpawns.Count);
                return;
            }

            throw new FileNotFoundException($"Required NPC template table was not found at '{npcsTxtPath}' (FR-004).", npcsTxtPath);
        }

        byte[] npcsBytes = File.ReadAllBytes(npcsTxtPath);
        Dictionary<int, NpcTemplate> parsedTemplates = ParseNpcsTxt(npcsBytes);
        _templates = parsedTemplates.ToFrozenDictionary();

        string autoExecPath = Path.Combine(serverRoot, "script", "global", "autoexec.lua");
        int autoExecCount = 0;
        var spawnsByMap = new Dictionary<ushort, List<StaticNpcSpawnTemplate>>();
        if (File.Exists(autoExecPath))
        {
            byte[] autoExecBytes = File.ReadAllBytes(autoExecPath);
            foreach (StaticNpcSpawnTemplate spawn in ParseAutoExecSpawns(autoExecBytes, _templates))
            {
                if (!spawnsByMap.TryGetValue(spawn.MapId, out List<StaticNpcSpawnTemplate>? list))
                {
                    list = [];
                    spawnsByMap[spawn.MapId] = list;
                }

                list.Add(spawn);
                autoExecCount++;
            }
        }

        _autoExecSpawnsByMap = spawnsByMap.ToFrozenDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<StaticNpcSpawnTemplate>)kvp.Value.ToArray());

        GameAssetsLogMessages.LogNpcTemplatesLoaded(logger, _templates.Count, npcsTxtPath, autoExecCount);
    }

    public bool TryGetTemplate(int templateId, out NpcTemplate? template) =>
        _templates.TryGetValue(templateId, out template);

    public IReadOnlyList<StaticNpcSpawnTemplate> GetAutoExecSpawnsForMap(ushort mapId) =>
        _autoExecSpawnsByMap.TryGetValue(mapId, out IReadOnlyList<StaticNpcSpawnTemplate>? spawns)
            ? spawns
            : [];

    public static Dictionary<int, NpcTemplate> ParseNpcsTxt(ReadOnlySpan<byte> rawBytes)
    {
        var result = new Dictionary<int, NpcTemplate>();
        string text = Tcvn3Encoding.DecodeNullTerminated(rawBytes);
        string[] lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        if (lines.Length < 3)
        {
            return result;
        }

        string[] headers = lines[0].Split('\t');
        int nameCol = FindColumnIndex(headers, "Name", fallback: 0);
        int kindCol = FindColumnIndex(headers, "Kind", fallback: 1);
        int campCol = FindColumnIndex(headers, "Camp", fallback: 2);
        int seriesCol = FindColumnIndex(headers, "Series", fallback: 3);
        int walkSpeedCol = FindColumnIndex(headers, "WalkSpeed", fallback: 30);
        int runSpeedCol = FindColumnIndex(headers, "RunSpeed", fallback: 31);
        int lifeParamCol = FindColumnIndex(headers, "LifeParam", fallback: 40);
        int expParamCol = FindColumnIndex(headers, "ExpParam", fallback: 37);
        int activeRadiusCol = FindColumnIndex(headers, "ActiveRadius", fallback: 33);
        int visionRadiusCol = FindColumnIndex(headers, "VisionRadius", fallback: 34);

        // Row 0 is headers; Row 1 is baseline defaults; Row 2+ map to 0-based nTemplateID = lineIdx - 2
        int templateId = 0;
        for (int lineIdx = 2; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx];
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            string[] cols = line.Split('\t');
            string rawName = GetCol(cols, nameCol).Trim();
            string displayName = string.IsNullOrWhiteSpace(rawName) ? $"NPC #{templateId}" : rawName;

            int kindVal = ParseIntOrDefault(GetCol(cols, kindCol), 0);
            EntityKind kind = kindVal switch
            {
                1 => EntityKind.Player,
                2 => EntityKind.Partner,
                3 => EntityKind.DialogNpc,
                _ => EntityKind.Monster,
            };

            int campVal = ParseIntOrDefault(GetCol(cols, campCol), 0);
            EntityCamp camp = Enum.IsDefined(typeof(EntityCamp), (byte)campVal)
                ? (EntityCamp)campVal
                : EntityCamp.Begin;

            int seriesVal = ParseIntOrDefault(GetCol(cols, seriesCol), 0);
            ElementSeries series = Enum.IsDefined(typeof(ElementSeries), (byte)seriesVal)
                ? (ElementSeries)seriesVal
                : ElementSeries.Metal;

            int walkSpeed = Math.Clamp(ParseIntOrDefault(GetCol(cols, walkSpeedCol), 10), 1, 64);
            int runSpeed = Math.Clamp(ParseIntOrDefault(GetCol(cols, runSpeedCol), 20), 1, 64);
            int baseLife = Math.Max(100, ParseIntOrDefault(GetCol(cols, lifeParamCol), 500));
            int baseExp = Math.Max(20, ParseIntOrDefault(GetCol(cols, expParamCol), 80));
            int activeRadius = Math.Clamp(ParseIntOrDefault(GetCol(cols, activeRadiusCol), 400), 128, 2048);
            int visionRadius = Math.Clamp(ParseIntOrDefault(GetCol(cols, visionRadiusCol), 320), 96, 1024);

            result[templateId] = new NpcTemplate(
                templateId,
                displayName,
                kind,
                camp,
                series,
                walkSpeed,
                runSpeed,
                baseLife,
                DialogRadius: 4,
                Code: $"npc-{templateId}",
                AttackSpeedFrames: 18,
                CastSpeedFrames: 18,
                HitRecoverFrames: 6,
                AttackRadiusMps: 96,
                ActiveRadiusMps: activeRadius,
                VisionRadiusMps: visionRadius,
                PursuitRadiusMps: Math.Max(activeRadius * 2, 640),
                RespawnFrames: 180,
                BaseExp: baseExp,
                TreasureDropCount: 2,
                LootTableCode: "default-monster-loot",
                Skills: [new NpcSkillSlotTemplate(29, 1, 100)],
                AiConfig: new NpcAiConfigTemplate(3, 0, 0, 0, 36),
                CombatScaling: new NpcCombatScalingTemplate());

            templateId++;
        }

        return result;
    }

    public static List<StaticNpcSpawnTemplate> ParseAutoExecSpawns(
        ReadOnlySpan<byte> autoExecBytes,
        IReadOnlyDictionary<int, NpcTemplate> templates)
    {
        var spawns = new List<StaticNpcSpawnTemplate>();
        byte[] unescapedBytes = UnescapeLuaDecimalBytes(autoExecBytes);
        string text = Tcvn3Encoding.DecodeNullTerminated(unescapedBytes);

        // Matches Lua entries like: {1783, 53, 1593, 3188, "\\script\\...", "Người hướng dẫn bang hội"}
        foreach (Match match in AutoExecTupleRegex().Matches(text))
        {
            if (!int.TryParse(match.Groups[1].Value, out int templateId) ||
                !ushort.TryParse(match.Groups[2].Value, out ushort mapId) ||
                !int.TryParse(match.Groups[3].Value, out int coordX) ||
                !int.TryParse(match.Groups[4].Value, out int coordY))
            {
                continue;
            }

            string scriptPath = match.Groups[5].Value.Trim();
            string explicitName = match.Groups[6].Success ? match.Groups[6].Value.Trim() : string.Empty;

            templates.TryGetValue(templateId, out NpcTemplate? tpl);
            string displayName = !string.IsNullOrWhiteSpace(explicitName)
                ? explicitName
                : (tpl?.Name ?? $"NPC #{templateId}");

            // In autoexec.lua adddialognpc, coordinates < 8192 are cell coordinates (cellX, cellY) -> Mps = cell * 32
            int mpsX = coordX < 8192 ? coordX * WorldCoordinateConsts.CellWidthMps : coordX;
            int mpsY = coordY < 16384 ? coordY * WorldCoordinateConsts.CellHeightMps : coordY;

            spawns.Add(new StaticNpcSpawnTemplate(
                TemplateId: templateId,
                MapId: mapId,
                SpawnMpsX: mpsX,
                SpawnMpsY: mpsY,
                Level: 1,
                Kind: tpl?.Kind ?? EntityKind.DialogNpc,
                Camp: tpl?.Camp ?? EntityCamp.Begin,
                Series: tpl?.Series ?? ElementSeries.Metal,
                IsSpecialNpc: false,
                DisplayName: displayName,
                ScriptPath: scriptPath));
        }

        return spawns;
    }

    private static int FindColumnIndex(string[] headers, string columnName, int fallback)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            if (string.Equals(headers[i].Trim(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return fallback;
    }

    private static string GetCol(string[] cols, int index) =>
        (uint)index < (uint)cols.Length ? cols[index] : string.Empty;

    private static int ParseIntOrDefault(string text, int defaultValue) =>
        int.TryParse(text.Trim(), out int val) ? val : defaultValue;

    private static byte[] UnescapeLuaDecimalBytes(ReadOnlySpan<byte> input)
    {
        byte[] output = new byte[input.Length];
        int outIdx = 0;
        int i = 0;
        while (i < input.Length)
        {
            if (input[i] == (byte)'\\' &&
                i + 3 < input.Length &&
                input[i + 1] is >= (byte)'0' and <= (byte)'9' &&
                input[i + 2] is >= (byte)'0' and <= (byte)'9' &&
                input[i + 3] is >= (byte)'0' and <= (byte)'9')
            {
                int val = (input[i + 1] - '0') * 100 + (input[i + 2] - '0') * 10 + (input[i + 3] - '0');
                if (val <= 255)
                {
                    output[outIdx++] = (byte)val;
                    i += 4;
                    continue;
                }
            }

            output[outIdx++] = input[i++];
        }

        return output[..outIdx];
    }

    [GeneratedRegex(@"\{\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*""([^""]*)""(?:\s*,\s*""([^""]*)"")?\s*\}", RegexOptions.Compiled)]
    private static partial Regex AutoExecTupleRegex();
}
