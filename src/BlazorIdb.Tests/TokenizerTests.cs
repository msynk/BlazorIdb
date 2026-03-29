using BlazorIdb.FullText;
using Xunit;

namespace BlazorIdb.Tests;

/// <summary>Unit tests for <see cref="Tokenizer"/>.</summary>
public sealed class TokenizerTests
{
    [Fact]
    public void Tokenize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(Tokenizer.Tokenize(null));
        Assert.Empty(Tokenizer.Tokenize(string.Empty));
        Assert.Empty(Tokenizer.Tokenize("   "));
    }

    [Fact]
    public void Tokenize_LowercasesTokens()
    {
        var tokens = Tokenizer.Tokenize("Hello World");
        Assert.Contains("hello", tokens);
        Assert.Contains("world", tokens);
    }

    [Fact]
    public void Tokenize_SplitsOnPunctuation()
    {
        var tokens = Tokenizer.Tokenize("fast, reliable; and scalable.");
        Assert.Contains("fast", tokens);
        Assert.Contains("reliable", tokens);
        Assert.Contains("and", tokens);
        Assert.Contains("scalable", tokens);
    }

    [Fact]
    public void Tokenize_Deduplicates()
    {
        var tokens = Tokenizer.Tokenize("the cat sat on the mat");
        Assert.Equal(tokens.Length, tokens.Distinct().Count());
    }

    [Fact]
    public void Tokenize_FiltersShortTokens()
    {
        var tokens = Tokenizer.Tokenize("a I an it the");
        // Default MinTokenLength = 2, so "a", "I" are excluded; "an", "it" included
        Assert.DoesNotContain("a", tokens);
        Assert.Contains("an", tokens);
        Assert.Contains("it", tokens);
    }

    [Fact]
    public void TokenizeToSet_ReturnsCaseSensitiveHashSet()
    {
        var set = Tokenizer.TokenizeToSet("hello world hello");
        Assert.Contains("hello", set);
        Assert.Contains("world", set);
        Assert.Equal(2, set.Count);
    }
}
