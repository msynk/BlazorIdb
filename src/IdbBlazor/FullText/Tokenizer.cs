using System.Text.RegularExpressions;

namespace IdbBlazor.FullText;

/// <summary>
/// Tokenizes a string into a set of normalized search tokens for use with
/// multi-entry full-text indexes.  The default strategy:
/// <list type="number">
///   <item>Converts the text to lowercase.</item>
///   <item>Splits on whitespace and punctuation.</item>
///   <item>Removes stop-words and tokens shorter than <see cref="MinTokenLength"/> characters.</item>
///   <item>Deduplicates the resulting tokens.</item>
/// </list>
/// </summary>
public static class Tokenizer
{
    /// <summary>Minimum number of characters required for a token to be indexed.</summary>
    public static int MinTokenLength { get; set; } = 2;

    private static readonly char[] Separators =
        " \t\n\r.,;:!?\"'()[]{}<>/\\|@#$%^&*-_+=`~".ToCharArray();

    /// <summary>
    /// Converts <paramref name="text"/> into a deduplicated array of lowercase tokens.
    /// </summary>
    public static string[] Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

        return text
            .ToLowerInvariant()
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= MinTokenLength)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t)
            .ToArray();
    }

    /// <summary>
    /// Tokenizes <paramref name="text"/> and returns the result as a
    /// <see cref="HashSet{T}"/> for fast membership testing.
    /// </summary>
    public static HashSet<string> TokenizeToSet(string? text)
        => new(Tokenize(text), StringComparer.Ordinal);
}
