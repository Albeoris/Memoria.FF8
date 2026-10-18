namespace Memoria.FF8.Runtime;

/// <summary>Rush's independently retained source and FullLife-style mesh policy over the shared node lifecycle.</summary>
public static class NativeBattleRush91Nodes
{
    private static readonly NativeBattleEffectNodeCallerPolicy Caller = new(0x16CB3FC, 0xEF0, 0x759B40, 20, 70, true);
    private static NativeBattleEffectNodeCallerPolicy Policy(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 91, Entry: 0x759370, Initializer: 0x759350 })
        {
            throw new InvalidDataException("Native Rush nodes require their actual91 entry and resource owner.");
        }
        return Caller;
    }
    public static NativeBattleRecover93NodeFrame ClearStorage(NativeBattleRecover93NodeFrame frame, int initializerTimArenaOffset) => NativeBattleEffectNodeCaller.ClearStorage(Policy(frame.Effect), frame, initializerTimArenaOffset);
    public static NativeBattleRecover93NodeFrame BeginPass(NativeBattleRecover93NodeFrame frame, NativeBattleActionMatrix copiedCamera, int privateDrawCursor) => NativeBattleEffectNodeCaller.BeginPass(Policy(frame.Effect), frame, copiedCamera, privateDrawCursor);
    public static NativeBattleRecover93NodeFrame InitializeContext(NativeBattleRecover93NodeFrame frame, uint taskAddress, NativeBattleRecover93NodeResource resource, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource) => NativeBattleEffectNodeCaller.Initialize74EC00(Policy(frame.Effect), frame, taskAddress, resource, packets, readModel, readPoseSource);
    public static NativeBattleRecover93NodeStep AdvanceContext(NativeBattleRecover93NodeFrame frame, uint taskAddress, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors) => NativeBattleEffectNodeCaller.Advance74F6E0(Policy(frame.Effect), frame, taskAddress, readModel, projection, cameraRoll, ordering, colors);
}
