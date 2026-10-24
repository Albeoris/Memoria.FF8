namespace Memoria.FF8.Runtime;

/// <summary>Private frame writes are committed before a service can publish their immutable owners.</summary>
internal sealed class NativeBattleWaterFrameBatch : IDisposable
{
    private NativeBattleEffectArenaTransaction? arena;
    private NativeBattleEffectArenaTransaction? core;
    private bool active;

    public NativeBattleWaterExecutionFrame Begin(NativeBattleWaterExecutionFrame frame)
    {
        if (active)
        {
            throw new InvalidOperationException("A frame batch must publish before starting another private segment.");
        }
        active = true;
        arena = frame.Arena.Transaction is null ? new(frame.Arena) : null;
        core = frame.Core.Core.Transaction is null ? new(frame.Core.Core) : null;
        return frame with { Arena = arena?.View ?? frame.Arena, Core = frame.Core with { Core = core?.View ?? frame.Core.Core } };
    }

    public NativeBattleWaterExecutionFrame Publish(NativeBattleWaterExecutionFrame frame)
    {
        if (!active)
        {
            throw new InvalidOperationException("A frame batch requires its current private segment.");
        }
        if (arena is not null && ReferenceEquals(frame.Arena.Transaction, arena))
        {
            frame = frame with { Arena = arena.Freeze() };
        }
        if (core is not null && ReferenceEquals(frame.Core.Core.Transaction, core))
        {
            frame = frame with { Core = frame.Core with { Core = core.Freeze() } };
        }
        Dispose();
        return frame;
    }

    public void Dispose()
    {
        arena?.Dispose();
        core?.Dispose();
        arena = null;
        core = null;
        active = false;
    }
}
