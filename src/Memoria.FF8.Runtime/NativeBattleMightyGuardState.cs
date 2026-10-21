using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMightyGuardState([property: JsonRequired] ushort Effect, [property: JsonRequired] uint InitializationFlag, [property: JsonRequired] ImmutableArray<byte>? Executable)
{
    public int? SourceArenaOffset { get; init; }
    public int? PrimaryDrawCursor { get; init; }
    public int? SecondaryDrawCursor { get; init; }
    public int? AuxiliaryCursor { get; init; }
    public int? Emitters { get; init; }
    public int? Geometry { get; init; }
    public ushort EmitterScan { get; init; }
    public ushort GeometryScan { get; init; }
    public ushort EmitterReset { get; init; }
    public ushort GeometryReset { get; init; }
    public ushort PassEmitters { get; init; }
    public ushort PassGeometry { get; init; }
    public uint ContextTask { get; init; }
    public uint EntryCounter { get; init; }

    public static NativeBattleMightyGuardState FromColdProcess(ushort effect)
    {
        _ = NativeBattleMightyGuardProfile.ForEffect(effect);
        return new(effect, 0, null);
    }

    public void Validate()
    {
        NativeBattleMightyGuardProfile profile = NativeBattleMightyGuardProfile.ForEffect(Effect);
        if (Executable is { } bytes && (bytes.IsDefault || bytes.Length != profile.SourceLength || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != InitializationFlag))
        {
            throw new InvalidDataException("Mighty Guard requires its retained initialization flag and matching original executable bank.");
        }
    }

    public NativeBattleMightyGuardState InitializeSource(NativeBattleMightyGuardResources resources)
    {
        Validate();
        resources.Validate();
        if (resources.Effect != Effect)
        {
            throw new InvalidDataException("Mighty Guard cannot initialize another module's source bank.");
        }
        if (InitializationFlag != 0)
        {
            if (Executable is null)
            {
                throw new NotSupportedException("Mighty Guard requires its previously fixed executable bytes when the retained initialization flag is nonzero.");
            }
            return this;
        }
        byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(Executable ?? resources.NodeSource.Data, resources.NodeSource.ResourceAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(relocated, 1);
        return this with { InitializationFlag = 1, Executable = relocated.ToImmutableArray() };
    }
}

public sealed record NativeBattleMightyGuardFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleMightyGuardState State, NativeCrtRandomState Random)
{
    public NativeBattleMightyGuardProfile Profile => NativeBattleMightyGuardProfile.ForEffect(Effect.Binding.Effect);
    public NativeBattleActionMatrix Camera => (Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Mighty Guard requires its actual shared camera copy.")).ReadMatrix();
}

public abstract record NativeBattleMightyGuardOperation;
public sealed record NativeBattleMightyGuardContact(int Index) : NativeBattleMightyGuardOperation;
public sealed record NativeBattleMightyGuardSound(NativeBattleModuleSound Sound) : NativeBattleMightyGuardOperation;
public sealed record NativeBattleMightyGuardStage(short Depth, bool ClearRgb) : NativeBattleMightyGuardOperation;
public sealed record NativeBattleMightyGuardBindCaster(int Actor, uint SavedBank, uint Resource, byte Sequence) : NativeBattleMightyGuardOperation;
public sealed record NativeBattleMightyGuardInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleMightyGuardFrame, NativeBattleMightyGuardOperation, NativeBattleMightyGuardFrame> Execute, Func<NativeBattleMightyGuardFrame, uint, NativeBattleMightyGuardFrame> AdvanceNodes)
{
    public NativeBattlePackedMatrix32? PackedCamera { get; init; }
    public ushort? CopiedCameraPadding { get; init; }
}
