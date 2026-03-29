using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.JSInterop;

namespace IdbBlazor.Reactive;

/// <summary>
/// An <see cref="IObservable{T}"/> implementation that polls the IndexedDB store
/// on a configurable interval and emits the current filtered result set whenever
/// it changes.  Backed by <c>IdbBlazor.subscribeToStore</c> in JS.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class LiveQueryObservable<T> : IObservable<IEnumerable<T>>
    where T : class
{
    private readonly IndexedDbSet<T> _set;
    private readonly Expression<Func<T, bool>>? _predicate;
    private readonly int _intervalMs;

    internal LiveQueryObservable(
        IndexedDbSet<T> set,
        Expression<Func<T, bool>>? predicate = null,
        int intervalMs = 500)
    {
        _set = set;
        _predicate = predicate;
        _intervalMs = intervalMs;
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<IEnumerable<T>> observer)
    {
        var subscription = new LiveQuerySubscription<T>(_set, _predicate, observer, _intervalMs);
        _ = subscription.StartAsync();
        return subscription;
    }

    /// <summary>
    /// Convenience overload that accepts a plain <see cref="Action{T}"/> callback,
    /// avoiding a dependency on System.Reactive. Errors are silently swallowed;
    /// use <see cref="Subscribe(IObserver{IEnumerable{T}})"/> for full observer semantics.
    /// </summary>
    public IDisposable Subscribe(Action<IEnumerable<T>> onNext)
        => Subscribe(new DelegateObserver<IEnumerable<T>>(onNext));
}

/// <summary>Internal subscription handle that drives the polling loop.</summary>
internal sealed class LiveQuerySubscription<T> :
    IDisposable,
    IAsyncDisposable
    where T : class
{
    private readonly IndexedDbSet<T> _set;
    private readonly Func<T, bool>? _compiledPredicate;
    private readonly IObserver<IEnumerable<T>> _observer;
    private readonly int _intervalMs;
    private readonly CancellationTokenSource _cts = new();
    private readonly DotNetObjectReference<LiveQueryCallback<T>> _dotnetRef;
    private int _subId = -1;
    private bool _disposed;

    internal LiveQuerySubscription(
        IndexedDbSet<T> set,
        Expression<Func<T, bool>>? predicate,
        IObserver<IEnumerable<T>> observer,
        int intervalMs)
    {
        _set = set;
        _compiledPredicate = predicate?.Compile();
        _observer = observer;
        _intervalMs = intervalMs;
        _dotnetRef = DotNetObjectReference.Create(new LiveQueryCallback<T>(this));
    }

    internal async Task StartAsync()
    {
        try
        {
            await _set._context.EnsureInitializedAsync();
            var store = _set.GetStore();
            _subId = await _set._context.JsInterop.SubscribeToStoreAsync(
                _set._context.DbName,
                store.Name,
                // DotNetObjectReference<object> cast — JS only sees the object
                (DotNetObjectReference<object>)(object)_dotnetRef,
                nameof(LiveQueryCallback<T>.OnStoreChanged),
                _intervalMs);
        }
        catch (Exception ex)
        {
            _observer.OnError(ex);
        }
    }

    internal void OnStoreChanged(string json)
    {
        if (_disposed) return;
        try
        {
            var all = JsonSerializer.Deserialize<List<T>>(json, IndexedDbContext.JsonOptions)
                      ?? new List<T>();
            IEnumerable<T> result = _compiledPredicate is not null
                ? all.Where(_compiledPredicate)
                : all;
            _observer.OnNext(result);
        }
        catch (Exception ex)
        {
            _observer.OnError(ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ = DisposeAsync().AsTask();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        if (_subId >= 0)
            await _set._context.JsInterop.UnsubscribeAsync(_subId);
        _dotnetRef.Dispose();
        _cts.Dispose();
    }
}

/// <summary>
/// Thin class that holds a JS-invokable callback method so that the JS polling
/// loop can call back into .NET.
/// </summary>
internal sealed class LiveQueryCallback<T> where T : class
{
    private readonly LiveQuerySubscription<T> _subscription;

    internal LiveQueryCallback(LiveQuerySubscription<T> subscription)
        => _subscription = subscription;

    /// <summary>Called from JS when the store contents change.</summary>
    [JSInvokable]
    public void OnStoreChanged(string json) => _subscription.OnStoreChanged(json);
}

/// <summary>
/// Minimal <see cref="IObserver{T}"/> that delegates <c>OnNext</c> to an action
/// and silently ignores errors and completion.
/// </summary>
internal sealed class DelegateObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    internal DelegateObserver(Action<T> onNext) => _onNext = onNext;
    public void OnNext(T value) => _onNext(value);
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
