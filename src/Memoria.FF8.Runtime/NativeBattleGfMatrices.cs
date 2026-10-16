using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>701310's axis conventions and the original56C090/56C3D0 matrix operations.</summary>
public static class NativeBattleGfMatrices
{
    public static NativeBattleActionMatrix Euler701310(NativeBattleVector angles)
    {
        short sx = unchecked((short)NativeBattleCameraMath.Sine(angles.X));
        short cx = unchecked((short)NativeBattleCameraMath.Cosine(angles.X));
        short sy = unchecked((short)NativeBattleCameraMath.Sine(-(int)angles.Y));
        short cy = unchecked((short)NativeBattleCameraMath.Cosine(-(int)angles.Y));
        short sz = unchecked((short)NativeBattleCameraMath.Sine(angles.Z));
        short cz = unchecked((short)NativeBattleCameraMath.Cosine(angles.Z));
        NativeBattleActionMatrix x = new([4096, 0, 0, 0, cx, sx, 0, unchecked((short)-sx), cx], 0, 0, 0, 4096);
        NativeBattleActionMatrix y = new([cy, 0, unchecked((short)-sy), 0, 4096, 0, sy, 0, cy], 0, 0, 0, 4096);
        NativeBattleActionMatrix z = new([cz, sz, 0, unchecked((short)-sz), cz, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return Multiply56C090(Multiply56C090(z, x), y);
    }

    public static NativeBattleActionMatrix Multiply56C090(NativeBattleActionMatrix left, NativeBattleActionMatrix right)
    {
        left.Validate();
        right.Validate();
        short[] words = new short[9];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                double value = right.Rotation[column] * (left.Rotation[row * 3] / 4096d) + right.Rotation[3 + column] * (left.Rotation[row * 3 + 1] / 4096d) + right.Rotation[6 + column] * (left.Rotation[row * 3 + 2] / 4096d);
                words[row * 3 + column] = unchecked((short)NativePrecision53.RoundInteger(value));
            }
        }
        return new(words.ToImmutableArray(), 0, 0, 0, 4096);
    }

    public static (int X, int Y, int Z) Rotate56C3D0(NativeBattleActionMatrix matrix, NativeBattleVector point)
    {
        matrix.Validate();
        int Row(int row) => NativePrecision53.RoundInteger(matrix.Rotation[row * 3 + 2] * (point.Z / 4096d) + matrix.Rotation[row * 3 + 1] * (point.Y / 4096d) + matrix.Rotation[row * 3] * (point.X / 4096d));
        return (Row(0), Row(1), Row(2));
    }

    public static NativeBattleActionMatrix Compose6C6060(NativeBattleGfAnimationNode node, NativeBattleActionMatrix view, NativeBattleActionMatrix? billboard, NativeBattleVector basePosition)
    {
        if ((node.Header & 0x40000) != 0)
        {
            throw new NotSupportedException("Native6C6060 quaternion node requires its original7015B0 rotation contract.");
        }
        NativeBattleActionMatrix basis = (node.Header & 0x1000) != 0 ? billboard ?? throw new NotSupportedException("Native6C6060 billboard node requires its executed67AAE0 matrix.") : view;
        NativeBattleActionMatrix rotation = Euler701310(node.Vector16);
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                rotation = Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096));
            }
            else
            {
                rotation = rotation with { Rotation = rotation.Rotation.Select((word, index) => unchecked((short)(word * (index / 3 == 0 ? scale.X : index / 3 == 1 ? scale.Y : scale.Z) >> 12))).ToImmutableArray() };
            }
        }
        if ((node.Header & 0x8200) == 0)
        {
            rotation = Multiply56C090(basis, rotation);
        }
        (int x, int y, int z) = (node.Header & 0x200) != 0 ? (node.Vector8.X, node.Vector8.Y, node.Vector8.Z) : Rotate56C3D0(basis, node.Vector8);
        (int baseX, int baseY, int baseZ) = Rotate56C3D0(view, basePosition);
        return rotation with { X = unchecked(baseX + basis.X + x), Y = unchecked(baseY + basis.Y + y), Z = unchecked(baseZ + basis.Z + z) };
    }
}
