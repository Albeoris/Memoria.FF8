using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco98NodeFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleChoco97NodeState State, NativeCrtRandomState Random);
public sealed record NativeBattleChoco98NodeStep(NativeBattleChoco98NodeFrame Frame, bool Complete, NativeBattleLogicalOrdering Ordering, ImmutableArray<NativeBattleGfGeometryBatch> PrivateSprites, ImmutableArray<NativeBattleGfGeometryBatch> CommonMeshes);

public static class NativeBattleChoco98Nodes
{
    public const uint StaticAddress = 0x167D934;
    public const int StaticLength = 0x3268;
    public const uint Callback = 0x722920;

    private static NativeBattleEffectNodeCallerPolicy Policy(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 98, Entry: 0x721860 })
        {
            throw new InvalidDataException("Native Choco nodes require their actual module identity.");
        }
        return new(StaticAddress, StaticLength, Callback, 40, 100, true, 0x2000, CommonSprites: true);
    }

    public static NativeBattleChoco98NodeFrame ClearStorage(NativeBattleChoco98NodeFrame frame, int initializerTimArenaOffset)
    {
        frame.State.Validate();
        NativeBattleRecover93NodeFrame result = NativeBattleEffectNodeCaller.ClearStorage(Policy(frame.Effect), new(frame.Memory, frame.Effect, frame.State.Work, frame.Random), initializerTimArenaOffset);
        return frame with { Memory = result.Memory, Effect = result.Effect, State = frame.State with { Work = result.State }, Random = result.Random };
    }

    public static NativeBattleChoco98NodeFrame BeginPass(NativeBattleChoco98NodeFrame frame, NativeBattleActionMatrix view, int privateCursor)
    {
        frame.State.Validate();
        NativeBattleRecover93NodeFrame result = NativeBattleEffectNodeCaller.BeginPass(Policy(frame.Effect), new(frame.Memory, frame.Effect, frame.State.Work, frame.Random), view, privateCursor);
        return frame with { State = frame.State with { Work = result.State } };
    }

    public static NativeBattleChoco98NodeFrame InitializeContext(NativeBattleChoco98NodeFrame frame, uint address, NativeBattleChoco97NodeResource resource, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource, NativeBattleEffectNodeDogOrigins origins)
    {
        if (resource.Address != StaticAddress || new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30) != StaticAddress)
        {
            throw new InvalidDataException("Native Choco nodes support only their actual static source.");
        }
        NativeBattleRecover93NodeFrame result = NativeBattleEffectNodeCaller.Initialize74EC00(Policy(frame.Effect), Assemble(frame), address, new(resource.Data), packets, readModel, readPoseSource, origins);
        return Store(frame, result);
    }

    public static NativeBattleChoco98NodeStep AdvanceContext(NativeBattleChoco98NodeFrame frame, uint address, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short roll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors)
    {
        NativeBattleRecover93NodeFrame current = Assemble(frame);
        if (current.State.InitializationFlag == 0 || new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30) != StaticAddress)
        {
            throw new NotSupportedException("Native Choco nodes require their executed static source initialization.");
        }
        NativeBattleRecover93NodeStep result = NativeBattleEffectNodeCaller.Advance74F6E0(Policy(frame.Effect), current, address, readModel, projection, roll, ordering, colors);
        return new(Store(frame, result.Frame), result.Complete, result.Ordering, result.PrivateSprites, result.CommonMeshes);
    }

    private static NativeBattleRecover93NodeFrame Assemble(NativeBattleChoco98NodeFrame frame)
    {
        frame.State.Validate();
        NativeBattleNodeSource? data = frame.State.StaticData;
        uint flag = data is { } bytes ? bytes.ReadUInt32(0) : 0;
        return new(frame.Memory, frame.Effect, frame.State.Work with { InitializationFlag = flag, Data = data }, frame.Random);
    }

    private static NativeBattleChoco98NodeFrame Store(NativeBattleChoco98NodeFrame frame, NativeBattleRecover93NodeFrame result) => frame with { Memory = result.Memory, Effect = result.Effect, State = frame.State with { Work = result.State with { InitializationFlag = 0, Data = null }, StaticData = result.State.Data }, Random = result.Random };
}
