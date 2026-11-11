using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JXServer.DataExporter.Compilation;

/// <summary>
/// Full <c>JXData Standard v1</c> JSON Schema (Draft 2020-12 subset), literal unescaped UTF-8,
/// synthetic-ID prohibition, and 100% cross-catalog <c>*Code</c> foreign-key validator.
/// </summary>
public sealed class JsonSchemaAssetValidator
{
    private readonly Dictionary<string, Regex> _regexCache = new(StringComparer.Ordinal);

    public IReadOnlyList<string> ValidateAll(string dataDirectory, string schemasDirectory)
    {
        var errors = new List<string>();
        if (!Directory.Exists(dataDirectory))
        {
            errors.Add($"Data directory '{dataDirectory}' does not exist.");
            return errors;
        }

        if (!Directory.Exists(schemasDirectory))
        {
            errors.Add($"Schemas directory '{schemasDirectory}' does not exist.");
            return errors;
        }

        var folderToSchema = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["maps"] = "map.schema.json",
            ["npcs"] = "npc-catalog.schema.json",
            ["players"] = "player-template.schema.json",
            ["skills"] = "skill-catalog.schema.json",
            ["items"] = "item-catalog.schema.json",
            ["loot"] = "loot-table.schema.json",
            ["shops"] = "shop-catalog.schema.json",
        };

        var schemaDocs = new Dictionary<string, JsonDocument>(StringComparer.OrdinalIgnoreCase);
        var schemaRootByKey = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (string schemaFile in folderToSchema.Values.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string schemaPath = Path.Combine(schemasDirectory, schemaFile);
                if (!File.Exists(schemaPath))
                {
                    errors.Add($"Missing schema file '{schemaPath}'.");
                    continue;
                }

                JsonDocument sDoc = JsonDocument.Parse(File.ReadAllBytes(schemaPath));
                schemaDocs[schemaFile] = sDoc;
                schemaRootByKey[schemaFile] = sDoc.RootElement;
                if (sDoc.RootElement.TryGetProperty("$id", out JsonElement idProp) && idProp.ValueKind == JsonValueKind.String)
                {
                    string? schemaId = idProp.GetString();
                    if (!string.IsNullOrEmpty(schemaId))
                    {
                        schemaRootByKey[schemaId] = sDoc.RootElement;
                    }
                }
            }

            if (errors.Count > 0)
            {
                return errors;
            }

            var itemCodes = new HashSet<string>(StringComparer.Ordinal);
            var affixCodes = new HashSet<string>(StringComparer.Ordinal);
            var objCodes = new HashSet<string>(StringComparer.Ordinal);
            var skillCodes = new HashSet<string>(StringComparer.Ordinal);
            var missileCodes = new HashSet<string>(StringComparer.Ordinal);
            var lootCodes = new HashSet<string>(StringComparer.Ordinal);
            var npcCodes = new HashSet<string>(StringComparer.Ordinal);
            var mapCodes = new HashSet<string>(StringComparer.Ordinal);
            var shopCodes = new HashSet<string>(StringComparer.Ordinal);

            var allParsedFiles = new List<(string Folder, string FilePath, byte[] RawBytes)>();

            foreach (var (folder, schemaFile) in folderToSchema)
            {
                string subDir = Path.Combine(dataDirectory, folder);
                if (!Directory.Exists(subDir))
                {
                    errors.Add($"Missing data subfolder '{subDir}'.");
                    continue;
                }

                string[] jsonFiles = Directory.GetFiles(subDir, "*.json", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (jsonFiles.Length == 0)
                {
                    errors.Add($"No JSON files found in '{subDir}'.");
                    continue;
                }

                JsonElement rootSchema = schemaRootByKey[schemaFile];
                foreach (string jsonFile in jsonFiles)
                {
                    byte[] rawBytes = File.ReadAllBytes(jsonFile);
                    CheckLiteralUtf8NoAsciiEscapes(jsonFile, rawBytes, errors);

                    using JsonDocument doc = JsonDocument.Parse(rawBytes);
                    JsonElement root = doc.RootElement;

                    ValidateElement(root, rootSchema, rootSchema, schemaRootByKey, jsonFile, "$", errors);
                    CollectDefinitionsAndCheckZeroSyntheticIds(folder, jsonFile, root, itemCodes, affixCodes, objCodes, skillCodes, missileCodes, lootCodes, npcCodes, mapCodes, shopCodes, errors);
                    allParsedFiles.Add((folder, jsonFile, rawBytes));
                }
            }

            // Second pass: 100% cross-catalog *Code Foreign Key validation
            foreach (var (_, filePath, rawBytes) in allParsedFiles)
            {
                using JsonDocument doc = JsonDocument.Parse(rawBytes);
                ValidateForeignKeysRecursive(
                    doc.RootElement,
                    filePath,
                    "$",
                    itemCodes,
                    affixCodes,
                    objCodes,
                    skillCodes,
                    lootCodes,
                    npcCodes,
                    mapCodes,
                    errors);
            }
        }
        finally
        {
            foreach (JsonDocument d in schemaDocs.Values)
            {
                d.Dispose();
            }
        }

