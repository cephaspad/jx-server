using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace JXServer.DataExporter.Compilation;

/// <summary>
/// Validates generated <c>data/**/*.json</c> files against the structural contracts in <c>schemas/*.schema.json</c>.
/// </summary>
public sealed class JsonSchemaAssetValidator
{
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

        var requiredByFolder = new Dictionary<string, (string SchemaFile, string[] RequiredProps)>(StringComparer.OrdinalIgnoreCase)
        {
            ["maps"] = ("map.schema.json", ["schemaVersion", "id", "code", "name", "regionBounds", "regions", "portals", "spawnGroups"]),
            ["npcs"] = ("npc-catalog.schema.json", ["schemaVersion", "npcs"]),
            ["players"] = ("player-template.schema.json", ["schemaVersion", "characterTemplates", "seriesGrowthRules", "fiveElementsMatrix", "levelExpTable", "starterVillages"]),
            ["skills"] = ("skill-catalog.schema.json", ["schemaVersion", "skills", "missiles"]),
            ["items"] = ("item-catalog.schema.json", ["schemaVersion", "items", "magicAffixes"]),
            ["loot"] = ("loot-table.schema.json", ["schemaVersion", "lootTables"]),
        };

        foreach (var (folder, (schemaFile, requiredProps)) in requiredByFolder)
        {
            string schemaPath = Path.Combine(schemasDirectory, schemaFile);
            if (!File.Exists(schemaPath))
            {
                errors.Add($"Missing schema file '{schemaPath}'.");
                continue;
            }

            string subDir = Path.Combine(dataDirectory, folder);
            if (!Directory.Exists(subDir))
            {
                errors.Add($"Missing data subfolder '{subDir}'.");
                continue;
            }

            string[] jsonFiles = Directory.GetFiles(subDir, "*.json", SearchOption.AllDirectories);
            if (jsonFiles.Length == 0)
            {
                errors.Add($"No JSON files found in '{subDir}'.");
                continue;
            }

            foreach (string jsonFile in jsonFiles)
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(jsonFile));
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"File '{jsonFile}' root is not a JSON object.");
                    continue;
                }

                if (!root.TryGetProperty("schemaVersion", out JsonElement ver) || ver.GetString() != "1.0.0")
                {
                    errors.Add($"File '{jsonFile}' is missing schemaVersion == '1.0.0'.");
                }

                foreach (string req in requiredProps)
                {
                    if (!root.TryGetProperty(req, out _))
                    {
                        errors.Add($"File '{jsonFile}' is missing required property '{req}' per '{schemaFile}'.");
                    }
                }
            }
        }

        return errors;
    }
}
