using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lambda.Core.Main;


public class KeyedDebouncer<TKey> : IDisposable
{
    private readonly Dictionary<TKey, CancellationTokenSource> _activeTasks = new();
    private readonly object _lock = new();

    public void Debounce(TKey key, TimeSpan delay, Action action)
    {
        CancellationTokenSource newCts = new CancellationTokenSource();
        CancellationTokenSource oldCts = null;

        lock (_lock)
        {
            if (_activeTasks.TryGetValue(key, out oldCts))
            {
                oldCts.Cancel();
            }

            _activeTasks[key] = newCts;
        }

        oldCts?.Dispose();

        ExecuteAsync(key, delay, action, newCts).Forget();
    }

    private async UniTaskVoid ExecuteAsync(TKey key, TimeSpan delay, Action action, CancellationTokenSource cts)
    {
        try
        {
            await UniTask.Delay(delay, cancellationToken: cts.Token);

            action?.Invoke();
        }
        catch (OperationCanceledException) { }
        finally
        {
            lock (_lock)
            {
                if (_activeTasks.TryGetValue(key, out var currentCts) && currentCts == cts)
                {
                    _activeTasks.Remove(key);
                    cts.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var cts in _activeTasks.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _activeTasks.Clear();
        }
    }
}