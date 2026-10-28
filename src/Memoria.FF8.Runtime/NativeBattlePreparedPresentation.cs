using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Completed command payload. Pending ordering and physical packet storage are deliberately absent.</summary>
public sealed record NativeBattlePreparedPrimitiveFrame(ImmutableArray<NativeBattleLogicalResolvedCommand> LastCommands, ImmutableArray<int?> LastModuleBlendModes)
{
    public bool? LastDescriptorPerspective { get; init; }
    public NativeBattlePrimitiveDepthState? LastDepthState { get; init; }
    public NativeBattlePrimitiveColorState? LastColorState { get; init; }
    public ImmutableArray<NativeBattlePcTextureWindowPixels>? LastTextureSnapshots { get; init; }
    public NativeBattlePcRenderTailReceipt? LastPcRenderTail { get; init; }

    public static NativeBattlePreparedPrimitiveFrame From(NativeBattlePrimitiveFrame frame) => new(frame.LastCommands, frame.LastModuleBlendModes) { LastDescriptorPerspective = frame.LastDescriptorPerspective, LastDepthState = frame.LastDepthState, LastColorState = frame.LastColorState, LastTextureSnapshots = frame.LastTextureSnapshots, LastPcRenderTail = frame.LastPcRenderTail };
}

public sealed record NativeBattlePreparedActor(string Id, string Name, bool IsPlayer, int ModelId, int Costume, int? WeaponId, int? SceneSlot, int? GuardianFormVisual, NativeBattleModelTextureBinding BodyTextures, NativeBattleModelTextureBinding? LinkedTextures);
public sealed record NativeBattlePreparedScanActor(int ModelId, string Name, bool IsPlayer, bool HasNativeDisplayNameOverride);
public enum NativeBattlePreparedScanExtraFailure
{
    None,
    MissingName,
    InvalidIndex,
    InvalidLength
}
public sealed record NativeBattlePreparedScanExtra(NativeBattleScanByteOwner? Row, NativeBattlePreparedScanExtraFailure Failure)
{
    public byte ReadByte(int offset) => Failure switch
    {
        NativeBattlePreparedScanExtraFailure.MissingName => throw new NotSupportedException("Native Scan extra text requires its actual AI name buffer."),
        NativeBattlePreparedScanExtraFailure.InvalidIndex => throw new IndexOutOfRangeException(),
        NativeBattlePreparedScanExtraFailure.InvalidLength => throw new InvalidDataException("Native Scan extra text exceeds its physical row."),
        _ => (Row ?? throw new InvalidOperationException("Native Scan extra text has no prepared row.")).ReadByte(offset)
    };
}

/// <summary>Scan text reads only these retained immutable banks, never a live battle callback.</summary>
public sealed record NativeBattlePreparedScan(NativeBattleScanTextFrame Text, NativeBattleScanResultMemory Results, NativeBattleScanByteOwner Name, ImmutableDictionary<int, NativeBattlePreparedScanExtra> EnemyExtra, ImmutableDictionary<byte, ImmutableArray<byte>> Names, NativeBattleScanTextResources? Resources, NativeBattleScanByteOwner? Effect68Empty, NativeBattlePreparedScanActor? Actor)
{
    public NativeBattleScanTextExpansionServices CreateExpansionServices() => new(ReadByte, _ => throw new NotSupportedException("Native Scan names are supplied by their canonical encoded content owner."))
    {
        ReadNameBytes = operand => Names.TryGetValue(operand, out ImmutableArray<byte> bytes) ? bytes : throw new NotSupportedException($"Native Scan replacement name{operand:X2} has no canonical content owner.")
    };

    private byte ReadByte(uint address)
    {
        if (address == NativeBattleEffect68Profile.ResultEmptyAddress)
        {
            return (Effect68Empty ?? throw new NotSupportedException("Effect68 text requires its retained result header.")).ReadByte(0);
        }
        return NativeBattleScanResults.ReadByte(Results, address, ReadExternal);
    }

    private byte ReadExternal(uint address)
    {
        if (address >= 0x269AA78 && address < 0x269AA90)
        {
            return Name.ReadByte((int)(address - 0x269AA78));
        }
        if (address >= NativeBattleScanResults.EnemyExtraAddress && address < NativeBattleScanResults.EnemyExtraAddress + 71 * 4)
        {
            int offset = (int)(address - NativeBattleScanResults.EnemyExtraAddress);
            return EnemyExtra.TryGetValue(3 + offset / 71, out NativeBattlePreparedScanExtra? row) ? row.ReadByte(offset % 71) : throw new InvalidOperationException("Native Scan text references an unallocated enemy extra row.");
        }
        return (Resources ?? throw new NotSupportedException("Native Scan text requires its original description bank.")).ReadDescriptionByte(address);
    }
}

/// <summary>One completed host presentation, composed from stable immutable components after immediate UI publication.</summary>
public sealed record NativeBattlePreparedPresentation(long Version, long Callback, long Frame, bool GuardianAnimationSkipPending, NativeBattlePreparedPrimitiveFrame Primitives, NativeBattleEffectTextureState? Textures, NativeBattlePcTextureWindowCache? TextureWindows, NativeBattlePcUiFrame? UiFrame, NativeBattleCameraPose? Camera, ImmutableArray<NativeBattleDamageTextDraw> DamageText, int? StageResourceId, long? StageTextureAnimationStartCallback, ImmutableArray<NativeBattlePreparedActor> Actors, NativeBattlePreparedScan? Scan)
{
    public ushort? PilotEffect { get; init; }
    public long PilotGeneration { get; init; }
    public NativeBattleCameraProjection? PilotProjection { get; init; }
    public NativeBattleCameraPose? PilotCamera { get; init; }
}
