namespace Memoria.FF8.Runtime;

/// <summary>Groups ordered local stores between existing committed working-owner boundaries.</summary>
internal sealed class NativeBattleEffectArenaBatch(NativeBattleEffectArena source) : IDisposable
{
    private NativeBattleEffectArena arena = source;
    private NativeBattleEffectArenaTransaction? owned;
    private bool disposed;

    public byte[] Read(int offset, int count)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return arena.Read(offset, count);
    }

    public void Write(int offset, ReadOnlySpan<byte> bytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (arena.Transaction is null)
        {
            NativeBattleTaskOperation.EnlistRetained(arena.Working);
            owned = new(arena);
            arena = owned.View;
        }
        arena = arena.Write(offset, bytes);
    }

    public NativeBattleEffectArena Publish()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (owned is { } transaction)
        {
            arena = transaction.Freeze();
            transaction.Dispose();
            owned = null;
        }
        return arena;
    }

    public void Replace(NativeBattleEffectArena replacement)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        owned?.Dispose();
        owned = null;
        arena = replacement;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            owned?.Dispose();
            owned = null;
        }
    }
}
