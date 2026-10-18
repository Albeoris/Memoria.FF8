using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanCameraFrame(NativeBattleScanFrame Scan, NativeBattleScanByteOwner Eye, NativeBattleScanByteOwner Target, ushort Zoom);
public sealed record NativeBattleScanCameraInputs(ImmutableArray<NativeBattleScanByteOwner> Models, NativeBattleScanByteOwner Profile, ushort Category, short Direction1D9777C, short Direction1D97788, Func<int, byte, int, NativeBattleVector> ReadBone502170);
public sealed record NativeBattleScanCameraStep(NativeBattleScanCameraFrame Frame, int Result);
public sealed record NativeBattleScanCameraInterpolation(NativeBattleScanByteOwner Camera, bool Complete);

public static class NativeBattleScanCameraTransition
{
    public static NativeBattleScanCameraStep Advance84E810(NativeBattleScanCameraFrame frame, uint address, NativeBattleScanCameraInputs inputs)
    {
        NativeBattleScanProcess process = frame.Scan.Memory.Scan ?? throw new NotSupportedException("Native Scan camera requires its current physical presentation owners.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x84E810)
        {
            throw new InvalidDataException("Native Scan camera transition requires its84E810 task.");
        }
        NativeBattleScanByteOwner camera = process.Presentation.Camera269A168;
        camera.RequireLength(80);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                camera = Initialize84E870(camera, task.ReadByte(0x2D), task.ReadWord(0x68), process.Presentation.PrimaryModel269CE30, frame.Eye, frame.Target, frame.Zoom, inputs);
                task = Next(task);
                break;
            case 1:
                NativeBattleScanCameraInterpolation interpolated = Advance84EB40(camera);
                camera = interpolated.Camera;
                if (interpolated.Complete)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                frame = Publish84EC80(frame, camera);
                camera = frame.Scan.Memory.Scan!.Presentation.Camera269A168;
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native Scan camera signed phase exceeds its three callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        process = frame.Scan.Memory.Scan!;
        frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools }, Memory = frame.Scan.Memory with { Scan = process with { Presentation = process.Presentation with { Camera269A168 = camera } } } } };
        return new(frame, complete ? 2 : 0);
    }

    public static NativeBattleScanByteOwner Initialize84E870(NativeBattleScanByteOwner camera, int targetModel, ushort mode, uint selectedModelPointer, NativeBattleScanByteOwner eye, NativeBattleScanByteOwner target, ushort zoom, NativeBattleScanCameraInputs inputs)
    {
        camera.RequireLength(80);
        eye.RequireLength(8);
        target.RequireLength(8);
        inputs.Profile.RequireLength(16);
        int selected = ModelIndex(inputs.Models, selectedModelPointer);
        if ((uint)targetModel >= 7)
        {
            throw new InvalidDataException("Native Scan camera requires its actual target physical model.");
        }
        NativeBattleScanByteOwner primary = inputs.Models[selected];
        camera = camera.WriteWord(0x4C, inputs.Category).WriteWord(0x30, 0);
        if (mode <= 3)
        {
            int radius = unchecked((short)inputs.Profile.ReadWord(0));
            if (mode != 1)
            {
                radius = unchecked((short)(3 * radius / 4));
            }
            camera = camera.WriteWord(0x3E, unchecked((ushort)radius)).WriteWord(0x44, mode == 1 ? (ushort)0 : unchecked((ushort)(radius / 2))).WriteWord(0x32, mode == 1 ? (ushort)682 : mode == 3 ? (ushort)512 : (ushort)256);
        }
        int dx = S(eye, 0) - S(target, 0);
        int dz = S(eye, 4) - S(target, 4);
        int square = unchecked(dx * dx + dz * dz);
        int distance = unchecked((int)(long)Math.Sqrt(square));
        camera = camera.WriteWord(0x48, zoom).WriteWord(0x4A, 576).WriteWord(0x3C, unchecked((ushort)-distance)).WriteWord(0x42, unchecked((ushort)(S(eye, 2) - S(target, 2)))).WriteWord(0x38, primary.ReadWord(0x0E)).WriteWord(0x36, (ushort)((NativeWorldScriptedPathMath.NativeAngle(inputs.Direction1D97788, inputs.Direction1D9777C) - 1024) & 0xFFF));
        if (mode <= 3)
        {
            int sampled = mode == 1 ? selected : targetModel;
            NativeBattleVector anchor = inputs.ReadBone502170(sampled, 0xF1, 0);
            camera = camera.WriteWord(0x18, unchecked((ushort)anchor.X)).WriteWord(0x1A, unchecked((ushort)anchor.Y)).WriteWord(0x1C, unchecked((ushort)anchor.Z));
            if (inputs.Profile.ReadWord(8) == 0)
            {
                camera = camera.WriteDword(0x18, inputs.Models[sampled].ReadDword(0x1C)).WriteDword(0x1C, inputs.Models[sampled].ReadDword(0x20));
            }
            camera = camera.WriteWord(0x1A, 0);
            NativeBattleVector local = new(mode == 1 ? S(inputs.Profile, 2) : (short)0, S(inputs.Profile, 4), 0);
            NativeBattleActionMatrix yaw = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), S(primary, 0x0E));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(yaw, local);
            camera = camera.WriteWord(0x18, unchecked((ushort)(S(camera, 0x18) + rotated.X))).WriteWord(0x1A, unchecked((ushort)(S(camera, 0x1A) + rotated.Y))).WriteWord(0x1C, unchecked((ushort)(S(camera, 0x1C) + rotated.Z)));
        }
        return camera.WriteDword(0x10, target.ReadDword(0)).WriteDword(0x14, target.ReadDword(4));
    }

    public static NativeBattleScanCameraInterpolation Advance84EB40(NativeBattleScanByteOwner camera)
    {
        camera.RequireLength(80);
        short progress = unchecked((short)(camera.ReadWord(0x30) + camera.ReadWord(0x32)));
        bool complete = progress >= 4096;
        if (complete)
        {
            progress = 4096;
        }
        camera = camera.WriteWord(0x30, unchecked((ushort)progress));
        (int Output, int Start, int End)[] lanes = [(0x34, 0x36, 0x38), (0x3A, 0x3C, 0x3E), (0x40, 0x42, 0x44), (0x46, 0x48, 0x4A), (8, 0x10, 0x18), (10, 0x12, 0x1A), (12, 0x14, 0x1C)];
        foreach (var lane in lanes)
        {
            int start = S(camera, lane.Start);
            int end = S(camera, lane.End);
            int value = unchecked(start + unchecked((end - start) * progress) / 4096);
            camera = camera.WriteWord(lane.Output, unchecked((ushort)value));
        }
        return new(camera, complete);
    }

    public static NativeBattleScanCameraFrame Publish84EC80(NativeBattleScanCameraFrame frame, NativeBattleScanByteOwner camera)
    {
        camera.RequireLength(80);
        frame.Eye.RequireLength(8);
        frame.Target.RequireLength(8);
        NativeBattleActionMatrix yaw = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), S(camera, 0x34));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(yaw, new(0, S(camera, 0x40), S(camera, 0x3A)));
        camera = camera.WriteWord(0, unchecked((ushort)(S(camera, 8) + rotated.X))).WriteWord(2, unchecked((ushort)(S(camera, 10) + rotated.Y))).WriteWord(4, unchecked((ushort)(S(camera, 12) + rotated.Z)));
        NativeBattleScanProcess process = frame.Scan.Memory.Scan ?? throw new NotSupportedException("Native Scan camera publication requires its retained process.");
        return frame with
        {
            Eye = frame.Eye.WriteDword(0, camera.ReadDword(0)).WriteDword(4, camera.ReadDword(4)),
            Target = frame.Target.WriteDword(0, camera.ReadDword(8)).WriteDword(4, camera.ReadDword(12)),
            Zoom = camera.ReadWord(0x46),
            Scan = frame.Scan with { Memory = frame.Scan.Memory with { Scan = process with { Presentation = process.Presentation with { Camera269A168 = camera } } } }
        };
    }

    private static short S(NativeBattleScanByteOwner owner, int offset) => unchecked((short)owner.ReadWord(offset));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));

    private static int ModelIndex(ImmutableArray<NativeBattleScanByteOwner> models, uint pointer)
    {
        long offset = (long)pointer - 0x1D972C0;
        if (models.IsDefault || models.Length != 7 || models.Any(model => model is null) || offset < 0 || offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Native Scan camera requires its actual selected physical model pointer.");
        }
        int selected = (int)(offset / 156);
        foreach (NativeBattleScanByteOwner model in models)
        {
            model.RequireLength(156);
        }
        return selected;
    }
}