        return errors;
    }

    private static void CheckLiteralUtf8NoAsciiEscapes(string filePath, ReadOnlySpan<byte> rawBytes, List<string> errors)
    {
        for (int i = 0; i + 5 < rawBytes.Length; i++)
        {
            if (rawBytes[i] == (byte)'\\' && rawBytes[i + 1] == (byte)'u')
            {
                // Check if \uXXXX is a non-control character >= 0x0080 (escaped Vietnamese/CJK Unicode is forbidden!)
                string hex = Encoding.ASCII.GetString(rawBytes.Slice(i + 2, 4));
                if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort codeUnit) &&
                    codeUnit >= 0x0080)
                {
                    errors.Add($"{filePath}: Contains escaped non-ASCII sequence '\\u{hex}' instead of literal UTF-8.");
                    return;
                }
            }
        }
    }

    private static void CollectDefinitionsAndCheckZeroSyntheticIds(
        string folder,
        string filePath,
        JsonElement root,
        HashSet<string> itemCodes,
        HashSet<string> affixCodes,
        HashSet<string> objCodes,
        HashSet<string> skillCodes,
        HashSet<string> missileCodes,
        HashSet<string> lootCodes,
        HashSet<string> npcCodes,
        HashSet<string> mapCodes,
        HashSet<string> shopCodes,
        List<string> errors)
    {
        if (string.Equals(folder, "maps", StringComparison.OrdinalIgnoreCase))
        {
            if (root.TryGetProperty("code", out JsonElement c) && c.ValueKind == JsonValueKind.String && c.GetString() is { Length: > 0 } mc)
            {
                if (!mapCodes.Add(mc))
                {
                    errors.Add($"{filePath}: Duplicate map code '{mc}'.");
                }
            }

            string fileName = Path.GetFileName(filePath);
            if (!Regex.IsMatch(fileName, @"^\d{3}_[a-z0-9]+(-[a-z0-9]+)*\.map\.json$"))
            {
                errors.Add($"{filePath}: Map filename '{fileName}' does not match '<id:D3>_<code>.map.json'.");
            }
        }

        CollectCodesFromArray(root, "items", itemCodes, filePath, forbidIdProperty: true, errors);
        CollectCodesFromArray(root, "magicAffixes", affixCodes, filePath, forbidIdProperty: true, errors);
        CollectCodesFromArray(root, "groundObjects", objCodes, filePath, forbidIdProperty: false, errors);
        CollectCodesFromArray(root, "skills", skillCodes, filePath, forbidIdProperty: false, errors);
        CollectCodesFromArray(root, "missiles", missileCodes, filePath, forbidIdProperty: false, errors);
        CollectCodesFromArray(root, "lootTables", lootCodes, filePath, forbidIdProperty: true, errors);
        CollectCodesFromArray(root, "npcs", npcCodes, filePath, forbidIdProperty: false, errors);
        CollectCodesFromArray(root, "shops", shopCodes, filePath, forbidIdProperty: false, errors);
    }

    private static void CollectCodesFromArray(
        JsonElement root,
        string arrayName,
        HashSet<string> targetSet,
        string filePath,
        bool forbidIdProperty,
        List<string> errors)
    {
        if (!root.TryGetProperty(arrayName, out JsonElement arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int idx = 0;
        foreach (JsonElement elem in arr.EnumerateArray())
        {
            if (elem.ValueKind == JsonValueKind.Object)
            {
                if (forbidIdProperty && elem.TryGetProperty("id", out _))
                {
                    errors.Add($"{filePath} $.{arrayName}[{idx}]: Forbidden synthetic 'id' property present.");
                }

                if (elem.TryGetProperty("code", out JsonElement cp) && cp.ValueKind == JsonValueKind.String && cp.GetString() is { Length: > 0 } code)
                {
                    if (!targetSet.Add(code))
                    {
                        errors.Add($"{filePath} $.{arrayName}[{idx}]: Duplicate canonical code '{code}'.");
                    }
                }
            }

            idx++;
        }
    }

    private static void ValidateForeignKeysRecursive(
        JsonElement element,
        string filePath,
        string jsonPath,
        HashSet<string> itemCodes,
        HashSet<string> affixCodes,
        HashSet<string> objCodes,
        HashSet<string> skillCodes,
        HashSet<string> lootCodes,
        HashSet<string> npcCodes,
        HashSet<string> mapCodes,
        List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                string childPath = $"{jsonPath}.{prop.Name}";
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    string val = prop.Value.GetString() ?? string.Empty;
                    if (val.Length > 0)
                    {
                        CheckSingleForeignKey(prop.Name, val, filePath, childPath, itemCodes, affixCodes, objCodes, skillCodes, lootCodes, npcCodes, mapCodes, errors);
                    }
                }
                else if (prop.Name == "forbiddenItemCodes" && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (JsonElement itemElem in prop.Value.EnumerateArray())
                    {
                        if (itemElem.ValueKind == JsonValueKind.String && itemElem.GetString() is { Length: > 0 } ic && !itemCodes.Contains(ic))
                        {
                            errors.Add($"{filePath} {childPath}[{i}]: Unresolved itemCode '{ic}'.");
                        }

                        i++;
                    }
                }
                else
                {
                    ValidateForeignKeysRecursive(prop.Value, filePath, childPath, itemCodes, affixCodes, objCodes, skillCodes, lootCodes, npcCodes, mapCodes, errors);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int idx = 0;
            foreach (JsonElement child in element.EnumerateArray())
            {
                ValidateForeignKeysRecursive(child, filePath, $"{jsonPath}[{idx}]", itemCodes, affixCodes, objCodes, skillCodes, lootCodes, npcCodes, mapCodes, errors);
                idx++;
            }
        }
    }

    private static void CheckSingleForeignKey(
        string propName,
        string value,
        string filePath,
        string jsonPath,
        HashSet<string> itemCodes,
        HashSet<string> affixCodes,
        HashSet<string> objCodes,
        HashSet<string> skillCodes,
        HashSet<string> lootCodes,
        HashSet<string> npcCodes,
        HashSet<string> mapCodes,
        List<string> errors)
    {
        switch (propName)
        {
            case "itemCode":
            case "atlasItemCode":
                if (!itemCodes.Contains(value))
                {
                    errors.Add($"{filePath} {jsonPath}: Unresolved {propName} '{value}'.");
                }

                break;

            case "affixCode":
                if (!affixCodes.Contains(value))
                {
                    errors.Add($"{filePath} {jsonPath}: Unresolved affixCode '{value}'.");
                }

                break;

            case "objCode":
                if (!objCodes.Contains(value))
                {
                    errors.Add($"{filePath} {jsonPath}: Unresolved objCode '{value}'.");
                }

                break;

            case "skillCode":
                if (!skillCodes.Contains(value))
                {
                    errors.Add($"{filePath} {jsonPath}: Unresolved skillCode '{value}'.");
                }

                break;

            case "lootTableCode":
            case "normalLootTableCode":
            case "goldenLootTableCode":
                if (!lootCodes.Contains(value))
                {
                    errors.Add($"{filePath} {jsonPath}: Unresolved {propName} '{value}'.");
                }

                break;

            case "npcCode":
                if (!npcCodes.Contains(value))
                {
                    errors.Add($"{filePath} {jsonPath}: Unresolved npcCode '{value}'.");
                }

                break;

            case "mapCode":
                if (!mapCodes.Contains(value))
                {
                    errors.Add($"{filePath} {jsonPath}: Unresolved mapCode '{value}'.");
                }

                break;
        }
    }

    private void ValidateElement(
        JsonElement instance,
        JsonElement schema,
        JsonElement currentSchemaRoot,
        Dictionary<string, JsonElement> schemaRootByKey,
        string filePath,
        string jsonPath,
        List<string> errors)
    {
        if (errors.Count >= 200)
        {
            return;
        }

        if (schema.TryGetProperty("$ref", out JsonElement refProp) && refProp.ValueKind == JsonValueKind.String)
        {
            string refStr = refProp.GetString()!;
            if (TryResolveRef(refStr, currentSchemaRoot, schemaRootByKey, out JsonElement targetSchema, out JsonElement targetRoot))
            {
                ValidateElement(instance, targetSchema, targetRoot, schemaRootByKey, filePath, jsonPath, errors);
            }
            else
            {
                errors.Add($"{filePath} {jsonPath}: Unable to resolve schema $ref '{refStr}'.");
            }

            return;
        }

        if (schema.TryGetProperty("type", out JsonElement typeProp) && typeProp.ValueKind == JsonValueKind.String)
        {
            string expectedType = typeProp.GetString()!;
            if (!MatchesJsonType(instance, expectedType))
            {
                errors.Add($"{filePath} {jsonPath}: Expected JSON type '{expectedType}', got '{instance.ValueKind}'.");
                return;
            }
        }

        if (schema.TryGetProperty("const", out JsonElement constProp))
        {
            if (!JsonElementsEqual(instance, constProp))
            {
                errors.Add($"{filePath} {jsonPath}: Value '{instance}' does not match const '{constProp}'.");
            }
        }

        if (schema.TryGetProperty("enum", out JsonElement enumProp) && enumProp.ValueKind == JsonValueKind.Array)
        {
            bool matched = false;
            foreach (JsonElement opt in enumProp.EnumerateArray())
            {
                if (JsonElementsEqual(instance, opt))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                errors.Add($"{filePath} {jsonPath}: Value '{instance}' is not in allowed enum.");
            }
        }

        if (schema.TryGetProperty("anyOf", out JsonElement anyOfProp) && anyOfProp.ValueKind == JsonValueKind.Array)
        {
            bool anyMatched = false;
            foreach (JsonElement subSchema in anyOfProp.EnumerateArray())
            {
                var branchErrors = new List<string>();
                ValidateElement(instance, subSchema, currentSchemaRoot, schemaRootByKey, filePath, jsonPath, branchErrors);
                if (branchErrors.Count == 0)
                {
                    anyMatched = true;
                    break;
                }
            }

            if (!anyMatched)
            {
                errors.Add($"{filePath} {jsonPath}: Instance did not satisfy any 'anyOf' schema constraint.");
            }
        }

        switch (instance.ValueKind)
        {
            case JsonValueKind.String:
            {
                string s = instance.GetString() ?? string.Empty;
                if (schema.TryGetProperty("minLength", out JsonElement minLenProp) && minLenProp.TryGetInt32(out int minLen) && s.Length < minLen)
                {
                    errors.Add($"{filePath} {jsonPath}: String length {s.Length} < minLength {minLen}.");
                }

                if (schema.TryGetProperty("pattern", out JsonElement patProp) && patProp.ValueKind == JsonValueKind.String)
                {
                    string pat = patProp.GetString()!;
                    if (!_regexCache.TryGetValue(pat, out Regex? rx))
                    {
                        rx = new Regex(pat, RegexOptions.Compiled | RegexOptions.CultureInvariant);
                        _regexCache[pat] = rx;
                    }

                    if (!rx.IsMatch(s))
                    {
                        errors.Add($"{filePath} {jsonPath}: String '{s}' does not match pattern '{pat}'.");
                    }
                }

                break;
            }

            case JsonValueKind.Number:
            {
                double val = instance.GetDouble();
                if (schema.TryGetProperty("minimum", out JsonElement minProp) && minProp.TryGetDouble(out double minVal) && val < minVal)
                {
                    errors.Add($"{filePath} {jsonPath}: Number {val} < minimum {minVal}.");
                }

                if (schema.TryGetProperty("maximum", out JsonElement maxProp) && maxProp.TryGetDouble(out double maxVal) && val > maxVal)
                {
                    errors.Add($"{filePath} {jsonPath}: Number {val} > maximum {maxVal}.");
                }

                break;
            }

            case JsonValueKind.Array:
            {
                int len = instance.GetArrayLength();
                if (schema.TryGetProperty("minItems", out JsonElement minItemsProp) && minItemsProp.TryGetInt32(out int minItems) && len < minItems)
                {
                    errors.Add($"{filePath} {jsonPath}: Array length {len} < minItems {minItems}.");
                }

                if (schema.TryGetProperty("maxItems", out JsonElement maxItemsProp) && maxItemsProp.TryGetInt32(out int maxItems) && len > maxItems)
                {
                    errors.Add($"{filePath} {jsonPath}: Array length {len} > maxItems {maxItems}.");
                }

                if (schema.TryGetProperty("items", out JsonElement itemSchema))
                {
                    int idx = 0;
                    foreach (JsonElement child in instance.EnumerateArray())
                    {
                        ValidateElement(child, itemSchema, currentSchemaRoot, schemaRootByKey, filePath, $"{jsonPath}[{idx}]", errors);
                        idx++;
                    }
                }

                break;
            }

            case JsonValueKind.Object:
            {
                if (schema.TryGetProperty("required", out JsonElement reqProp) && reqProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement r in reqProp.EnumerateArray())
                    {
                        string reqName = r.GetString()!;
                        if (!instance.TryGetProperty(reqName, out _))
                        {
                            errors.Add($"{filePath} {jsonPath}: Missing required property '{reqName}'.");
                        }
                    }
                }

                bool hasProps = schema.TryGetProperty("properties", out JsonElement propsSchema) && propsSchema.ValueKind == JsonValueKind.Object;
                bool forbidAdditional = schema.TryGetProperty("additionalProperties", out JsonElement addProp) && addProp.ValueKind == JsonValueKind.False;

                foreach (JsonProperty prop in instance.EnumerateObject())
                {
                    if (hasProps && propsSchema.TryGetProperty(prop.Name, out JsonElement propSubSchema))
                    {
                        ValidateElement(prop.Value, propSubSchema, currentSchemaRoot, schemaRootByKey, filePath, $"{jsonPath}.{prop.Name}", errors);
                    }
                    else if (forbidAdditional)
                    {
                        errors.Add($"{filePath} {jsonPath}: Additional property '{prop.Name}' is not allowed.");
                    }
                }

                break;
            }
        }
    }

    private static bool TryResolveRef(
        string refStr,
        JsonElement currentRoot,
        Dictionary<string, JsonElement> schemaRootByKey,
        out JsonElement resolved,
        out JsonElement resolvedRoot)
    {
        resolved = default;
        resolvedRoot = currentRoot;

        string fragment;
        int hashIdx = refStr.IndexOf('#');
        if (hashIdx > 0)
        {
            string docPart = refStr[..hashIdx];
            fragment = refStr[(hashIdx + 1)..];
            if (!schemaRootByKey.TryGetValue(docPart, out resolvedRoot))
            {
                string fileName = Path.GetFileName(docPart);
                if (!schemaRootByKey.TryGetValue(fileName, out resolvedRoot))
                {
                    return false;
                }
            }
        }
        else if (hashIdx == 0)
        {
            fragment = refStr[1..];
        }
        else
        {
            return schemaRootByKey.TryGetValue(refStr, out resolved);
        }

        JsonElement cursor = resolvedRoot;
        foreach (string seg in fragment.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (cursor.ValueKind != JsonValueKind.Object || !cursor.TryGetProperty(seg, out cursor))
            {
                return false;
            }
        }

        resolved = cursor;
        return true;
    }

    private static bool MatchesJsonType(JsonElement instance, string expectedType) =>
        expectedType switch
        {
            "object" => instance.ValueKind == JsonValueKind.Object,
            "array" => instance.ValueKind == JsonValueKind.Array,
            "string" => instance.ValueKind == JsonValueKind.String,
            "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "number" => instance.ValueKind == JsonValueKind.Number,
            "integer" => instance.ValueKind == JsonValueKind.Number && IsIntegralJsonNumber(instance),
            _ => true,
        };

    private static bool IsIntegralJsonNumber(JsonElement num)
    {
        if (num.TryGetInt64(out _))
        {
            return true;
        }

        double d = num.GetDouble();
        return Math.Abs(d - Math.Truncate(d)) < 1e-9;
    }

    private static bool JsonElementsEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.String => string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => Math.Abs(a.GetDouble() - b.GetDouble()) < 1e-9,
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            JsonValueKind.Null => true,
            _ => string.Equals(a.GetRawText(), b.GetRawText(), StringComparison.Ordinal),
        };
    }
}
