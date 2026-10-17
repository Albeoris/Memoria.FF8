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
        return RotateLong56C600(matrix, point.X, point.Y, point.Z);
    }

    /// <summary>56C600 uses DWORD vector components with the same three biased-rounded dot products as56C3D0; it ignores translation and writes XYZ only.</summary>
    public static (int X, int Y, int Z) RotateLong56C600(NativeBattleActionMatrix matrix, int x, int y, int z)
    {
        matrix.Validate();
        int Row(int row) => NativePrecision53.RoundInteger(matrix.Rotation[row * 3 + 2] * (z / 4096d) + matrix.Rotation[row * 3 + 1] * (y / 4096d) + matrix.Rotation[row * 3] * (x / 4096d));
        return (Row(0), Row(1), Row(2));
    }

    /// <summary>460840 followed by45E3E0: rounded rotation, DWORD translation and saturated signed IR WORDs.</summary>
    public static NativeBattleVector Transform460840(NativeBattleActionMatrix matrix, NativeBattleVector vector)
    {
        (int x, int y, int z) = Rotate56C3D0(matrix, vector);
        return new((short)Math.Clamp(unchecked(x + matrix.X), short.MinValue, short.MaxValue), (short)Math.Clamp(unchecked(y + matrix.Y), short.MinValue, short.MaxValue), (short)Math.Clamp(unchecked(z + matrix.Z), short.MinValue, short.MaxValue));
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

    public static NativeBattleActionMatrix SecondWindowContext6C3C47(NativeBattleActionMatrix view, int targetZ1778)
    {
        NativeBattleActionMatrix local = Euler701310(new(0, -2048, 0));
        NativeBattleActionMatrix rotation = Multiply56C090(view, local);
        int Row(int row) => NativePrecision53.RoundInteger(view.Rotation[row * 3 + 2] * (targetZ1778 / 4096d) + view.Rotation[row * 3] * (500 / 4096d));
        return rotation with { X = unchecked(Row(0) + view.X), Y = unchecked(Row(1) + view.Y), Z = unchecked(Row(2) + view.Z) };
    }

    public static NativeBattleActionMatrix Compose6C53A0(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, NativeBattleActionMatrix? billboard)
    {
        if ((node.Header & 0x40000) != 0)
        {
            throw new NotSupportedException("Native6C53A0 quaternion node requires its original7015B0 rotation contract.");
        }
        bool billboardNode = (node.Header & 0x1000) != 0;
        bool direct = !billboardNode && (node.Header & 0x200) != 0;
        NativeBattleActionMatrix basis = billboardNode ? billboard ?? throw new NotSupportedException("Native6C53A0 billboard node requires its executed67AAE0 matrix.") : context;
        NativeBattleActionMatrix rotation = Euler701310(node.Vector16);
        if (!direct && (node.Header & 0x8000) == 0)
        {
            rotation = Multiply56C090(basis, rotation);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0
                ? Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : rotation with { Rotation = rotation.Rotation.Select((word, index) => unchecked((short)(word * (index / 3 == 0 ? scale.X : index / 3 == 1 ? scale.Y : scale.Z) >> 12))).ToImmutableArray() };
        }
        (int x, int y, int z) = direct ? (node.Vector8.X, node.Vector8.Y, node.Vector8.Z) : Rotate56C3D0(basis, node.Vector8);
        return rotation with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
    }
}
