namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect65Camera
{
    public static NativeBattleEffect65Frame Interpolate6E2C10(NativeBattleEffect65Frame frame, NativeBattleVector firstTarget, NativeBattleVector firstEye, NativeBattleVector secondTarget, NativeBattleVector secondEye, int firstRoll, int secondRoll, int ratio, NativeBattleEffect65Inputs inputs)
    {
        short roll = unchecked((short)(firstRoll + unchecked(ratio * (secondRoll - firstRoll)) / 4096));
        return Blend(frame, firstTarget, firstEye, secondTarget, secondEye, ratio, roll, inputs);
    }

    public static NativeBattleEffect65Frame Interpolate6FC250(NativeBattleEffect65Frame frame, NativeBattleVector firstTarget, NativeBattleVector firstEye, NativeBattleVector secondTarget, NativeBattleVector secondEye, int firstRoll, int secondRoll, int elapsed, int duration, NativeBattleEffect65Inputs inputs)
    {
        if (duration <= 1)
        {
            throw new InvalidDataException("Effect65 camera interpolation requires its native nonzero divisor.");
        }
        short roll = unchecked((short)(firstRoll + unchecked(elapsed * (secondRoll - firstRoll)) / (duration - 1)));
        int ratio = unchecked(elapsed << 12) / (duration - 1);
        frame = inputs.Execute(frame, new NativeBattleEffect65CameraRoll(roll));
        return Blend(frame, firstTarget, firstEye, secondTarget, secondEye, ratio, null, inputs);
    }

    private static NativeBattleEffect65Frame Blend(NativeBattleEffect65Frame frame, NativeBattleVector firstTarget, NativeBattleVector firstEye, NativeBattleVector secondTarget, NativeBattleVector secondEye, int ratio, short? roll, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleGeometryRegisterStep registers = NativeBattleEffect65Controller.Registers(frame);
        NativeBattleVector target = Point(firstTarget, secondTarget);
        NativeBattleVector eye = Point(firstEye, secondEye);
        frame = NativeBattleEffect65Controller.Publish(frame, registers);
        frame = inputs.Execute(frame, new NativeBattleEffect65CameraPoseXYZ(new(eye.X, eye.Y, eye.Z), new(target.X, target.Y, target.Z)));
        return roll is short value ? inputs.Execute(frame, new NativeBattleEffect65CameraRoll(value)) : frame;

        NativeBattleVector Point(NativeBattleVector first, NativeBattleVector second)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - ratio)) } }, unchecked((ushort)first.X), unchecked((ushort)first.Y), unchecked((ushort)first.Z));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)ratio) } }, unchecked((ushort)second.X), unchecked((ushort)second.Y), unchecked((ushort)second.Z));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, inputs.Colors);
            return new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        }
    }
}
