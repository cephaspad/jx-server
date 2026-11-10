using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using JXServer.GameAssets.Archives;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.WorldZones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Scans <c>script/**/trap/*.lua</c> on startup, hashes their canonical <c>\script\...</c> GBK paths via <see cref="PakFilePathHasher"/>,
/// and parses both Town Gate Fight-State traps and Cross-Map Portal traps (`FR-008`).
/// </summary>
public sealed partial class TrapScriptCatalog : ITrapScriptCatalog
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly FrozenDictionary<uint, PortalTrapBindingTemplate> _bindings;

    public int Count => _bindings.Count;

    public TrapScriptCatalog(IOptions<GameAssetsOptions> options, ILogger<TrapScriptCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        string serverRoot = options.Value.ServerRootPath;
        string scriptRoot = Path.Combine(serverRoot, "script");
        var bindings = new Dictionary<uint, PortalTrapBindingTemplate>();

        if (Directory.Exists(scriptRoot))
        {
            foreach (string luaFile in Directory.EnumerateFiles(scriptRoot, "*.lua", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(serverRoot, luaFile);
                if (!relativePath.Contains($"{Path.DirectorySeparatorChar}trap{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string canonicalPath = "\\" + relativePath.Replace('/', '\\');
                uint[] candidateHashes = ComputeCandidateHashes(canonicalPath);
                byte[] fileBytes = File.ReadAllBytes(luaFile);
                if (TryParseLuaTrapFile(canonicalPath, candidateHashes[0], fileBytes, out PortalTrapBindingTemplate? parsed) &&
                    parsed is not null)
                {
                    foreach (uint hash in candidateHashes)
                    {
                        bindings.TryAdd(hash, parsed with { TrapId = hash });
                    }
                }
            }
        }

        _bindings = bindings.ToFrozenDictionary();
        GameAssetsLogMessages.LogTrapScriptsLoaded(logger, _bindings.Count, scriptRoot);
    }

    public bool TryGetTrapBinding(uint trapId, out PortalTrapBindingTemplate? binding) =>
        _bindings.TryGetValue(trapId, out binding);

    private static uint[] ComputeCandidateHashes(string canonicalPath)
    {
        // On Windows/NTFS unpacked from Linux archives, filenames may be either Latin1-mojibake of GBK bytes or direct GBK/Unicode
        byte[] latin1Bytes = Latin1.GetBytes(canonicalPath);
        uint hashFromLatin1Bytes = PakFilePathHasher.ComputeHash(latin1Bytes);
        uint hashFromGbkString = PakFilePathHasher.ComputeHash(canonicalPath);

        return hashFromLatin1Bytes == hashFromGbkString
            ? [hashFromLatin1Bytes]
            : [hashFromLatin1Bytes, hashFromGbkString];
    }

    private static bool TryParseLuaTrapFile(
        string scriptPath,
        uint primaryTrapId,
        ReadOnlySpan<byte> rawBytes,
        out PortalTrapBindingTemplate? binding)
    {
        binding = null;
        string content = Tcvn3Encoding.DecodeNullTerminated(rawBytes);

        // 1. Check for Cross-Map NewWorld(mapId, cellX, cellY)
        Match newWorldMatch = NewWorldRegex().Match(content);
        if (newWorldMatch.Success &&
            ushort.TryParse(newWorldMatch.Groups[1].Value, out ushort targetMapId) &&
            int.TryParse(newWorldMatch.Groups[2].Value, out int targetCellX) &&
            int.TryParse(newWorldMatch.Groups[3].Value, out int targetCellY))
        {
            int targetMpsX = ToMpsX(targetCellX);
            int targetMpsY = ToMpsY(targetCellY);

            ushort minLevel = 1;
            Match levelMatch = GetLevelRegex().Match(content);
            if (levelMatch.Success && ushort.TryParse(levelMatch.Groups[1].Value, out ushort parsedLevel))
            {
                minLevel = parsedLevel;
            }

            byte targetFightMode = 1;
            Match fightMatch = SetFightStateRegex().Match(content);
            if (fightMatch.Success && byte.TryParse(fightMatch.Groups[1].Value, out byte parsedFight))
            {
                targetFightMode = parsedFight;
            }

            int bounceMpsX = 0;
            int bounceMpsY = 0;
            Match setPosMatch = SetPosRegex().Match(content);
            if (setPosMatch.Success &&
                int.TryParse(setPosMatch.Groups[1].Value, out int bounceCellX) &&
                int.TryParse(setPosMatch.Groups[2].Value, out int bounceCellY))
            {
                bounceMpsX = ToMpsX(bounceCellX);
                bounceMpsY = ToMpsY(bounceCellY);
            }

            string? deniedMessage = null;
            Match talkMatch = TalkRegex().Match(content);
            if (talkMatch.Success)
            {
                deniedMessage = talkMatch.Groups[1].Value.Trim();
            }

            binding = new PortalTrapBindingTemplate(
                TrapId: primaryTrapId,
                ScriptPath: scriptPath,
                ActionKind: PortalTrapActionKind.ChangeMapPortal,
                TargetMapId: targetMapId,
                TargetMpsX: targetMpsX,
                TargetMpsY: targetMpsY,
                TargetFightMode: targetFightMode,
                MinRequiredLevel: minLevel,
                BounceBackMpsX: bounceMpsX,
                BounceBackMpsY: bounceMpsY,
                DeniedMessage: deniedMessage);
            return true;
        }

        // 2. Check for Town Gate Fight-State Toggle: two SetPos(...) calls + SetFightState(...)
        MatchCollection setPosMatches = SetPosRegex().Matches(content);
        if (setPosMatches.Count >= 2 &&
            int.TryParse(setPosMatches[0].Groups[1].Value, out int cellX0) &&
            int.TryParse(setPosMatches[0].Groups[2].Value, out int cellY0) &&
            int.TryParse(setPosMatches[1].Groups[1].Value, out int cellX1) &&
            int.TryParse(setPosMatches[1].Groups[2].Value, out int cellY1))
        {
            binding = new PortalTrapBindingTemplate(
                TrapId: primaryTrapId,
                ScriptPath: scriptPath,
                ActionKind: PortalTrapActionKind.ToggleFightStateAndSetPos,
                TargetMapId: 0,
                TargetMpsX: ToMpsX(cellX0),
                TargetMpsY: ToMpsY(cellY0),
                TargetFightMode: 1,
                AlternateMpsX: ToMpsX(cellX1),
                AlternateMpsY: ToMpsY(cellY1),
                AlternateFightMode: 0);
            return true;
        }

        if (setPosMatches.Count == 1 &&
            int.TryParse(setPosMatches[0].Groups[1].Value, out int singleCellX) &&
            int.TryParse(setPosMatches[0].Groups[2].Value, out int singleCellY))
        {
            byte fightMode = 0;
            Match fightMatch = SetFightStateRegex().Match(content);
            if (fightMatch.Success && byte.TryParse(fightMatch.Groups[1].Value, out byte parsedFight))
            {
                fightMode = parsedFight;
            }

            binding = new PortalTrapBindingTemplate(
                TrapId: primaryTrapId,
                ScriptPath: scriptPath,
                ActionKind: PortalTrapActionKind.ToggleFightStateAndSetPos,
                TargetMapId: 0,
                TargetMpsX: ToMpsX(singleCellX),
                TargetMpsY: ToMpsY(singleCellY),
                TargetFightMode: fightMode,
                AlternateMpsX: ToMpsX(singleCellX),
                AlternateMpsY: ToMpsY(singleCellY),
                AlternateFightMode: fightMode);
            return true;
        }

        return false;
    }

    private static int ToMpsX(int coordX) =>
        coordX < 8192 ? coordX * WorldCoordinateConsts.CellWidthMps : coordX;

    private static int ToMpsY(int coordY) =>
        coordY < 16384 ? coordY * WorldCoordinateConsts.CellHeightMps : coordY;

    [GeneratedRegex(@"NewWorld\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NewWorldRegex();

    [GeneratedRegex(@"SetPos\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SetPosRegex();

    [GeneratedRegex(@"SetFightState\s*\(\s*(\d+)\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SetFightStateRegex();

    [GeneratedRegex(@"GetLevel\s*\(\s*\)\s*>=\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GetLevelRegex();

    [GeneratedRegex(@"Talk\s*\(\s*\d+\s*,\s*""[^""]*""\s*,\s*""([^""]+)""\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TalkRegex();
}
