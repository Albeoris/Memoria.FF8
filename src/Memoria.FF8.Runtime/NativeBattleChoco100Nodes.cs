using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100NodeFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleChoco97NodeState State, NativeBattleChoco100IoState? Io, NativeCrtRandomState Random);
public sealed record NativeBattleChoco100NodeStep(NativeBattleChoco100NodeFrame Frame, bool Complete, NativeBattleLogicalOrdering Ordering, ImmutableArray<NativeBattleGfGeometryBatch> PrivateSprites, ImmutableArray<NativeBattleGfGeometryBatch> CommonMeshes);

/// <summary>The static and file-loaded Choco node banks share work storage while loaded source writes retain their physical IO owner.</summary>
public static class NativeBattleChoco100Nodes
{
    public const uint StaticAddress = 0x165FEEC;
    public const int StaticLength = 0x3268;
    public const uint Loaded495Address = 0x19E3018;
    public const int Loaded495Length = 0x1958;
    public const uint Loaded501Address = 0x19D5018;
    public const int Loaded501Length = 0x7660;
    public const uint Callback = 0x70FAF0;

    private static NativeBattleEffectNodeCallerPolicy Policy(NativeBattleOriginalEffectState effect, uint source)
    {
        if (effect.Binding is not { Effect: 100, Entry: 0x70D390 })
        {
            throw new InvalidDataException("Native Choco nodes require their actual module identity.");
        }
        int length = source switch
        {
            StaticAddress => StaticLength,
            Loaded495Address => Loaded495Length,
            Loaded501Address => Loaded501Length,
            _ => throw new NotSupportedException("Native Choco node source leaves its three physical bank owners.")
        };
        return new(source, length, Callback, 40, 100, true, 0x2000, CommonSprites: true);
    }

    public static NativeBattleChoco100NodeFrame ClearStorage(NativeBattleChoco100NodeFrame frame, int initializerTimArenaOffset)
    {
        frame.State.Validate();
        NativeBattleRecover93NodeFrame cleared = NativeBattleEffectNodeCaller.ClearStorage(Policy(frame.Effect, StaticAddress), new(frame.Memory, frame.Effect, frame.State.Work, frame.Random), initializerTimArenaOffset);
        return frame with { Memory = cleared.Memory, Effect = cleared.Effect, State = frame.State with { Work = cleared.State }, Random = cleared.Random };
    }

    public static NativeBattleChoco100NodeFrame BeginPass(NativeBattleChoco100NodeFrame frame, NativeBattleActionMatrix view, int privateCursor)
    {
        frame.State.Validate();
        NativeBattleRecover93NodeFrame pass = NativeBattleEffectNodeCaller.BeginPass(Policy(frame.Effect, StaticAddress), new(frame.Memory, frame.Effect, frame.State.Work, frame.Random), view, privateCursor);
        return frame with { State = frame.State with { Work = pass.State } };
    }

    public static NativeBattleChoco100NodeFrame InitializeContext(NativeBattleChoco100NodeFrame frame, uint address, NativeBattleChoco97NodeResource resource, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource, NativeBattleEffectNodeDogOrigins origins)
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

    public static NativeBattleChoco100NodeStep AdvanceContext(NativeBattleChoco100NodeFrame frame, uint address, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short roll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors)
    {
        frame.State.Validate();
        uint source = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30);
        NativeBattleEffectNodeCallerPolicy profile = Policy(frame.Effect, source);
        NativeBattleRecover93NodeFrame current = Assemble(frame, source);
        if (current.State.InitializationFlag == 0)
        {
            throw new NotSupportedException("Native Choco context requires its current physical source's executed initialization.");
        }
        NativeBattleRecover93NodeStep step = NativeBattleEffectNodeCaller.Advance74F6E0(profile, current, address, readModel, projection, roll, ordering, colors);
        return new(Store(frame, source, step.Frame), step.Complete, step.Ordering, step.PrivateSprites, step.CommonMeshes);
    }

    private static NativeBattleRecover93NodeFrame Assemble(NativeBattleChoco100NodeFrame frame, uint source)
    {
        ImmutableArray<byte>? data = source == StaticAddress ? frame.State.StaticData : (frame.Io ?? throw new NotSupportedException("Native Choco loaded nodes require their initialized IO owner.")).Banks.Read(source, Policy(frame.Effect, source).SourceLength).ToImmutableArray();
        uint flag = data is { } bytes ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) : 0;
        return new(frame.Memory, frame.Effect, frame.State.Work with { InitializationFlag = flag, Data = data }, frame.Random);
    }

    private static NativeBattleChoco100NodeFrame Store(NativeBattleChoco100NodeFrame frame, uint source, NativeBattleRecover93NodeFrame result)
    {
        NativeBattleChoco97NodeState state = frame.State with { Work = result.State with { InitializationFlag = 0, Data = null } };
        NativeBattleChoco100IoState? io = frame.Io;
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
            io = io with { Banks = io.Banks.Write(source, result.State.Data ?? throw new InvalidDataException("Native Choco lost its loaded mutable source owner.")) };
        }
        return frame with { Memory = result.Memory, Effect = result.Effect, Random = result.Random, State = state, Io = io };
    }
}
