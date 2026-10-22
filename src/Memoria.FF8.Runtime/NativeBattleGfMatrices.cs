using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleFullMacVertex(int X, int Y, int Z, uint Flags);

/// <summary>701310's axis conventions and the original56C090/56C3D0 matrix operations.</summary>
public static class NativeBattleGfMatrices
{
    public static NativeBattleActionMatrix Normalize67AAE0(NativeBattleActionMatrix source)
    {
        source.Validate();
        var column1 = NativeBattleActionGeometryRules.NormalizeVector56BC50(source.Rotation[1], source.Rotation[4], source.Rotation[7]);
        var column0 = NativeBattleActionGeometryRules.NormalizeVector56BC50(column1.Y, -column1.X, 0);
        int x = unchecked(column0.Y * column1.Z - column0.Z * column1.Y) >> 12;
        int y = unchecked(column0.Z * column1.X - column0.X * column1.Z) >> 12;
        int z = unchecked(column0.X * column1.Y - column0.Y * column1.X) >> 12;
        var column2 = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        return source with { Rotation = [unchecked((short)column0.X), unchecked((short)column1.X), unchecked((short)column2.X), unchecked((short)column0.Y), unchecked((short)column1.Y), unchecked((short)column2.Y), unchecked((short)column0.Z), unchecked((short)column1.Z), unchecked((short)column2.Z)] };
    }

    public static NativeBattleActionMatrix Euler701310(NativeBattleVector angles)
    {
        (NativeBattleActionMatrix x, NativeBattleActionMatrix y, NativeBattleActionMatrix z) = AxisMatrices(angles);
        return Multiply56C090(Multiply56C090(z, x), y);
    }

    public static NativeBattleActionMatrix Euler701430(NativeBattleVector angles)
    {
        (NativeBattleActionMatrix x, NativeBattleActionMatrix y, NativeBattleActionMatrix z) = AxisMatrices(angles);
        return Multiply56C090(Multiply56C090(z, y), x);
    }

    public static NativeBattleActionMatrix Euler7015B0(NativeBattleVector angles)
    {
        (NativeBattleActionMatrix x, NativeBattleActionMatrix y, NativeBattleActionMatrix z) = AxisMatrices(angles);
        return Multiply56C090(Multiply56C090(y, x), z);
    }

    public static NativeBattleActionMatrix Euler701630(NativeBattleVector angles)
    {
        (NativeBattleActionMatrix x, NativeBattleActionMatrix y, NativeBattleActionMatrix z) = AxisMatrices(angles);
        return Multiply56C090(Multiply56C090(x, y), z);
    }

    /// <summary>56CE30 left-multiplies Z, then X, then Y; each56C090 product rounds before the next operation.</summary>
    public static NativeBattleActionMatrix Euler56CE30(NativeBattleVector angles)
    {
        (NativeBattleActionMatrix x, NativeBattleActionMatrix y, NativeBattleActionMatrix z) = AxisMatrices(angles, true);
        return Multiply56C090(y, Multiply56C090(x, z));
    }

    /// <summary>56CD50 left-multiplies Z, then Y, then X; each56C090 product rounds before the next operation.</summary>
    public static NativeBattleActionMatrix Euler56CD50(NativeBattleVector angles)
    {
        (NativeBattleActionMatrix x, NativeBattleActionMatrix y, NativeBattleActionMatrix z) = AxisMatrices(angles, true);
        return Multiply56C090(x, Multiply56C090(y, z));
    }

    /// <summary>56D020/56C270 accepts a full DWORD angle and replaces only the existing matrix's rotation, preserving its translation.</summary>
    public static NativeBattleActionMatrix RotateY56D020(NativeBattleActionMatrix matrix, int angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        NativeBattleActionMatrix axis = new([cosine, 0, sine, 0, 4096, 0, unchecked((short)-sine), 0, cosine], 0, 0, 0, 4096);
        return matrix with { Rotation = Multiply56C090(axis, matrix).Rotation };
    }

    public static NativeBattleActionMatrix RotateX56CFB0(NativeBattleActionMatrix matrix, int angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        NativeBattleActionMatrix axis = new([4096, 0, 0, 0, cosine, unchecked((short)-sine), 0, sine, cosine], 0, 0, 0, 4096);
        return matrix with { Rotation = Multiply56C090(axis, matrix).Rotation };
    }

