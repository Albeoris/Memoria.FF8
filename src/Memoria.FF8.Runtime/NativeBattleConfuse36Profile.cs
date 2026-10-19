using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleConfuse36Profile
{
    public ushort Effect => 36;
    public uint Entry => 0x85F4E0;
    public uint Initializer => 0x85F4C0;
    public uint InitializerGlobal => 0x26A8E7C;
    public uint MainCallback => 0x85F620;
    public uint TargetCallback => 0x85F840;
    public uint GenericCallback => 0x85FB40;
    public uint ExecutableAddress => 0x15E5570;
    public int DrawBankBytes => 0xE000;
    public int EmitterBytes => 0xCA8;
    public int GeometryBytes => 0x8340;
    public ushort TargetReleaseCounter => 30;
    public ImmutableArray<uint> Descriptors => [0x26A8F70, 0x26A9A20, 0x26A9780, 0x26A8DB8, 0x26A9A10];
    public ImmutableArray<uint> Banks => [0x26A8EA8, 0x26A9A30, 0x26A8F90, 0x26A8DC8, 0x26A9790];
    public static NativeBattleConfuse36Profile ForEffect(ushort effect) => effect == 36 ? new() : throw new InvalidDataException("Native Confuse requires its original36 module.");
}

public sealed record NativeBattleConfuse36Resources(NativeBattleEffectHeapAllocation Animation, uint Sound, ImmutableArray<byte> Executable, ImmutableArray<uint> Overlay, ImmutableArray<short> Shake)
{
    public ushort Effect => 36;
    public ImmutableArray<byte> ConstantChannels { get; init; } = [];

    public byte[] ReadConstantChannel(uint address, int count)
    {
        if (ConstantChannels.IsDefault || ConstantChannels.Length != 0x2BBC || address < 0x26A9BC4 || count < 0 || (ulong)address + (uint)count > 0x26AC780)
        {
            throw new NotSupportedException("Native Confuse channel read leaves its original loader-owned constant arrays.");
        }
        return ConstantChannels.AsSpan().Slice(checked((int)(address - 0x26A9BC4)), count).ToArray();
    }
}

public sealed record NativeBattleConfuse36State([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    public int? Emitters { get; init; }
    public int? Geometry { get; init; }
    public ushort? EmitterScanCursor { get; init; }
    public ushort? GeometryScanCursor { get; init; }
    public ushort? EmitterResetWord { get; init; }
    public ushort? GeometryResetWord { get; init; }
    public ushort? PassEmitterCount { get; init; }
    public ushort? PassGeometryCount { get; init; }
}

public sealed record NativeBattleConfuse36Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleConfuse36State State);
public sealed record NativeBattleConfuse36LeafStep(NativeBattleConfuse36Frame Frame, int Result);
public sealed record NativeBattleConfuse36ControllerStep(NativeBattleConfuse36Frame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);
public sealed record NativeBattleConfuse36ControllerInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource)
{
    public Func<NativeBattleConfuse36Frame, int, NativeBattleConfuse36Frame>? ApplyContact { get; init; }
    public Func<NativeBattleConfuse36Frame, byte, NativeBattleConfuse36Frame>? TransformTarget { get; init; }
}
public sealed record NativeBattleConfuse36ContextStep(NativeBattleConfuse36Frame Frame, bool Complete);
public sealed record NativeBattleConfuse36GenericInputs(Func<NativeBattleConfuse36Frame, uint, NativeBattleConfuse36Frame> InitializeContext, Func<NativeBattleConfuse36Frame, uint, NativeBattleConfuse36ContextStep> AdvanceContext);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleConfuse36State? Confuse36 { get; init; }
}

public sealed partial record NativeBattleEffectMemory
{
    public ImmutableArray<byte>? Confuse36Executable { get; init; }
    public bool? Confuse36ExecutableInitialized { get; init; }
}
