using Albeoris.Games.Core.Abstractions.NsDisposables;

namespace Albeoris.Games.Core.NsDisposables;

public sealed class DisposableStack : IDisposableStack
{
    private readonly Stack<IDisposable> _stack;

    public DisposableStack()
        : this(capacity: 0)
    {
    }

    public DisposableStack(Int32 capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity, nameof(capacity));

        _stack = new Stack<IDisposable>(capacity);
    }

    public T Add<T>(T item) where T : IDisposable
    {
        _stack.Push(item);
        return item;
    }

    public void Clear() => _stack.Clear();

    public void Dispose()
    {
        while (_stack.Count > 0)
            _stack.Pop().Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        while (_stack.Count > 0)
        {
            IDisposable disposable = _stack.Pop();
            if (disposable is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else
                disposable.Dispose();
        }
    }
}