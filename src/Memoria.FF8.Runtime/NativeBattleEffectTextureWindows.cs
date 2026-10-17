using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The normalized E2 stores used by6C5750/6C5B50 and88E820/88ECA0/88FC50.</summary>
public static class NativeBattleEffectTextureWindows
{
    public static uint Encode(NativeBattleTextureWindow window)
    {
        return 0xE2000000u | (uint)((window.Y & 0xF8) << 12) | (uint)((window.X & 0xF8) << 7) | (uint)((~(window.Height - 1) & 0xF8) << 2) | (uint)((~(window.Width - 1) >> 3) & 31);
    }

    public static ImmutableArray<byte> OffsetAxis(IEnumerable<byte> coordinates, ushort offset, byte wrapDimension)
    {
        return OffsetAxisSigned(coordinates, offset, wrapDimension);
    }

    /// <summary>61BDC1's aggregate signed offset: overflow wins over underflow and every coordinate receives the same one-step adjustment.</summary>
    public static ImmutableArray<byte> OffsetAxisSigned(IEnumerable<byte> coordinates, int offset, short wrapDimension)
    {
        int[] values = coordinates.Select(value => unchecked(value + offset)).ToArray();
        int adjustment = values.Any(value => value >= 256) ? -wrapDimension : values.Any(value => value < 0) ? wrapDimension : 0;
        return values.Select(value => unchecked((byte)(value + adjustment))).ToImmutableArray();
    }

    internal static NativeBattleGfGeometryBatch Bracket(NativeBattleGfGeometryAttempt stores, NativeBattleGeometryPrimitive polygon, NativeBattleTextureWindow before, NativeBattleTextureWindow after)
    {
        int length = stores.PacketLength;
        SkinWrites linkOnly = new(length, stores.ByteOffsetRelative);
        NativeBattleGfGeometryAttempt first = Window(length, before);
        NativeBattleGfGeometryAttempt last = Window(length + 12, after);
        return new([stores with { Primitive = null }, first, linkOnly.Attempt(polygon), last], length + 24);

        NativeBattleGfGeometryAttempt Window(int relativeOffset, NativeBattleTextureWindow rectangle)
        {
            SkinWrites packet = new(12, checked(stores.ByteOffsetRelative + relativeOffset));
            packet.Word(0, 0x02000000);
            packet.Word(4, Encode(rectangle));
            packet.Word(8, 0);
            return packet.Attempt(new(polygon.Bucket, packet.Words(), [0, 0, 0, 0]));
        }
    }
}
