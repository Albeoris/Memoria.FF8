using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMoogle96NodeResource(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleMoogle96NodeBank([property: JsonRequired] uint Address, [property: JsonRequired] uint InitializationFlag, [property: JsonRequired] ImmutableArray<byte>? Data);
public sealed record NativeBattleMoogle96NodeState([property: JsonRequired] NativeBattleRecover93NodeState Work, [property: JsonRequired] ImmutableArray<NativeBattleMoogle96NodeBank> Banks)
{
    public static NativeBattleMoogle96NodeState FromColdProcess() => new(NativeBattleRecover93NodeState.FromColdProcess(), [new(0x169227C, 0, null), new(0x1696DE8, 0, null)]);
    public void Validate()
    {
        Work.Validate(0, 0x10E0, 0x10680);
        if (Work.InitializationFlag != 0 || Work.Data is not null || Banks.IsDefault || Banks.Length != 2 || Banks[0].Address != 0x169227C || Banks[1].Address != 0x1696DE8)
        {
            throw new InvalidDataException("Native Moogle nodes require two distinct source owners over one shared work owner.");
        }
        foreach (NativeBattleMoogle96NodeBank bank in Banks)
        {
            int length = bank.Address == 0x169227C ? 0x4B6C : 0x33D4;
            if (bank.Data is { } bytes && (bytes.IsDefault || bytes.Length != length || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != bank.InitializationFlag))
            {
                throw new InvalidDataException("Native Moogle node source contradicts its retained initialization owner.");
            }
            if (bank.InitializationFlag != 0 && bank.Data is null)
            {
                throw new NotSupportedException("Native Moogle fixed source bytes are missing and cannot be replaced with originals.");
            }
        }
    }
}
public sealed record NativeBattleMoogle96NodeFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleMoogle96NodeState State, NativeCrtRandomState Random);
public sealed record NativeBattleMoogle96NodeStep(NativeBattleMoogle96NodeFrame Frame, bool Complete, NativeBattleLogicalOrdering Ordering, ImmutableArray<NativeBattleGfGeometryBatch> PrivateSprites, ImmutableArray<NativeBattleGfGeometryBatch> CommonMeshes);

/// <summary>Two independent Moogle source banks sharing the single native40/100 work owner and neutral lifecycle.</summary>
public static class NativeBattleMoogle96Nodes
{
    public const uint Callback = 0x734840;
    private static NativeBattleEffectNodeCallerPolicy Policy(NativeBattleOriginalEffectState effect, uint source)
    {
        if (effect.Binding is not { Effect: 96, Entry: 0x731DE0, Initializer: 0x731DC0 })
        {
            throw new InvalidDataException("Native Moogle nodes require their actual96 module identity.");
        }
        int length = source switch
        {
            0x169227C => 0x4B6C,
            0x1696DE8 => 0x33D4,
            _ => throw new NotSupportedException("Native Moogle node source is outside its two retained bank owners.")
        };
        return new(source, length, Callback, 40, 100, true, 0x2000, CommonSprites: true);
    }
    public static NativeBattleMoogle96NodeFrame ClearStorage(NativeBattleMoogle96NodeFrame frame, int initializerTimArenaOffset)
    {
        frame.State.Validate();
        NativeBattleRecover93NodeFrame cleared = NativeBattleEffectNodeCaller.ClearStorage(Policy(frame.Effect, 0x169227C), new(frame.Memory, frame.Effect, frame.State.Work, frame.Random), initializerTimArenaOffset);
        return frame with { Memory = cleared.Memory, Effect = cleared.Effect, State = frame.State with { Work = cleared.State }, Random = cleared.Random };
    }
    public static NativeBattleMoogle96NodeFrame BeginPass(NativeBattleMoogle96NodeFrame frame, NativeBattleActionMatrix view, int privateCursor)
    {
        frame.State.Validate();
        NativeBattleRecover93NodeFrame pass = NativeBattleEffectNodeCaller.BeginPass(Policy(frame.Effect, 0x169227C), new(frame.Memory, frame.Effect, frame.State.Work, frame.Random), view, privateCursor);
        return frame with { State = frame.State with { Work = pass.State } };
    }
    public static NativeBattleMoogle96NodeFrame InitializeContext(NativeBattleMoogle96NodeFrame frame, uint address, NativeBattleMoogle96NodeResource resource, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource, NativeBattleEffectNodeDogOrigins dogOrigins)
    {
        frame.State.Validate();
        NativeBattleEffectNodeCallerPolicy profile = Policy(frame.Effect, resource.Address);
        uint source = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30);
        if (source != resource.Address)
        {
            throw new InvalidDataException("Native Moogle context source differs from its supplied original bank.");
        }
        int selected = source == 0x169227C ? 0 : 1;
        NativeBattleRecover93NodeFrame initialized = NativeBattleEffectNodeCaller.Initialize74EC00(profile, Assemble(frame, selected), address, new(resource.Data), packets, readModel, readPoseSource, dogOrigins);
        return Store(frame, selected, initialized);
    }
    public static NativeBattleMoogle96NodeStep AdvanceContext(NativeBattleMoogle96NodeFrame frame, uint address, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short roll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors)
    {
        frame.State.Validate();
        uint source = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadDword(0x30);
        NativeBattleEffectNodeCallerPolicy profile = Policy(frame.Effect, source);
        int selected = source == 0x169227C ? 0 : 1;
        if (frame.State.Banks[selected].InitializationFlag == 0)
        {
            throw new NotSupportedException("Native Moogle context requires its selected source's executed initialization.");
        }
        NativeBattleRecover93NodeStep step = NativeBattleEffectNodeCaller.Advance74F6E0(profile, Assemble(frame, selected), address, readModel, projection, roll, ordering, colors);
        return new(Store(frame, selected, step.Frame), step.Complete, step.Ordering, step.PrivateSprites, step.CommonMeshes);
    }
    private static NativeBattleRecover93NodeFrame Assemble(NativeBattleMoogle96NodeFrame frame, int selected)
    {
        NativeBattleMoogle96NodeBank bank = frame.State.Banks[selected];
        return new(frame.Memory, frame.Effect, frame.State.Work with { InitializationFlag = bank.InitializationFlag, Data = bank.Data }, frame.Random);
    }
    private static NativeBattleMoogle96NodeFrame Store(NativeBattleMoogle96NodeFrame frame, int selected, NativeBattleRecover93NodeFrame result)
    {
        NativeBattleMoogle96NodeBank bank = frame.State.Banks[selected] with { InitializationFlag = result.State.InitializationFlag, Data = result.State.Data };
        return frame with { Memory = result.Memory, Effect = result.Effect, Random = result.Random, State = new(result.State with { InitializationFlag = 0, Data = null }, frame.State.Banks.SetItem(selected, bank)) };
    }
}
