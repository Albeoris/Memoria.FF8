using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleAxisAngle(int Angle, NativeBattleVector Axis);

/// <summary>571480/5714F0's normalized cross-axis, FPATAN angle and staged Q12 rotation WORDs.</summary>
public static class NativeBattleModuleAlignment
{
    public static NativeBattleModuleAxisAngle Between571480(int firstX, int firstY, int firstZ, int secondX, int secondY, int secondZ)
    {
        int dot = unchecked(firstZ * secondZ + firstY * secondY + firstX * secondX) >> 12;
        int x = unchecked(firstY * secondZ - secondY * firstZ) >> 12;
        int y = unchecked(firstZ * secondX - firstX * secondZ) >> 12;
        int z = unchecked(firstX * secondY - secondX * firstY) >> 12;
        int squared = unchecked(x * x + y * y + z * z) >> 12;
        if (squared < 0)
        {
            throw new NotSupportedException("Native571480 overflow reaches an unsupported negative56BED0 square root.");
        }
        int length = unchecked((int)(Math.Sqrt(squared * (double)BitConverter.Int32BitsToSingle(0x39800000)) * 4096f));
        //56D160 loads length before dot;FPATAN is atan2(length,dot), scaled by the exact floatB6B984.
        int angle = NativePrecision53.RoundInteger(Math.Atan2(length, dot) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        var axis = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        return new(angle, new(unchecked((short)axis.X), unchecked((short)axis.Y), unchecked((short)axis.Z)));
    }

    public static ImmutableArray<short> DownToVelocity573C40(short x, short y, short z)
    {
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        NativeBattleModuleAxisAngle alignment = Between571480(0, -4096, 0, direction.X, direction.Y, direction.Z);
        return Rotation5714F0(alignment.Angle, alignment.Axis);
    }

    public static ImmutableArray<short> Rotation5714F0(int angle, NativeBattleVector axis)
    {
        int sine = NativeBattleCameraMath.Sine(angle);
        int cosine = NativeBattleCameraMath.Cosine(angle);
        int remaining = unchecked(4096 - cosine);
        int xx = Product(axis.X, axis.X);
        int yy = Product(axis.Y, axis.Y);
        int zz = Product(axis.Z, axis.Z);
        int xy = Product(Product(axis.X, axis.Y), remaining);
        int xz = Product(Product(axis.X, axis.Z), remaining);
        int yz = Product(Product(axis.Y, axis.Z), remaining);
        int xs = Product(axis.X, sine);
        int ys = Product(axis.Y, sine);
        int zs = Product(axis.Z, sine);
        return
        [
            Word(unchecked(Product(4096 - xx, cosine) + xx)), Word(unchecked(xy - zs)), Word(unchecked(xz + ys)),
            Word(unchecked(xy + zs)), Word(unchecked(Product(4096 - yy, cosine) + yy)), Word(unchecked(yz - xs)),
            Word(unchecked(xz - ys)), Word(unchecked(yz + xs)), Word(unchecked(Product(4096 - zz, cosine) + zz))
        ];
    }

    private static int Product(int first, int second) => unchecked(first * second) >> 12;
    private static short Word(int value) => unchecked((short)value);
}

public static partial class NativeBattlePoseMatrices
{
    /// <summary>Expose the current56BEF0 column-scaling implementation without another Q12 loop.</summary>
    public static ImmutableArray<short> ScaleRotation56BEF0(ImmutableArray<short> rotation, NativeBattleVector scale)
    {
        new NativeBattleActionMatrix(rotation, 0, 0, 0, 4096).Validate();
        return Scale(new(rotation.ToArray()), [scale.X, scale.Y, scale.Z]).Rotation.ToImmutableArray();
    }
}
