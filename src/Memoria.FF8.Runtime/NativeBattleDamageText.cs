using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDamageTextAnchor(short X, short Y);
public sealed record NativeBattleDamageTextDraw(short X, short Y, uint Color, ImmutableArray<byte> Glyphs);

public static class NativeBattleDamageText
{
    public static NativeBattleDamageTextAnchor Project5069B0(uint screen)
    {
        return new((short)Math.Clamp(unchecked((short)screen) >> 3, 32, 288), (short)Math.Clamp(unchecked((short)(screen >> 16)) >> 3, 32, 152));
    }

    public static NativeBattleDamageTextDraw Layout5069B0(NativeBattleEffectTask task)
    {
        NativeBattleDamageTextAnchor anchor = task.TextAnchor ?? throw new NotSupportedException("Native5069B0 lacks its retained first callback screen position; migrate the standalone snapshot explicitly.");
        if (task.Kind != NativeBattleEffectKind.DamageText5069B0 || task.Frame >= 10)
        {
            throw new InvalidDataException("Native5069B0 requires one of its ten original callback frames.");
        }
        ReadOnlySpan<sbyte> damage = [-12, -16, -17, -17, -16, -12, -13, -13, -12, -12];
        ReadOnlySpan<sbyte> healing = [-12, -13, -14, -15, -16, -17, -18, -19, -20, -21];
        int y = (task.Color == 0x408040 ? healing : damage)[task.Frame];
        uint color = task.Color;
        if (task.Frame >= 8)
        {
            //DPCS with IR0=0 retains RGB on frame8; frame9 uses IR0=2048 toward black.
            if (task.Frame == 9)
            {
                color = color >> 1 & 0x7F7F7F;
            }
            color |= 0x04000000u;
        }
        return new(unchecked((short)(anchor.X - task.HalfWidth)), unchecked((short)(anchor.Y + y)), color, task.Glyphs);
    }
}
