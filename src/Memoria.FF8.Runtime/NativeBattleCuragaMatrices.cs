namespace Memoria.FF8.Runtime;

/// <summary>8DD770/8DD7E0/8DD8A0/8DD960's identity and postmultiplied GTE rotations.</summary>
public static class NativeBattleCuragaMatrices
{
    public static NativeBattleActionMatrix Identity8DD770() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    public static int Cosine8DDRotation(short angle) => NativeBattleCameraMath.Sine((angle + 0x400) & 0xFFF);

    public static NativeBattleActionMatrix RotateX8DD7E0(NativeBattleActionMatrix matrix, short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)Cosine8DDRotation(angle));
        return NativeBattlePoseMatrices.MultiplyGteRotation45E470(matrix, new([4096, 0, 0, 0, cosine, unchecked((short)-sine), 0, sine, cosine], 0, 0, 0, 4096));
    }

    public static NativeBattleActionMatrix RotateY8DD8A0(NativeBattleActionMatrix matrix, short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)Cosine8DDRotation(angle));
        return NativeBattlePoseMatrices.MultiplyGteRotation45E470(matrix, new([cosine, 0, sine, 0, 4096, 0, unchecked((short)-sine), 0, cosine], 0, 0, 0, 4096));
    }

    public static NativeBattleActionMatrix RotateZ8DD960(NativeBattleActionMatrix matrix, short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)Cosine8DDRotation(angle));
        return NativeBattlePoseMatrices.MultiplyGteRotation45E470(matrix, new([cosine, unchecked((short)-sine), 0, sine, cosine, 0, 0, 0, 4096], 0, 0, 0, 4096));
    }

    public static NativeBattleActionMatrix Euler8818A0(NativeBattleVector rotation)
    {
        NativeBattleActionMatrix matrix = Identity8DD770();
        if (rotation.Z != 0)
        {
            matrix = RotateZ8DD960(matrix, rotation.Z);
        }
        if (rotation.X != 0)
        {
            matrix = RotateX8DD7E0(matrix, rotation.X);
        }
        if (rotation.Y != 0)
        {
            matrix = RotateY8DD8A0(matrix, rotation.Y);
        }
        return matrix;
    }

    public static NativeBattleVector RotateVector45DF80(NativeBattleActionMatrix matrix, NativeBattleVector point)
    {
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, point);
        return new((short)Math.Clamp(x, short.MinValue, short.MaxValue), (short)Math.Clamp(y, short.MinValue, short.MaxValue), (short)Math.Clamp(z, short.MinValue, short.MaxValue));
    }
}
