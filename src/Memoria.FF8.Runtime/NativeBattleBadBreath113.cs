using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath113Resources(NativeBattleGfAnimationSource Cloud, ImmutableArray<uint> Flags, ImmutableArray<NativeBattleModuleResource> Sprites, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<short> Wave);

public static class NativeBattleBadBreath113
{
    public static NativeBattleVector Direction6CBB40(short pitch, short yaw, short roll)
    {
        int c0 = NativeBattleCameraMath.Cosine(pitch);
        int s0 = -NativeBattleCameraMath.Sine(pitch);
        int t = unchecked(-NativeBattleCameraMath.Sine(roll) * s0) >> 12;
        int y = unchecked(NativeBattleCameraMath.Cosine(roll) * s0) >> 12;
        int s1 = -NativeBattleCameraMath.Sine(yaw);
        int c1 = -NativeBattleCameraMath.Cosine(yaw);
        return new(unchecked((short)(unchecked(c1 * t + s1 * c0) >> 12)), unchecked((short)y), unchecked((short)(unchecked(c1 * c0 - s1 * t) >> 12)));
    }

    public static NativeBattleProjectedVertex Sway6CB7D0(NativeBattleModuleProjectedVertex projected, short ir2, short originX, short amplitude, short phase, ImmutableArray<short> wave, NativeBattleProjectedVertex retained)
    {
        if (projected.Depth == 0)
        {
            return retained with { DepthAndClip = (retained.DepthAndClip & 0xFFFF) | 0x100000 };
        }
        short x = unchecked((short)projected.Screen);
        short y = unchecked((short)(projected.Screen >> 16));
        int index = unchecked(phase + ir2 * 4) & 4095;
        int delta = unchecked(wave[index] * amplitude) >> 12;
        delta = unchecked(delta * (x - originX)) >> 11;
        x = unchecked((short)(x + delta));
        uint clip = x < 0 ? 1u : x >= 2560 ? 2u : 0u;
        clip |= y < 0 ? 4u : y >= 1728 ? 8u : 0u;
        return new((ushort)x | unchecked((uint)y << 16), projected.Depth | clip << 16);
    }

    public static int RiseHeight(int random, short height)
    {
        int product = unchecked(random * height);
        int quotient = (int)(((long)product * 0x57619F1) >> 32) >> 9;
        return unchecked(quotient + (int)((uint)quotient >> 31));
    }
}
