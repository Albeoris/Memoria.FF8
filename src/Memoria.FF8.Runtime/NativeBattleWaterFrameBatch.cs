using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime;

/// <summary>Private frame segments commit before a service receives their current owners.</summary>
internal sealed class NativeBattleWaterFrameBatch : IDisposable
{
    private NativeBattleEffectArenaTransaction? arena;
    private NativeBattleEffectArenaTransaction? core;
    private NativeBattleWaterTemporaryFrame.PrivateWrite? temporary;
    private bool active;

    public NativeBattleWaterExecutionFrame Begin(NativeBattleWaterExecutionFrame frame)
    {
        if (active)
        {
            throw new InvalidOperationException("A frame batch must publish before starting another private segment.");
        }
        active = true;
        temporary = frame.TemporaryFrame.BeginPrivateWrite();
        arena = frame.Arena.Transaction is null ? new(frame.Arena) : null;
        core = frame.Core.Core.Transaction is null ? new(frame.Core.Core) : null;
        return frame with { Arena = arena?.View ?? frame.Arena, Core = frame.Core with { Core = core?.View ?? frame.Core.Core }, TemporaryFrame = temporary.View };
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
        frame = frame with { TemporaryFrame = temporary!.Publish() };
        Dispose();
        return frame;
    }

    public void Dispose()
    {
        ExceptionDispatchInfo? failure = null;
        try
        {
            core?.Dispose();
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }
        try
        {
            arena?.Dispose();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
        try
        {
            temporary?.Dispose();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
        arena = null;
        core = null;
        temporary = null;
        active = false;
        failure?.Throw();
    }
}
