namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleLogicalOrdering? provisionalQuezacotlOrdering;
    private bool provisionalQuezacotlOrderingPublished;

    private void PublishNativeQuezacotlGraphics(NativeBattleGraphicsState graphics)
    {
        if (provisionalQuezacotlOrdering is { } pending && graphics.PrimitiveFrame?.Pending.SharesWorkingStorage(pending) == true)
        {
            provisionalQuezacotlOrderingPublished = true;
        }
        AcceptNativeEffectPublication(graphics.OriginalEffect);
        nativeGraphics = graphics;
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
        bool previousPublished = provisionalQuezacotlOrderingPublished;
        provisionalQuezacotlOrdering = pending;
        provisionalQuezacotlOrderingPublished = false;
        try
        {
            NativeBattleGraphicsState result = FinishNativeQuezacotlSecondaryLists(step.Graphics, step.Memory, step.Effect, step.Phase, step.DepthTrace);
            NativeBattleLogicalOrdering accepted = continuation.Freeze();
            return result with { PrimitiveFrame = result.PrimitiveFrame! with { Pending = accepted } };
        }
        catch
        {
            if (provisionalQuezacotlOrderingPublished)
            {
                continuation.Freeze();
            }
            throw;
        }
        finally
        {
            provisionalQuezacotlOrdering = previous;
            provisionalQuezacotlOrderingPublished = previousPublished;
        }
    }
}
