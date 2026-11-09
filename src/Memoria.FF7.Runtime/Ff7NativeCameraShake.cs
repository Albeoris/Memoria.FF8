namespace Memoria.FF7.Runtime;

public sealed record Ff7CameraShakeAxis(bool Enabled = false, byte Phase = 0, byte RandomIndex = 0, sbyte Offset = 0, ushort Amplitude = 0, short From = 0, short Target = 0, ushort Duration = 0, short Step = 0)
{
    public void Validate()
    {
        if (Phase > 1 || Amplitude > 255 || Duration > 255 || Step is < 0 or > 256)
        {
            throw new InvalidDataException("FFVII camera shake continuation is invalid.");
        }
    }
}

public static class Ff7NativeCameraShake
{
    public static Ff7CameraShakeAxis ResetForField(Ff7CameraShakeAxis axis)
    {
        return axis with { Enabled = false, Phase = 0, Amplitude = 0, Duration = 0, Step = 0 };
    }

    public static Ff7CameraShakeAxis Configure(Ff7CameraShakeAxis axis, bool enabled, byte amplitude, byte duration)
    {
        return enabled ? axis with { Enabled = true, Amplitude = amplitude, Duration = duration } : axis with { Enabled = false };
    }

    public static Ff7CameraShakeAxis Advance(Ff7CameraShakeAxis axis, IReadOnlyList<byte> randomTable, Ff7NativeMovement interpolation)
    {
        axis.Validate();
        if (randomTable.Count != 256)
        {
            throw new InvalidDataException("FFVII camera shake requires its signed-index native random table.");
        }
        if (axis.Enabled)
        {
            if (axis.Phase == 0)
            {
                short target = Sample(axis, randomTable);
                return axis with { Phase = 1, Step = 0, From = 0, Target = target, RandomIndex = unchecked((byte)(axis.RandomIndex + 1)) };
            }
            if (axis.Duration < axis.Step)
            {
                short sampled = Sample(axis, randomTable);
                short target = axis.Target < 0 ? sampled : unchecked((short)-sampled);
                return axis with { Step = 0, From = axis.Target, Target = target, RandomIndex = unchecked((byte)(axis.RandomIndex + 1)) };
            }
        }
        else if (axis.Phase == 1)
        {
            if (axis.Duration < axis.Step)
            {
                return axis with { Phase = 0, Step = 0, From = axis.Target, Target = 0, RandomIndex = unchecked((byte)(axis.RandomIndex + 1)) };
            }
        }
        else if (axis.Duration == axis.Step)
        {
            return axis with { Offset = 0 };
        }
        if (axis.Duration == 0)
        {
            throw new NotSupportedException("FFVII camera shake interpolation requires a positive native duration.");
        }
        short step = unchecked((short)(axis.Step + 1));
        int offset = interpolation.Interpolate(axis.From, axis.Target, axis.Duration, step, true);
        return axis with { Step = step, Offset = unchecked((sbyte)offset) };
    }

    private static short Sample(Ff7CameraShakeAxis axis, IReadOnlyList<byte> randomTable)
    {
        int random = randomTable[unchecked((sbyte)axis.RandomIndex) + 128];
        short product = unchecked((short)(random * axis.Amplitude));
        return (short)(product / 256);
    }
}
