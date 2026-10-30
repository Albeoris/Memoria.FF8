using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97NodeState([property: JsonRequired] NativeBattleRecover93NodeState Work, [property: JsonRequired] NativeBattleNodeSource? StaticData)
{
    public static NativeBattleChoco97NodeState FromColdProcess() => new(NativeBattleRecover93NodeState.FromColdProcess(), null);

    public void Validate()
    {
        Work.Validate(0, 0x10E0, 0x10680);
        if (Work.InitializationFlag != 0 || Work.Data is not null || StaticData is { } data && (data.IsDefault || data.Length != NativeBattleChoco97Nodes.StaticLength))
        {
            throw new InvalidDataException("Native Choco nodes require one static source and a separate shared work owner.");
        }
    }
}

public sealed record NativeBattleChoco97NodeResource(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleChoco97NodeFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleChoco97NodeState State, NativeBattleChoco97IoState? Io, NativeCrtRandomState Random);
public sealed record NativeBattleChoco97NodeStep(NativeBattleChoco97NodeFrame Frame, bool Complete, NativeBattleLogicalOrdering Ordering, ImmutableArray<NativeBattleGfGeometryBatch> PrivateSprites, ImmutableArray<NativeBattleGfGeometryBatch> CommonMeshes);

/// <summary>The static and file-loaded Choco node banks share work storage while loaded source writes retain their physical IO owner.</summary>
public static class NativeBattleChoco97Nodes
{
    public const uint StaticAddress = 0x168C654;
    public const int StaticLength = 0x3268;
    public const uint LoadedAddress = 0x19DFEF8;
    public const int LoadedLength = 0x4A60;
    public const uint Callback = 0x72AB20;

    private static NativeBattleEffectNodeCallerPolicy Policy(NativeBattleOriginalEffectState effect, uint source)
    {
        if (effect.Binding is not { Effect: 97, Entry: 0x729A60 })
        {
            throw new InvalidDataException("Native Choco nodes require their actual module identity.");
        }
        int length = source switch
        {
            StaticAddress => StaticLength,
            LoadedAddress => LoadedLength,
            _ => throw new NotSupportedException("Native Choco node source leaves its two physical bank owners.")
        };
        return new(source, length, Callback, 40, 100, true, 0x2000, CommonSprites: true);
    }

    public static NativeBattleChoco97NodeFrame ClearStorage(NativeBattleChoco97NodeFrame frame, int initializerTimArenaOffset)
    {
        frame.State.Validate();
        NativeBattleRecover93NodeFrame cleared = NativeBattleEffectNodeCaller.ClearStorage(Policy(frame.Effect, StaticAddress), new(frame.Memory, frame.Effect, frame.State.Work, frame.Random), initializerTimArenaOffset);
        return frame with { Memory = cleared.Memory, Effect = cleared.Effect, State = frame.State with { Work = cleared.State }, Random = cleared.Random };
    }

    public static NativeBattleChoco97NodeFrame BeginPass(NativeBattleChoco97NodeFrame frame, NativeBattleActionMatrix view, int privateCursor)
    {
        frame.State.Validate();
        NativeBattleRecover93NodeFrame pass = NativeBattleEffectNodeCaller.BeginPass(Policy(frame.Effect, StaticAddress), new(frame.Memory, frame.Effect, frame.State.Work, frame.Random), view, privateCursor);
        return frame with { State = frame.State with { Work = pass.State } };
    }

    public static NativeBattleChoco97NodeFrame InitializeContext(NativeBattleChoco97NodeFrame frame, uint address, NativeBattleChoco97NodeResource resource, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource, NativeBattleEffectNodeDogOrigins origins)
    {
        frame.State.Validate();
        NativeBattleEffectNodeCallerPolicy profile = Policy(frame.Effect, resource.Address);
        uint source = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30);
        if (source != resource.Address)
        {
            throw new InvalidDataException("Native Choco node context differs from its supplied original source.");
        }
        NativeBattleRecover93NodeFrame initialized = NativeBattleEffectNodeCaller.Initialize74EC00(profile, Assemble(frame, source), address, new(resource.Data), packets, readModel, readPoseSource, origins);
        return Store(frame, source, initialized);
    }

    public static NativeBattleChoco97NodeStep AdvanceContext(NativeBattleChoco97NodeFrame frame, uint address, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short roll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors)
    {
        frame.State.Validate();
        uint source = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30);
        NativeBattleEffectNodeCallerPolicy profile = Policy(frame.Effect, source);
        NativeBattleRecover93NodeFrame current = Assemble(frame, source);
        if (current.State.InitializationFlag == 0)
        {
            throw new NotSupportedException("Native Choco context requires its current physical source's executed initialization.");
        }
        NativeBattleRecover93NodeStep step = NativeBattleEffectNodeCaller.Advance74F6E0(profile, current, address, readModel, projection, roll, ordering, colors, NativeBattleChoco97Uv.MutateDrawableResource, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(97, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address)), captureBuiltInMesh: true);
        return new(Store(frame, source, step.Frame), step.Complete, step.Ordering, step.PrivateSprites, step.CommonMeshes);
    }

    private static NativeBattleRecover93NodeFrame Assemble(NativeBattleChoco97NodeFrame frame, uint source)
    {
        NativeBattleNodeSource? data = source == StaticAddress ? frame.State.StaticData : NativeBattleEffectNodeBank.BorrowSource((frame.Io ?? throw new NotSupportedException("Native Choco loaded nodes require their initialized IO owner.")).Banks, LoadedAddress, LoadedLength);
        uint flag = data is { } bytes ? bytes.ReadUInt32(0) : 0;
        return new(frame.Memory, frame.Effect, frame.State.Work with { InitializationFlag = flag, Data = data }, frame.Random);
    }

    private static NativeBattleChoco97NodeFrame Store(NativeBattleChoco97NodeFrame frame, uint source, NativeBattleRecover93NodeFrame result)
    {
        NativeBattleChoco97NodeState state = frame.State with { Work = result.State with { InitializationFlag = 0, Data = null } };
        NativeBattleChoco97IoState? io = frame.Io;
        if (source == StaticAddress)
        {
            state = state with { StaticData = result.State.Data };
        }
        else
        {
            if (io is null)
            {
                throw new NotSupportedException("Native Choco loaded node stores require their initialized IO owner.");
            }
            (result.State.Data ?? throw new InvalidDataException("Native Choco lost its loaded mutable source owner.")).RequireStorage(io.Banks.Bytes, checked((int)(LoadedAddress - io.Banks.BaseAddress)));
        }
        return frame with { Memory = result.Memory, Effect = result.Effect, Random = result.Random, State = state, Io = io };
    }
}
