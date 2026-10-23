namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleAcid317Frame Frame, NativeBattleGraphicsState Graphics) SnapshotNativeAcid317Body(NativeBattleAcid317Frame frame, NativeBattleGraphicsState graphics, byte actor, uint cacheAddress)
    {
        NativeDeformedBodyBridge step = SnapshotNativeDeformedBody(frame.Memory, frame.Effect, frame.Random, frame.Staging ?? throw new NotSupportedException("Acid body requires its current staging owner."), graphics, actor, cacheAddress, NativeBattleAcid317Profile.BodyCacheBytes);
        return (frame with { Memory = step.Memory, Random = step.Random, Staging = step.Staging }, step.Graphics);
    }

    private (NativeBattleAcid317Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeAcid317Body(NativeBattleAcid317Frame frame, NativeBattleGraphicsState graphics, byte actor, short fraction)
    {
        uint cacheAddress = checked(frame.State.ReadDword(NativeBattleAcid317Profile.WorkingGlobal) + NativeBattleAcid317Profile.BodyCacheOffset);
        NativeDeformedBodyBridge step = DrawNativeDeformedBody(frame.Memory, frame.Effect, frame.Random, frame.Staging ?? throw new NotSupportedException("Acid body requires its current staging owner."), graphics, actor, fraction, cacheAddress, NativeBattleAcid317Profile.BodyCacheBytes);
        return (frame with { Memory = step.Memory, Random = step.Random, Staging = step.Staging }, step.Graphics);
    }
}
