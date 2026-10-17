using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A50A500 call to4A93D0, before the separate PC UI-immediate backend.</summary>
public sealed record NativeBattleCountdownText(uint TableByteOffset, uint PackedPosition, uint Color, ImmutableArray<byte> Glyphs);

public static class NativeBattleStatusCountdowns
{
    /// <summary>Consumes the original F0/56C880 projected WORDs and participant timers; never advances timers.</summary>
    public static ImmutableArray<NativeBattleCountdownText> Layout50A410(uint statuses, short screenX, short screenY, ushort depth, uint projectionFlags, short? doomTimer, short? petrifyTimer)
    {
        if ((projectionFlags & 0x60000) != 0)
        {
            return [];
        }
        ImmutableArray<NativeBattleCountdownText>.Builder result = ImmutableArray.CreateBuilder<NativeBattleCountdownText>();
        short y = unchecked((short)(screenY - 8));
        if ((statuses & 0x2000) != 0)
        {
            result.AddRange(Layout50A500(screenX, y, depth, doomTimer ?? throw new NotSupportedException("Native50A410 requires participant status timer12 for Doom."), 0xFFFFFF));
            y = unchecked((short)(y - 8));
        }
        if ((statuses & 0x80000) != 0)
        {
            result.AddRange(Layout50A500(screenX, y, depth, petrifyTimer ?? throw new NotSupportedException("Native50A410 requires participant status timer10 for Petrify."), 0xFF));
        }
        return result.ToImmutable();
    }

    public static ImmutableArray<NativeBattleCountdownText> Layout50A500(short screenX, short screenY, ushort depth, short timer, uint color)
    {
        if (timer == -1111)
        {
            return [];
        }
        int value = (Math.Max(0, (int)timer) + 10) / 30;
        ImmutableArray<byte> glyphs = value < 10 ? [(byte)(0x18 + value), 0] : [unchecked((byte)(0x18 + value / 10)), (byte)(0x18 + value % 10), 0];
        ushort x = unchecked((ushort)((screenX >> 3) - (value < 10 ? 4 : 8)));
        ushort y = unchecked((ushort)(screenY >> 3));
        uint packedPosition = x | ((uint)y << 16);
        return [new(0x24u + 4u * (uint)(depth >> 2), packedPosition, color, glyphs), new(0, packedPosition, color | 0x04000000, glyphs)];
    }
}
