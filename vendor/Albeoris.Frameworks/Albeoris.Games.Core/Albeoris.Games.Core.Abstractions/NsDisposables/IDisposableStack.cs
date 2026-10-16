namespace Albeoris.Games.Core.Abstractions.NsDisposables;

public interface IDisposableStack : IDisposable, IAsyncDisposable
{
    T Add<T>(T item) where T : IDisposable;
    void Clear();
}