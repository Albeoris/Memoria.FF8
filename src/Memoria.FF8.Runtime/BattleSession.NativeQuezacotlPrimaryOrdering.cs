namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleLogicalOrdering? provisionalQuezacotlOrdering;
    private NativeBattleLogicalOrderingTransaction? provisionalQuezacotlOrderingTransaction;

    private void PublishNativeQuezacotlGraphics(NativeBattleGraphicsState graphics)
    {
        if (graphics.PrimitiveFrame is { } frame)
        {
            NativeBattleLogicalOrdering accepted = FreezeNativeQuezacotlOrderingPublication(frame.Pending, provisionalQuezacotlOrdering, provisionalQuezacotlOrderingTransaction);
            graphics = graphics with { PrimitiveFrame = frame with { Pending = accepted } };
        }
        AcceptNativeEffectPublication(graphics.OriginalEffect, graphics.PrimitiveFrame?.Pending);
        nativeGraphics = graphics;
    }

    internal static NativeBattleLogicalOrdering FreezeNativeQuezacotlOrderingPublication(NativeBattleLogicalOrdering pending, NativeBattleLogicalOrdering? provisional, NativeBattleLogicalOrderingTransaction? continuation)
    {
        NativeBattleLogicalOrdering accepted = pending.Transaction is { } producer ? producer.Freeze() : pending;
        if (continuation is not null && provisional is not null && pending.SharesWorkingStorage(provisional))
        {
            accepted = continuation.FreezeKnownAncestorPrefix();
        }
        return accepted;
    }

    private NativeBattleGraphicsState AdvanceNativeQuezacotlFirstWindow(NativeBattleGraphicsState graphics)
    {
        NativeBattleLogicalOrdering pending = (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Quezacotl primary pass requires its current ordering frame.")).Pending;
        (NativeBattleGraphicsState Graphics, NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, ushort Phase, NativeBattleGteDepthTrace DepthTrace) step;
        NativeBattleLogicalOrdering.Delta primaryDelta;
        using (NativeBattleLogicalOrderingTransaction ordering = new(pending))
        {
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.View } };
            ordering.BeginCheckpoint();
            try
            {
                step = AdvanceNativeQuezacotlFirstWindowCore(graphics, ordering);
            }
            catch
            {
                ordering.RollbackCheckpoint();
                PublishOrdering(ordering.Freeze());
                throw;
            }
            primaryDelta = ordering.SuspendCheckpointDelta();
            step.Graphics = step.Graphics with { PrimitiveFrame = step.Graphics.PrimitiveFrame! with { Pending = pending } };
            PublishOrdering(ordering.Freeze());

            void PublishOrdering(NativeBattleLogicalOrdering value)
            {
                if (NativeGraphics is { PrimitiveFrame: { } frame } published && ReferenceEquals(frame.Pending.Transaction, ordering))
                {
                    NativeGraphics = published with { PrimitiveFrame = frame with { Pending = value } };
                }
            }
        }
        using NativeBattleLogicalOrderingTransaction continuation = new(pending);
        continuation.ResumeDelta(primaryDelta);
        NativeBattleLogicalOrdering? previous = provisionalQuezacotlOrdering;
        NativeBattleLogicalOrderingTransaction? previousTransaction = provisionalQuezacotlOrderingTransaction;
        provisionalQuezacotlOrdering = pending;
        provisionalQuezacotlOrderingTransaction = continuation;
        try
        {
            NativeBattleGraphicsState result = FinishNativeQuezacotlSecondaryLists(step.Graphics, step.Memory, step.Effect, step.Phase, step.DepthTrace);
            NativeBattleLogicalOrdering accepted = continuation.Freeze();
            return result with { PrimitiveFrame = result.PrimitiveFrame! with { Pending = accepted } };
        }
        finally
        {
            provisionalQuezacotlOrdering = previous;
            provisionalQuezacotlOrderingTransaction = previousTransaction;
        }
    }
}
