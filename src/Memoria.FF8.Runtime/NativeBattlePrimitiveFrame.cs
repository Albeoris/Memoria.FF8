using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattlePrimitiveProcess(ushort? TexturePage, byte? Buffer80, string? PacketBank, byte PacketBuffer, int? ByteCursor)
{
    public byte? ScreenFadeEB8 { get; init; }
    public byte? PublicationState03A { get; init; }
    public uint? TextureWindowBits { get; init; }
    public NativeBattlePcTextureWindowCache? TextureWindowCache { get; init; }
    public ImmutableArray<bool?> TablePerspective { get; init; } = [null, null];
    public bool? DisableTableReset { get; init; }
    public NativeBattlePrimitiveDepthState? Depth { get; init; }
    public NativeBattlePrimitiveColorState? Color { get; init; }

    public void Validate()
    {
        ValidateCommonPacketMemory();
        TextureWindowCache?.Validate();
        if (TextureWindowBits is > 0xFFFFFF || Buffer80 is > 1 || PacketBuffer > 1 || TablePerspective.IsDefault || TablePerspective.Length != 2 || (PacketBank is null) != (ByteCursor is null) || PacketBank is not (null or "off_B6D070" or "off_B6D094") || ByteCursor is < 0 || ByteCursor is int cursor && (cursor & 3) != 0 || PacketBank == "off_B6D070" && ByteCursor is > 114688 || PacketBank == "off_B6D094" && ByteCursor is > 147456 || Depth is { } depth && (!float.IsFinite(depth.BucketMix) || !float.IsFinite(depth.RemainingWeight) || depth.BucketMix is < 0 or > 1 || depth.RemainingWeight is < 0 or > 1))
        {
            throw new InvalidDataException("Native primitive process requires bounded logical banks, original per-bank descriptors and finite retained GPU depth state.");
        }
    }

    public static NativeBattlePrimitiveProcess FromColdProcess() => new(0, 0, null, 0, null) { TextureWindowBits = 0, TextureWindowCache = NativeBattlePcTextureWindowCache.Reset4647A0(null) with { ScaleU = 0, ScaleV = 0 }, DisableTableReset = false, ScreenFadeEB8 = 0, PublicationState03A = 0, Depth = NativeBattlePrimitiveDepthState.FromColdProcess(), Color = NativeBattlePrimitiveColorState.FromColdProcess() };
    public NativeBattlePrimitiveProcess Initialize500400() => this with { PublicationState03A = 0, PacketBank = "off_B6D070", PacketBuffer = 0, ByteCursor = 0, TablePerspective = TablePerspective.SetItem(0, false) };

    public NativeBattlePrimitiveProcess Reset500900(uint flags)
    {
        byte buffer = Buffer80 ?? throw new NotSupportedException("Native500900 requires its retained buffer BYTE1D96A80.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native battle primitive buffer exceeds its two physical banks.");
        }
        bool disable = DisableTableReset ?? throw new NotSupportedException("Native500900 requires its retained1CFF834 OT-reset gate.");
        if (disable)
        {
            throw new NotSupportedException("Native500900 retained OT links require their physical proxy-bank reuse owner when1CFF834 disables table reset.");
        }
        //45D550→45CC80 clears only the selected bank and publishes its descriptor DWORD8=1.
        return this with { PacketBank = (flags & 0x80) == 0 ? "off_B6D070" : "off_B6D094", PacketBuffer = buffer, ByteCursor = 0, TablePerspective = TablePerspective.SetItem(buffer, true) };
    }
}

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattlePrimitiveProcess? PrimitiveProcess { get; init; }
}

public sealed record NativeBattlePrimitiveFrame(NativeBattleLogicalOrdering Pending, ImmutableArray<NativeBattleLogicalResolvedCommand> LastCommands, ImmutableArray<int?> LastModuleBlendModes)
{
    public bool? LastDescriptorPerspective { get; init; }
    public NativeBattlePrimitiveDepthState? LastDepthState { get; init; }
    public NativeBattlePrimitiveColorState? LastColorState { get; init; }
    public static NativeBattlePrimitiveFrame Clear() => new(NativeBattleLogicalOrdering.Clear(), [], []);
}

public sealed partial record NativeBattleGraphicsState
{
    public NativeBattlePrimitiveFrame? PrimitiveFrame { get; init; }
}

public sealed partial record NativeBattleGraphicsDefinition
{
    public NativeBattlePrimitiveTables? PrimitiveTables { get; init; }
}
