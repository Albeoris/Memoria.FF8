using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicRibbonBasisResult(NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix, bool Flip);

public static class NativeBattleCinematicRibbonBasis
{
    public static NativeBattleCinematicRibbonBasisResult Prepare(NativeBattleWaterExecutionFrame frame, NativeBattleVector from, NativeBattleVector to, bool publishRotation = true)
    {
        int x = to.X - from.X;
        int y = to.Y - from.Y;
        int z = to.Z - from.Z;
        frame = VectorDwords(frame, 0xF0, x, y, z);
        (frame, int yaw) = Angle(frame, from.X, from.Z, to.X, to.Z);
        if (((yaw + 1024) & 4095) < 2048)
        {
            y = unchecked(-y);
            frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)y));
        }
        z = unchecked(-z);
        frame = frame.WriteTemporaryDword(0xF8, unchecked((uint)z));
        (frame, int pitch) = Angle(frame, from.Y, from.Z, to.Y, to.Z);
        bool flip = pitch > 1024 && pitch < 3072;
        while (Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || Math.Abs(z) > 10000)
        {
            x = x / 2 + (x & 1);
            y = y / 2 + (y & 1);
            z = z / 2 + (z & 1);
        }
        frame = VectorDwords(frame, 0xF0, x, y, z);
        var forward = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        frame = VectorDwords(frame, 0xF0, forward.X, forward.Y, forward.Z);
        frame = VectorDwords(frame, 0xD0, forward.Z, 0, unchecked(-forward.X));
        var right = NativeBattleActionGeometryRules.NormalizeVector56BC50(forward.Z, 0, unchecked(-forward.X));
        frame = VectorDwords(frame, 0xD0, right.X, right.Y, right.Z);
        int upX = unchecked(forward.Y * right.Z - forward.Z * right.Y) >> 12;
        int upY = unchecked(forward.Z * right.X - forward.X * right.Z) >> 12;
        int upZ = unchecked(forward.X * right.Y - forward.Y * right.X) >> 12;
        frame = VectorDwords(frame, 0xE0, upX, upY, upZ);
        var up = NativeBattleActionGeometryRules.NormalizeVector56BC50(upX, upY, upZ);
        frame = VectorDwords(frame, 0xE0, up.X, up.Y, up.Z);
        short[] rotation = [unchecked((short)right.X), unchecked((short)right.Y), unchecked((short)right.Z), unchecked((short)up.X), unchecked((short)up.Y), unchecked((short)up.Z), unchecked((short)forward.X), unchecked((short)forward.Y), unchecked((short)forward.Z)];
        if (publishRotation)
        {
            for (int index = 0; index < rotation.Length; index++)
            {
                frame = frame.WriteTemporaryWord(0xA0 + index * 2, unchecked((ushort)rotation[index]));
            }
        }
        return new(frame, new(rotation.ToImmutableArray(), 0, 0, 0, 4096), flip);
    }

    public static (NativeBattleWaterExecutionFrame Frame, int Angle) Angle(NativeBattleWaterExecutionFrame frame, int startX, int startZ, int endX, int endZ)
    {
        int x = unchecked(endX - startX);
        int z = unchecked(endZ - startZ);
        int distance = Distance(x, 0, z);
        frame = frame.WriteTemporaryDword(0xFC, unchecked((uint)distance));
        return (frame, NativeWorldScriptedPathMath.NativeAngle(x, z) & 4095);
    }

    public static int Distance(int x, int y, int z)
    {
        int sum = unchecked(x * x + y * y + z * z);
        double root = Math.Sqrt(sum);
        return !double.IsFinite(root) || root >= 2147483648d ? int.MinValue : (int)root;
    }

    public static int Turn(int current, int target, int step)
    {
        current &= 4095;
        target &= 4095;
        int adjusted = current > target ? target + 4096 : target;
        if (adjusted - current < 2048)
        {
            current = unchecked(current + step) & 4095;
            adjusted = current > target ? target + 4096 : target;
            if (adjusted - current >= 2048)
            {
                current = adjusted & 4095;
            }
        }
        else
        {
            current = unchecked(current - step) & 4095;
            adjusted = current > target ? target + 4096 : target;
            if (adjusted - current < 2048)
            {
                current = adjusted;
            }
        }
        return current & 4095;
    }

    private static NativeBattleWaterExecutionFrame VectorDwords(NativeBattleWaterExecutionFrame frame, int offset, int x, int y, int z)
        => frame.WriteTemporaryDword(offset, unchecked((uint)x)).WriteTemporaryDword(offset + 4, unchecked((uint)y)).WriteTemporaryDword(offset + 8, unchecked((uint)z));
}
