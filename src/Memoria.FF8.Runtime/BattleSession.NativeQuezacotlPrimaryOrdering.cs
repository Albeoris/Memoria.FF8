namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeQuezacotlFirstWindow(NativeBattleGraphicsState graphics)
    {
        NativeBattleLogicalOrdering pending = (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Quezacotl primary pass requires its current ordering frame.")).Pending;
        (NativeBattleGraphicsState Graphics, NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, ushort Phase, NativeBattleGteDepthTrace DepthTrace) step;
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
            NativeBattleLogicalOrdering frozen = ordering.Freeze();
            step.Graphics = step.Graphics with { PrimitiveFrame = step.Graphics.PrimitiveFrame! with { Pending = frozen } };
            ordering.RollbackCheckpoint();
            PublishOrdering(ordering.Freeze());

            void PublishOrdering(NativeBattleLogicalOrdering value)
            {
                if (NativeGraphics is { PrimitiveFrame: { } frame } published && ReferenceEquals(frame.Pending.Transaction, ordering))
                {
                    NativeGraphics = published with { PrimitiveFrame = frame with { Pending = value } };
                }
            }
        }
        return FinishNativeQuezacotlSecondaryLists(step.Graphics, step.Memory, step.Effect, step.Phase, step.DepthTrace);
    }
}
