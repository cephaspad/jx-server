using System;
using System.Collections.Generic;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Shared cross-domain symbol table populated during <c>JXServer.DataExporter</c> execution so every
/// <c>*Code</c> foreign key resolves deterministically and Layer-2 row counts are recorded for parity verification.
/// </summary>
public sealed class ExportCatalogRegistry
{
    public HashSet<string> ItemCodes { get; } = new(StringComparer.Ordinal);

    public Dictionary<(int Genre, int DetailType, int ParticularType, int Level), string> ItemCodeByTupleAndLevel { get; } = [];

    public Dictionary<(int Genre, int DetailType, int ParticularType), string> PrimaryItemCodeByTuple { get; } = [];

    public Dictionary<int, string> GoldenCodeByRowId { get; } = [];

    public Dictionary<int, string> PlatinaCodeByRowId { get; } = [];

    public HashSet<string> AffixCodes { get; } = new(StringComparer.Ordinal);

    public Dictionary<int, (string Code, int AttrId, string AttrCode, int P0Min, int P0Max, int P1Min, int P1Max, int P2Min, int P2Max)> GoldenAffixByRowId { get; } = [];

    public Dictionary<int, string> ObjCodeById { get; } = [];

    public Dictionary<int, string> SkillCodeById { get; } = [];

    public Dictionary<string, int> SkillIdByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<int, string> MissileCodeById { get; } = [];

    public HashSet<string> LootTableCodes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> LootTableCodeByLegacyPath { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<int, string> NpcCodeById { get; } = [];

    public Dictionary<int, (string Name, string EntityKind, int DefaultCamp, int DefaultSeries, int RespawnFrames, string? LootTableCode)> NpcMetaById { get; } = [];

    public Dictionary<int, string> MapCodeById { get; } = [];

    public Dictionary<int, string> MapNameById { get; } = [];

    public HashSet<int> ActiveMapIds { get; } = [];

    public HashSet<string> ShopCodes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> LegacyRecordCounts { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> ExportedRecordCounts { get; } = new(StringComparer.Ordinal);

    public void RecordCount(string domainKey, int legacyCount, int exportedCount)
    {
        LegacyRecordCounts[domainKey] = legacyCount;
        ExportedRecordCounts[domainKey] = exportedCount;
    }

    public string? TryResolveLootCode(string? rawIniPath)
    {
        if (string.IsNullOrWhiteSpace(rawIniPath))
        {
            return null;
        }

        string norm = NormalizeLegacyRelPath(rawIniPath);
        if (LootTableCodeByLegacyPath.TryGetValue(norm, out string? code))
        {
            return code;
        }

        return null;
    }

    public static string NormalizeLegacyRelPath(string rawPath)
    {
        string trimmed = rawPath.Trim().Replace('/', '\\');
        if (!trimmed.StartsWith('\\'))
        {
            trimmed = "\\" + trimmed;
        }

        return trimmed.ToLowerInvariant();
    }
}
