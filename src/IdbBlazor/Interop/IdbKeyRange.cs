using System.Text.Json.Serialization;

namespace IdbBlazor.Interop;

/// <summary>
/// Represents an IndexedDB key range serialized to JSON for communication with the JS layer.
/// </summary>
public sealed class IdbKeyRange
{
    /// <summary>Equality key (IDBKeyRange.only).</summary>
    [JsonPropertyName("only")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Only { get; set; }

    /// <summary>Lower bound value (IDBKeyRange.lowerBound / bound).</summary>
    [JsonPropertyName("lower")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Lower { get; set; }

    /// <summary>Upper bound value (IDBKeyRange.upperBound / bound).</summary>
    [JsonPropertyName("upper")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Upper { get; set; }

    /// <summary>Whether the lower bound is exclusive.</summary>
    [JsonPropertyName("lowerOpen")]
    public bool LowerOpen { get; set; }

    /// <summary>Whether the upper bound is exclusive.</summary>
    [JsonPropertyName("upperOpen")]
    public bool UpperOpen { get; set; }

    /// <summary>Creates an equality range.</summary>
    public static IdbKeyRange Equality(object value) => new() { Only = value };

    /// <summary>Creates a lower-bound range.</summary>
    public static IdbKeyRange LowerBound(object value, bool open = false)
        => new() { Lower = value, LowerOpen = open };

    /// <summary>Creates an upper-bound range.</summary>
    public static IdbKeyRange UpperBound(object value, bool open = false)
        => new() { Upper = value, UpperOpen = open };

    /// <summary>Creates a bounded range.</summary>
    public static IdbKeyRange Bound(object lower, object upper, bool lowerOpen = false, bool upperOpen = false)
        => new() { Lower = lower, Upper = upper, LowerOpen = lowerOpen, UpperOpen = upperOpen };
}
