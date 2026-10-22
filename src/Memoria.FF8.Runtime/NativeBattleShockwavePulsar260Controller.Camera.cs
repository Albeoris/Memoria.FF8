namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShockwavePulsar260Controller
{
    public static NativeBattleShockwavePulsar260Frame AdvanceMainCamera(NativeBattleShockwavePulsar260Frame frame, int age, NativeBattleShockwavePulsar260Inputs inputs)
    {
        bool firstMove = unchecked((uint)(age - 20)) < 39;
        bool secondMove = unchecked((uint)(age - 79)) < 40;
        bool thirdMove = unchecked((uint)(age - 119)) < 60;
        if (age is not (0 or 59 or 213) && !firstMove && !secondMove && !thirdMove)
        {
            return frame;
        }
        int x = unchecked((short)frame.State.ReadWord(0x25086A0));
        int z = unchecked((short)frame.State.ReadWord(0x25086A4));
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraControl());
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraFocal(288));
            NativeBattleCameraPose pose = MainCameraPose(frame, inputs, x + 2463, -4674, z - 5697, x - 318, -2185, z + 313) with { Roll = 0 };
            return inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraPose(pose));
        }
        if (firstMove)
        {
            NativeBattleCameraPose first = MainCameraPose(frame, inputs, x + 2463, -4674, z - 5697, x - 318, -2185, z + 313);
            NativeBattleCameraPose second = MainCameraPose(frame, inputs, x + 418, 1741, z - 8000, x - 102, -8385, z - 2438);
            int angle = unchecked((age - 20) << 12) / 38 >> 2;
            int factor = unchecked(4096 - NativeBattleCameraMath.Cosine(angle));
            return MainCameraBlend(frame, inputs, first, second, factor);
        }
        if (age == 59)
        {
            return inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraPose(MainCameraPose(frame, inputs, x - 361, -13641, z - 8304, x - 365, -16445, z - 538)));
        }
        if (secondMove)
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraFocal(200));
            NativeBattleCameraPose first = MainCameraPose(frame, inputs, x - 6357, -5670, z - 4421, x - 29, 2999, z + 291);
            NativeBattleCameraPose second = MainCameraPose(frame, inputs, x + 27, -1073, z - 4566, x - 204, -13, z + 2255);
            int factor = unchecked((age - 79) << 12) / 39;
            return MainCameraBlend(frame, inputs, first, second, factor);
        }
        if (thirdMove)
        {
            NativeBattleCameraPose first = MainCameraPose(frame, inputs, x + 27, -1073, z - 4566, x - 204, -13, z + 2255);
            NativeBattleCameraPose second = MainCameraPose(frame, inputs, x + 8894, -1218, z + 2368, x - 205, -14, z + 2256);
            int factor = unchecked((age - 119) << 12) / 59;
            return MainCameraBlend(frame, inputs, first, second, factor);
        }
        frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraFocal(288));
        return inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraPose(MainCameraPose(frame, inputs, x + 4455, -8171, z + 8301, x + 702, 99, z - 459)));
    }

    private static NativeBattleShockwavePulsar260Frame MainCameraBlend(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Inputs inputs, NativeBattleCameraPose first, NativeBattleCameraPose second, int factor)
    {
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleSummonColors constants = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattleCameraPoint target = Blend(first.Target, second.Target);
        NativeBattleCameraPoint eye = Blend(first.Eye, second.Eye);
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers.Colors } };
        return inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraPose(new(eye, target, first.FocalLength, first.Roll)));

        NativeBattleCameraPoint Blend(NativeBattleCameraPoint a, NativeBattleCameraPoint b)
        {
            registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - factor)) } };
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)a.X), unchecked((ushort)a.Y), unchecked((ushort)a.Z));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, constants);
            registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } };
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)b.X), unchecked((ushort)b.Y), unchecked((ushort)b.Z));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, constants);
            return new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        }
    }

    private static NativeBattleCameraPose MainCameraPose(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Inputs inputs, int eyeX, int eyeY, int eyeZ, int targetX, int targetY, int targetZ)
    {
        NativeBattleCameraState camera = inputs.ReadCamera(frame);
        return new(new(unchecked((short)eyeX), unchecked((short)eyeY), unchecked((short)eyeZ)), new(unchecked((short)targetX), unchecked((short)targetY), unchecked((short)targetZ)), camera.CurrentPose?.FocalLength, camera.Roll);
    }
}
