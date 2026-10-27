using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

[method: JsonConstructor]
public sealed record NativeBattleMightyGuardState([property: JsonRequired] ushort Effect, [property: JsonRequired] uint InitializationFlag, [property: JsonRequired, JsonConverter(typeof(NativeBattleNodeSource.Converter))] NativeBattleNodeSource? Executable)
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
        if (Executable is { } bytes && (bytes.Length != profile.SourceLength || bytes.ReadUInt32(0) != InitializationFlag))
        {
            throw new InvalidDataException("Mighty Guard requires its retained initialization flag and matching original executable bank.");
        }
    }

    public NativeBattleMightyGuardState InitializeSource(NativeBattleMightyGuardResources resources)
    {
        using NativeBattleNodeInitialization<NativeBattleMightyGuardState> initialization = PrepareSource(resources);
        return initialization.Publish();
    }

    internal NativeBattleNodeInitialization<NativeBattleMightyGuardState> PrepareSource(NativeBattleMightyGuardResources resources)
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
            return new(this, null);
        }
        NativeBattleNodeSource data = Executable ?? new(resources.NodeSource.Data);
        NativeBattleTaskOperation.AdmitCurrentRetainedArena(data.Arena);
        NativeBattleNodeBankOperation operation = new(data);
        try
        {
            NativeBattleEffectNodeBankFixups.Relocate(operation, resources.NodeSource.ResourceAddress);
            operation.WriteUInt32(0, 1);
            return new(this with { InitializationFlag = 1, Executable = data }, operation);
        }
        catch
        {
            operation.Dispose();
            throw;
        }
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
