namespace Memoria.FF8.Runtime;

/// <summary>Keeps executable fixups private until their caller finishes initializing the task context.</summary>
internal sealed class NativeBattleNodeInitialization<T>(T state, NativeBattleNodeBankOperation? operation) : IDisposable
{
    private bool disposed;

    internal T Publish()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        operation?.Publish(false);
        return state;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            operation?.Dispose();
            disposed = true;
        }
    }
}
