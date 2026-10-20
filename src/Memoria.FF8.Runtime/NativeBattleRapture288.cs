using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRapture288Resources(NativeBattleGfAnimationSource First, NativeBattleGfAnimationSource Second, NativeBattleModuleResource Spark, NativeBattleStaticMeshResource Debris, NativeBattleEffectHeapAllocation Camera, uint Sound, NativeBattleVector DepthVector, ImmutableArray<uint> Colors);

public static class NativeBattleRapture288Geometry
{
    public static short TargetY(int age, short original, int scale, bool resisted)
    {
        if ((uint)(age - 23) >= 40)
        {
            throw new ArgumentOutOfRangeException(nameof(age));
        }
        int elapsed = age - 23;
        int angle = (elapsed << 13) / 40;
        int cosine = NativeBattleCameraMath.Cosine(angle);
        if (resisted)
        {
            return unchecked((short)(original + (unchecked(scale * (cosine - 4096)) >> 14)));
        }
        int first = NativeBattleCameraMath.Cosine((elapsed << 13) / 160);
        int second = 4096 - NativeBattleCameraMath.Cosine((4096 - first) >> 2);
        return unchecked((short)(original + (unchecked(scale * (cosine - 4096)) >> 12) - (unchecked(scale * second) >> 10)));
    }

    public static (int Strength, uint Color) Flash(int age, ImmutableArray<uint> colors)
    {
        uint elapsed = unchecked((uint)(age - 1));
        if (elapsed >= 74)
        {
            return (0, 0);
        }
        int strength = elapsed < 12 ? (int)(3072 * elapsed / 12) : elapsed < 62 ? 3072 : (int)((227328 - 3072 * elapsed) / 12);
        int position = (int)(20480 * elapsed / 74);
        int index = position >> 12;
        if (colors.Length != 6)
        {
            throw new InvalidDataException("Native Rapture requires its six original stage colors.");
        }
        int fraction = position & 4095;
        return (strength, NativeBattleDuelSharedPackets.InterpolateColor56CC00(colors[index], colors[index + 1], 4096 - fraction, fraction, 0));
    }

    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, int scale)
    {
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = new(unchecked((short)(unchecked(node.Vector8.X * scale) >> 12)), unchecked((short)(unchecked(node.Vector8.Y * scale) >> 12)), unchecked((short)(unchecked(node.Vector8.Z * scale) >> 12)));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = (offset.X, offset.Y, offset.Z);
        }
        else
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(context, offset);
            if ((node.Header & 0x8000) == 0)
            {
                matrix = NativeBattleGfMatrices.Multiply56C090(context, matrix);
            }
        }
        NativeBattleVector extra = node.Vector24;
        if (extra is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, extra.X, extra.Y, extra.Z);
        }
        return matrix with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
    }
}
