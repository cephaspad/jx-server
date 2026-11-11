using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Loads the canonical 287-entry <c>MAGIC_ATTRIB</c> registry from <c>settings/MagicDesc.Ini</c>
/// and <c>settings/attribconstdata.ini</c> for 1:1 attribute ID &lt;-&gt; code mapping (`JXData Standard v1`).
/// </summary>
public sealed class MagicAttributeRegistry
{
    private readonly FrozenDictionary<int, string> _codeById;
    private readonly FrozenDictionary<string, int> _idByCode;
    private readonly FrozenDictionary<int, string> _descTemplateById;
    private readonly List<(int AttributeId, string AttributeCode, int ConstValue)> _constRules;

    public IReadOnlyList<(int AttributeId, string AttributeCode, int ConstValue)> ConstRules => _constRules;

    private MagicAttributeRegistry(
        FrozenDictionary<int, string> codeById,
        FrozenDictionary<string, int> idByCode,
        FrozenDictionary<int, string> descTemplateById,
        List<(int, string, int)> constRules)
    {
        _codeById = codeById;
        _idByCode = idByCode;
        _descTemplateById = descTemplateById;
        _constRules = constRules;
    }

    public static MagicAttributeRegistry Load(string serverRootPath)
    {
        var codeById = new Dictionary<int, string>();
        var idByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var descById = new Dictionary<int, string>();

        string? magicDescPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\MagicDesc.Ini");
        if (magicDescPath is not null && File.Exists(magicDescPath))
        {
            byte[] raw = File.ReadAllBytes(magicDescPath);
            int idx = 0;
            int start = 0;
            for (int i = 0; i <= raw.Length; i++)
            {
                if (i == raw.Length || raw[i] == (byte)'\n')
                {
                    int len = i - start;
                    if (len > 0 && raw[start + len - 1] == (byte)'\r')
                    {
                        len--;
                    }

                    ReadOnlySpan<byte> line = raw.AsSpan(start, len);
                    ParseMagicDescLine(line, ref idx, codeById, idByCode, descById);
                    start = i + 1;
                }
            }
        }

        var constRules = new List<(int, string, int)>();
        string? constPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\attribconstdata.ini");
        if (constPath is not null && File.Exists(constPath))
        {
            foreach (string rawLine in File.ReadAllLines(constPath, Encoding.Latin1))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('['))
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line[..eq].Trim();
                string valStr = line[(eq + 1)..].Trim();
                if (int.TryParse(valStr, out int constVal))
                {
                    int attrId = idByCode.GetValueOrDefault(key, 0);
                    constRules.Add((attrId, key.ToLowerInvariant(), constVal));
                }
            }
        }

        return new MagicAttributeRegistry(
            codeById.ToFrozenDictionary(),
            idByCode.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
            descById.ToFrozenDictionary(),
            constRules);
    }

    public string GetCode(int attributeId) =>
        _codeById.TryGetValue(attributeId, out string? code) ? code : $"attr_{attributeId}";

    public int GetId(string attributeCode)
    {
        if (string.IsNullOrWhiteSpace(attributeCode))
        {
            return 0;
        }

        string trimmed = attributeCode.Trim();
        if (_idByCode.TryGetValue(trimmed, out int id))
        {
            return id;
        }

        return int.TryParse(trimmed, out int numeric) ? numeric : 0;
    }

    public string GetNameTemplate(int attributeId, string fallback) =>
        _descTemplateById.TryGetValue(attributeId, out string? tpl) && !string.IsNullOrWhiteSpace(tpl)
            ? tpl
            : fallback;

    public static string GetSkillSlotCategory(int attributeId, string attributeCode)
    {
        if (attributeCode.StartsWith("skill_", StringComparison.OrdinalIgnoreCase))
        {
            return "skillMechanic";
        }

        if (attributeCode.StartsWith("missle_", StringComparison.OrdinalIgnoreCase))
        {
            return "missileMechanic";
        }

        if (attributeId is >= 58 and <= 86 ||
            attributeCode.Contains("damage", StringComparison.OrdinalIgnoreCase) ||
            attributeCode.StartsWith("attackrating", StringComparison.OrdinalIgnoreCase))
        {
            return "damage";
        }

        if (attributeCode.Contains("skill", StringComparison.OrdinalIgnoreCase) &&
            attributeCode.Contains("enhance", StringComparison.OrdinalIgnoreCase))
        {
            return "synergy";
        }

        if (attributeId is 90 or 94 or 98 or 153 or 154)
        {
            return "immediate";
        }

        return "state";
    }

    private static void ParseMagicDescLine(
        ReadOnlySpan<byte> line,
        ref int idx,
        Dictionary<int, string> codeById,
        Dictionary<string, int> idByCode,
        Dictionary<int, string> descById)
    {
        int s = 0;
        while (s < line.Length && (line[s] == (byte)' ' || line[s] == (byte)'\t'))
        {
            s++;
        }

        if (s >= line.Length || line[s] == (byte)'[' || line[s] == (byte)';')
        {
            return;
        }

        if (s + 1 < line.Length && line[s] == (byte)'/' && line[s + 1] == (byte)'/')
        {
            return;
        }

        int eq = line[s..].IndexOf((byte)'=');
        if (eq <= 0)
        {
            return;
        }

        ReadOnlySpan<byte> keyBytes = line.Slice(s, eq);
        ReadOnlySpan<byte> valBytes = line[(s + eq + 1)..];
        string key = Encoding.ASCII.GetString(keyBytes).Trim().ToLowerInvariant();
        string desc = HybridLegacyTextDecoder.DecodeSmart(valBytes, preferVietnamese: true).Trim();

        codeById[idx] = key;
        idByCode.TryAdd(key, idx);
        if (desc.Length > 0)
        {
            descById[idx] = desc;
        }

        idx++;
    }
}
