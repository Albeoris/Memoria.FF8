using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Controller
{
    public static NativeBattleEffect345Frame Identity8DD770(NativeBattleEffect345Frame frame, uint address) => WriteBytes(frame, address, IdentityMatrix(ReadBytes(frame, address, 32)), 32);

    public static NativeBattleEffect345Frame Rotate8DD(NativeBattleEffect345Frame frame, uint address, int axis, short angle)
    {
        (frame, NativeBattleEffectArena matrix) = RotateMatrix(frame, ReadBytes(frame, address, 32), axis, angle);
        return WriteBytes(frame, address, matrix, 32);
    }

    public static NativeBattleEffect345Frame MultiplyRotation(NativeBattleEffect345Frame frame, uint left, uint destination)
    {
        (frame, NativeBattleEffectArena matrix) = MultiplyMatrix(frame, Packed(ReadBytes(frame, left, 32)), ReadBytes(frame, destination, 32));
        return WriteBytes(frame, destination, matrix, 32);
    }

    public static NativeBattleEffectArena IdentityMatrix(NativeBattleEffectArena matrix)
    {
        matrix = matrix.Write(0, new byte[18]).Write(20, new byte[12]);
        return matrix.Write(0, BitConverter.GetBytes((short)4096)).Write(8, BitConverter.GetBytes((short)4096)).Write(16, BitConverter.GetBytes((short)4096));
    }

    public static (NativeBattleEffect345Frame Frame, NativeBattleEffectArena Matrix) RotateMatrix(NativeBattleEffect345Frame frame, NativeBattleEffectArena matrix, int axis, short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCuragaMatrices.Cosine8DDRotation(angle));
        short[] rotation = axis switch
        {
            0 => [4096, 0, 0, 0, cosine, unchecked((short)-sine), 0, sine, cosine],
            1 => [cosine, 0, sine, 0, 4096, 0, unchecked((short)-sine), 0, cosine],
            2 => [cosine, unchecked((short)-sine), 0, sine, cosine, 0, 0, 0, 4096],
            _ => throw new ArgumentOutOfRangeException(nameof(axis))
        };
        NativeBattleEffectArena right = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
        for (int index = 0; index < 9; index++)
        {
            right = right.Write(index * 2, BitConverter.GetBytes(rotation[index]));
        }
        (frame, right) = MultiplyMatrix(frame, Packed(matrix), right);
        return (frame, CopyBytes(right, 0, matrix, 0, 18));
    }

    public static (NativeBattleEffect345Frame Frame, NativeBattleEffectArena Matrix) MultiplyMatrix(NativeBattleEffect345Frame frame, NativeBattlePackedMatrix32 left, NativeBattleEffectArena right)
    {
        frame = LoadMatrix(frame, left, true, false);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)Word345(right, column * 2)), unchecked((ushort)Word345(right, column * 2 + 6)), unchecked((ushort)Word345(right, column * 2 + 12)));
            registers = NativeBattleGeometry.Transform(registers, false, true);
            for (int lane = 0; lane < 3; lane++)
            {
                short value = unchecked((short)(registers.Colors.Ir[lane] ?? throw new NotSupportedException("Effect345 requires its current saturated matrix IR.")));
                right = right.Write(column * 2 + lane * 6, BitConverter.GetBytes(value));
            }
        }
        return (Publish(frame, registers), right);
    }

    public static (NativeBattleEffect345Frame Frame, NativeBattleVector Value) TransformVector(NativeBattleEffect345Frame frame, NativeBattlePackedMatrix32 matrix, NativeBattleEffectArena point8)
    {
        frame = LoadMatrix(frame, matrix, true, false);
        (byte[] bytes, bool[] known) = point8.ReadSnapshot(0, 8);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        registers = registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } };
        registers = NativeBattleGeometry.Transform(registers, false);
        NativeBattleVector result = new(unchecked((short)(registers.Colors.Ir[0] ?? throw new NotSupportedException("Effect345 requires transformed IR1."))), unchecked((short)(registers.Colors.Ir[1] ?? throw new NotSupportedException("Effect345 requires transformed IR2."))), unchecked((short)(registers.Colors.Ir[2] ?? throw new NotSupportedException("Effect345 requires transformed IR3."))));
        return (Publish(frame, registers), result);
    }

    public static (NativeBattleEffect345Frame Frame, NativeBattleEffectArena Matrix) EulerMatrix(NativeBattleEffect345Frame frame, NativeBattleEffectArena matrix, NativeBattleVector angles)
    {
        matrix = IdentityMatrix(matrix);
        if (angles.Z != 0)
        {
            (frame, matrix) = RotateMatrix(frame, matrix, 2, angles.Z);
        }
        if (angles.X != 0)
        {
            (frame, matrix) = RotateMatrix(frame, matrix, 0, angles.X);
        }
        if (angles.Y != 0)
        {
            (frame, matrix) = RotateMatrix(frame, matrix, 1, angles.Y);
        }
        return (frame, matrix);
    }
}
