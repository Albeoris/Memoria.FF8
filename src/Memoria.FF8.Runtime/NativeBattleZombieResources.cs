using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleZombieResources(NativeBattleEffectHeapAllocation Animation, NativeBattleGfAnimationSource Layout, ImmutableArray<short> Wave, NativeBattleVector Zero, uint Black, uint Sound);
public sealed record NativeBattleZombieDraw(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleZombieDrawing
{
    public static NativeBattleActionMatrix Euler701530(NativeBattleVector angles)
    {
        NativeBattleActionMatrix x = NativeBattleGfMatrices.Euler701310(new(angles.X, 0, 0));
        NativeBattleActionMatrix y = NativeBattleGfMatrices.Euler701310(new(0, angles.Y, 0));
        NativeBattleActionMatrix z = NativeBattleGfMatrices.Euler701310(new(0, 0, angles.Z));
        return NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(x, z), y);
    }

    public static NativeBattleActionMatrix Matrix(NativeBattleGfAnimationNode node, NativeBattleVector anchor, int reach, NativeBattleActionMatrix camera)
    {
        NativeBattleActionMatrix matrix = (node.Header & 0x20000) != 0 ? Euler701530(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = new(unchecked((short)(unchecked(node.Vector8.X * reach) >> 12)), unchecked((short)(unchecked(node.Vector8.Y * reach) >> 12)), unchecked((short)(unchecked(node.Vector8.Z * reach) >> 12)));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, reach, reach, reach);
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(camera, anchor);
            x = unchecked(x + offset.X);
            y = unchecked(y + offset.Y);
            z = unchecked(z + offset.Z);
        }
        else
        {
            NativeBattleVector position = new(unchecked((short)(offset.X + anchor.X)), unchecked((short)(offset.Y + anchor.Y)), unchecked((short)(offset.Z + anchor.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(camera, position);
            matrix = NativeBattleGfMatrices.Multiply56C090(camera, matrix);
        }
        matrix = matrix with { X = unchecked(x + camera.X), Y = unchecked(y + camera.Y), Z = unchecked(z + camera.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0
                ? matrix with { Rotation = NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation }
                : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix;
    }

    public static (short Lift, int Weight, int Amplitude, short Phase) OverlayParameters(int age, int reach)
    {
        if ((uint)(age - 30) >= 30)
        {
            throw new ArgumentOutOfRangeException(nameof(age));
        }
        int elapsed = age - 30;
        uint angle = (uint)(((ulong)((uint)elapsed << 12) * 0x88888889u) >> 32) >> 4;
        int sine = NativeBattleCameraMath.Sine((int)angle >> 2);
        return (unchecked((short)(unchecked(-sine * reach) >> 13)), sine, unchecked((int)(angle * 2 + 0x200)), unchecked((short)(elapsed * 8)));
    }
}
