using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JXServer.Infrastructure.Characters;

/// <summary>
/// Deterministic JSON serializer and EF Core ValueComparer for sparse Character.TaskValues (<c>ushort -> int</c>).
/// </summary>
public static class TaskValuesJsonSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public static readonly ValueComparer<Dictionary<ushort, int>> Comparer = new(
        (c1, c2) => AreEqual(c1, c2),
        c => ComputeHashCode(c),
        c => c == null ? new Dictionary<ushort, int>() : new Dictionary<ushort, int>(c));

    public static string Serialize(IReadOnlyDictionary<ushort, int>? dictionary)
    {
        if (dictionary is null || dictionary.Count == 0)
        {
            return "{}";
        }

        // Sort keys ascending and filter out 0 values for deterministic sparse serialization
        var sorted = new SortedDictionary<ushort, int>();
        foreach ((ushort key, int value) in dictionary)
        {
            if (value != 0)
            {
                sorted[key] = value;
            }
        }

        if (sorted.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(sorted, JsonOptions);
    }

    public static Dictionary<ushort, int> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
        {
            return [];
        }

        try
        {
            Dictionary<ushort, int>? deserialized = JsonSerializer.Deserialize<Dictionary<ushort, int>>(json, JsonOptions);
            if (deserialized is null || deserialized.Count == 0)
            {
                return [];
            }

            var result = new Dictionary<ushort, int>(deserialized.Count);
            foreach ((ushort key, int value) in deserialized)
            {
                if (value != 0)
                {
                    result[key] = value;
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool AreEqual(Dictionary<ushort, int>? left, Dictionary<ushort, int>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach ((ushort key, int val) in left)
        {
            if (!right.TryGetValue(key, out int rightVal) || val != rightVal)
            {
                return false;
            }
        }

        return true;
    }

    private static int ComputeHashCode(Dictionary<ushort, int>? dict)
    {
        if (dict is null)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach ((ushort key, int val) in dict.OrderBy(k => k.Key))
        {
            hash.Add(key);
            hash.Add(val);
        }

        return hash.ToHashCode();
    }
}
