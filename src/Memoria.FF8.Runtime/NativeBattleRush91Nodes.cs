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
    public static NativeBattleRecover93NodeStep AdvanceContext(NativeBattleRecover93NodeFrame frame, uint taskAddress, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors, bool capturePresentation = false) => NativeBattleEffectNodeCaller.Advance74F6E0(Policy(frame.Effect), frame, taskAddress, readModel, projection, cameraRoll, ordering, colors, pilotIdentity: capturePresentation ? NativeBattlePilotVisualLifetimes.Identity(91, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress)) : null, preparePilotMeshOverride: capturePresentation ? (memory, node, source) =>
    {
        short fade = unchecked((short)memory.Word(node + 0x1CE));
        uint flags = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A)) == 0 ? 0x30u : 0;
        flags |= fade == 0 ? 0 : 0xC0u;
        NativeBattleFullLifeMeshContext context = new(flags, fade == 0 ? null : memory.Dword(node + 0x16C), fade == 0 ? null : fade, null, null, null, 17, 2);
        return (bytes, matrix, identity) => NativeBattleRetainedPolygonDepths.Capture(NativeBattleRestorationMeshPackets.EmitBatch884370(bytes, matrix, projection, context, colors, observeFirstThreeFlags: memory.WriteProjectionFlags, pilotIdentity: identity));
    } : null);
}
