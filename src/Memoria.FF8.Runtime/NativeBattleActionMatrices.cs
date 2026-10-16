using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native32-byte matrix plus its separately retained signed scaleWORD.</summary>
public sealed record NativeBattleActionMatrix([property: JsonRequired] ImmutableArray<short> Rotation, [property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Z, [property: JsonRequired] short Scale)
{
    public void Validate()
    {
        if (Rotation.IsDefault || Rotation.Length != 9)
        {
            throw new InvalidDataException("Native camera matrix requires its nine original rotation WORDs.");
        }
    }
}

public static partial class NativeBattleActionGeometryRules
{
    /// <summary>5020A0 uses the midpoint of target X/Z bounds, with Y cleared by its DWORD store.</summary>
    public static NativeBattleVector TargetCenter(IReadOnlyList<NativeBattleVector> positions)
    {
        if (positions.Count == 0)
        {
            throw new InvalidDataException("Native target bounds require a nonempty physical target mask.");
        }
        return new(unchecked((short)((positions.Min(point => point.X) + positions.Max(point => point.X)) / 2)), 0, unchecked((short)((positions.Min(point => point.Z) + positions.Max(point => point.Z)) / 2)));
    }

    /// <summary>50D6B0 captures a transposed50CBA0 basis, midpoint and signed distance scale.</summary>
    public static NativeBattleActionMatrix CaptureMatrix(NativeBattleVector first, NativeBattleVector second, NativeBattleVector fallbackRotation)
    {
        var (x, y, z, _) = ReducedDifference(first, second);
        short[] rotation;
        short scale;
        if ((x | y | z) == 0)
        {
            rotation = NativeBattlePoseMatrices.RotationWords(fallbackRotation).ToArray();
            scale = 4096;
        }
        else
        {
            var forward = Normalize(x, y, z);
            var right = Normalize(forward.Z, 0, -forward.X);
            int upX = unchecked(forward.Y * right.Z - forward.Z * right.Y) >> 12;
            int upY = unchecked(forward.Z * right.X - forward.X * right.Z) >> 12;
            int upZ = unchecked(forward.X * right.Y - forward.Y * right.X) >> 12;
            var up = Normalize(upX, upY, upZ);
            //56C040 transposes the basis published as three rows by50CBA0.
            rotation = [unchecked((short)right.X), unchecked((short)up.X), unchecked((short)forward.X), unchecked((short)right.Y), unchecked((short)up.Y), unchecked((short)forward.Y), unchecked((short)right.Z), unchecked((short)up.Z), unchecked((short)forward.Z)];
            scale = DistanceScale(first, second);
        }
        return new(rotation.ToImmutableArray(), (first.X + second.X) / 2, (first.Y + second.Y) / 2, (first.Z + second.Z) / 2, scale);
    }

    private static (int X, int Y, int Z, int Factor) ReducedDifference(NativeBattleVector first, NativeBattleVector second)
    {
        int x = second.X - first.X;
        int y = second.Y - first.Y;
        int z = second.Z - first.Z;
        int factor = 1;
        while (Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || Math.Abs(z) > 10000)
        {
            x = x / 2 + (x & 1);
            y = y / 2 + (y & 1);
            z = z / 2 + (z & 1);
            factor *= 2;
        }
        return (x, y, z, factor);
    }

    private static (int X, int Y, int Z) Normalize(int x, int y, int z)
    {
        //56BC50 retains X on the FPU stack while Y/Z are stored as floats before biased conversion.
        double length = Math.Sqrt((double)z * (float)z + (double)(float)y * (float)y + (double)x * x);
        double factor = length == 0 ? 1 : 4096d / length;
        return (Round(x * factor), Round((float)(y * factor)), Round((float)(z * factor)));
    }

    private static int Round(double value) => unchecked((int)((uint)BitConverter.DoubleToInt64Bits(value + 4503601774854144d) + 0x80000000u));
}
