using Microsoft.JSInterop;

namespace IdbBlazor.Tests;

/// <summary>
/// A test-double for <see cref="IJSRuntime"/> that records all JS invocations and
/// allows tests to configure canned return values.
/// </summary>
public sealed class MockJsRuntime : IJSRuntime
{
    // ---- Recorded calls ----

    /// <summary>All calls made to InvokeAsync, in order.</summary>
    public List<JsCall> Calls { get; } = new();

    // ---- Configured responses ----

    private readonly Dictionary<string, Queue<object?>> _responses = new(StringComparer.Ordinal);

    /// <summary>
    /// Queues a return value for the next call to the given JS function.
    /// Multiple values can be queued and will be returned in order.
    /// </summary>
    public MockJsRuntime Returns(string identifier, object? value)
    {
        if (!_responses.TryGetValue(identifier, out var q))
        {
            q = new Queue<object?>();
            _responses[identifier] = q;
        }
        q.Enqueue(value);
        return this;
    }

    // ---- IJSRuntime ----

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Calls.Add(new JsCall(identifier, args));
        if (_responses.TryGetValue(identifier, out var q) && q.Count > 0)
        {
            var raw = q.Dequeue();
            if (raw is TValue typed) return ValueTask.FromResult(typed);
            if (raw is null) return ValueTask.FromResult(default(TValue)!);
            // Try JSON round-trip for complex types
            var json = System.Text.Json.JsonSerializer.Serialize(raw);
            var result = System.Text.Json.JsonSerializer.Deserialize<TValue>(json)!;
            return ValueTask.FromResult(result);
        }
        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}

/// <summary>Records a single JavaScript interop call.</summary>
public sealed record JsCall(string Identifier, object?[]? Args);