    public static NativeBattleActionMatrix RotateZ56D090(NativeBattleActionMatrix matrix, int angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        NativeBattleActionMatrix axis = new([cosine, unchecked((short)-sine), 0, sine, cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return matrix with { Rotation = Multiply56C090(axis, matrix).Rotation };
    }

    private static (NativeBattleActionMatrix X, NativeBattleActionMatrix Y, NativeBattleActionMatrix Z) AxisMatrices(NativeBattleVector angles, bool native56CD50 = false)
    {
        short sx = unchecked((short)NativeBattleCameraMath.Sine(angles.X));
        short cx = unchecked((short)NativeBattleCameraMath.Cosine(angles.X));
        int yaw = native56CD50 ? angles.Y : -(int)angles.Y;
        short sy = unchecked((short)NativeBattleCameraMath.Sine(yaw));
        short cy = unchecked((short)NativeBattleCameraMath.Cosine(yaw));
        short sz = unchecked((short)NativeBattleCameraMath.Sine(angles.Z));
        short cz = unchecked((short)NativeBattleCameraMath.Cosine(angles.Z));
        short xSine = native56CD50 ? unchecked((short)-sx) : sx;
        short ySine = native56CD50 ? unchecked((short)-sy) : sy;
        short zSine = native56CD50 ? unchecked((short)-sz) : sz;
        NativeBattleActionMatrix x = new([4096, 0, 0, 0, cx, xSine, 0, unchecked((short)-xSine), cx], 0, 0, 0, 4096);
        NativeBattleActionMatrix y = new([cy, 0, unchecked((short)-ySine), 0, 4096, 0, ySine, 0, cy], 0, 0, 0, 4096);
        NativeBattleActionMatrix z = new([cz, zSine, 0, unchecked((short)-zSine), cz, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return (x, y, z);
    }

    public static NativeBattleActionMatrix Compose56C2F0(NativeBattleActionMatrix left, NativeBattleActionMatrix right)
    {
        NativeBattleActionMatrix composed = Multiply56C090(left, right);
        (int x, int y, int z) = RotateLong56C600(left, right.X, right.Y, right.Z);
        return composed with { X = unchecked(x + left.X), Y = unchecked(y + left.Y), Z = unchecked(z + left.Z) };
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

    /// <summary>56C4F0 stores the low WORD of each56C3D0 dot product, without translation or signed IR saturation.</summary>
    public static NativeBattleVector RotateShort56C4F0(NativeBattleActionMatrix matrix, NativeBattleVector point)
    {
        (int x, int y, int z) = Rotate56C3D0(matrix, point);
        return new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
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
        NativeBattleFullMacVertex mac = TransformMac460840(matrix, vector);
        return new((short)Math.Clamp(mac.X, short.MinValue, short.MaxValue), (short)Math.Clamp(mac.Y, short.MinValue, short.MaxValue), (short)Math.Clamp(mac.Z, short.MinValue, short.MaxValue));
    }

    public static NativeBattleFullMacVertex TransformMac460840(NativeBattleActionMatrix matrix, NativeBattleVector vector)
    {
        (int x, int y, int z) = Rotate56C3D0(matrix, vector);
        x = unchecked(x + matrix.X);
        y = unchecked(y + matrix.Y);
        z = unchecked(z + matrix.Z);
        uint flags = 0;
        if (x is < short.MinValue or > short.MaxValue)
        {
            flags |= 0x81000000;
        }
        if (y is < short.MinValue or > short.MaxValue)
        {
            flags |= 0x80800000;
        }
        if (z is < short.MinValue or > short.MaxValue)
        {
            flags |= 0x00400000;
        }
        return new(x, y, z, flags);
    }
    public static NativeBattleActionMatrix Compose6C6060(NativeBattleGfAnimationNode node, NativeBattleActionMatrix view, NativeBattleActionMatrix? billboard, NativeBattleVector basePosition)
    {
        NativeBattleActionMatrix basis = (node.Header & 0x1000) != 0 ? billboard ?? throw new NotSupportedException("Native6C6060 billboard node requires its executed67AAE0 matrix.") : view;
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? Euler7015B0(node.Vector16) : Euler701310(node.Vector16);
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                rotation = Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096));
            }
            else
            {
                short[] scaled = rotation.Rotation.ToArray();
                for (int row = 0; row < 3; row++)
                {
                    int factor = row switch { 0 => scale.X, 1 => scale.Y, _ => scale.Z };
                    for (int column = 0; column < 3; column++)
                    {
                        int index = row * 3 + column;
                        scaled[index] = unchecked((short)(unchecked(scaled[index] * factor) >> 12));
                    }
                }
                rotation = rotation with { Rotation = scaled.ToImmutableArray() };
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

    public static NativeBattleActionMatrix LaterWindowContext6C64C0(NativeBattleActionMatrix view, int pitch, int yaw, NativeBattleVector target)
    {
        NativeBattleActionMatrix local = Multiply56C090(Euler701310(new(0, unchecked((short)yaw), 0)), Euler701310(new(unchecked((short)pitch), 0, 0)));
        NativeBattleActionMatrix rotation = Multiply56C090(view, local);
        (int x, int y, int z) = RotateLong56C600(view, target.X, target.Y, target.Z);
        return rotation with { X = unchecked(view.X + x), Y = unchecked(view.Y + y), Z = unchecked(view.Z + z) };
    }

    public static NativeBattleActionMatrix Compose6C53A0(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, NativeBattleActionMatrix? billboard)
    {
        bool billboardNode = (node.Header & 0x1000) != 0;
        bool direct = !billboardNode && (node.Header & 0x200) != 0;
        NativeBattleActionMatrix basis = billboardNode ? billboard ?? throw new NotSupportedException("Native6C53A0 billboard node requires its executed67AAE0 matrix.") : context;
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? Euler7015B0(node.Vector16) : Euler701310(node.Vector16);
        if (!direct && (node.Header & 0x8000) == 0)
        {
            rotation = Multiply56C090(basis, rotation);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0
                ? Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        (int x, int y, int z) = direct ? (node.Vector8.X, node.Vector8.Y, node.Vector8.Z) : Rotate56C3D0(basis, node.Vector8);
        return rotation with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
    }
}
