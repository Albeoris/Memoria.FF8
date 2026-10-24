using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Lower
{
    private static (ushort Category, NativeBattleScanByteOwner Profile) Profile(NativeBattleEffect68Frame frame, int actor, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        ImmutableArray<NativeBattleScanByteOwner> models = inputs.ReadModels(frame).Select(row => ByteOwner(row, 0x9C)).ToImmutableArray();
        byte category = NativeBattleScanModelSelection.Category84D410(models, checked((short)actor), _ => throw new NotSupportedException("Effect68 requires its current resource sequence."), index => inputs.ReadResourceSequence(frame, index));
        NativeBattleScanCameraProfile profile = resources.ReadCameraProfile(category);
        return (category, new(profile.Bytes, Enumerable.Repeat(true, 16).ToImmutableArray()));
    }

    private static int PhysicalIndex(uint pointer)
    {
        uint offset = unchecked(pointer - 0x1D972C0u);
        if (offset >= 7 * 0x9C || offset % 0x9C != 0)
        {
            throw new InvalidDataException("Effect68 requires its current selected physical model.");
        }
        return (int)(offset / 0x9C);
    }

    private static short V(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));
    private static NativeBattleEffectArena CW(NativeBattleEffectArena row, int offset, int value) => row.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
    private static (NativeBattleEffect68Frame Frame, NativeBattleActionMatrix Matrix) Yaw(NativeBattleEffect68Frame frame, short angle)
    {
        NativeBattleStatefulMatrixRotationStep rotation = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, frame.Staging ?? throw new NotSupportedException("Effect68 camera requires its actual temporary owner."), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0), 'Y', angle);
        return (frame with { Memory = rotation.Memory, Staging = rotation.Staging }, rotation.Matrix.ReadMatrix());
    }

    private static NativeBattleEffect68Frame InitializeCamera(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (ushort category, NativeBattleScanByteOwner profile) = Profile(frame, task.ReadByte(0x2D), resources, inputs);
        NativeBattleEffectArena camera = ReadBytes(frame, Camera, 80);
        camera = CW(CW(camera, 0x4C, category), 0x30, 0);
        int mode = task.ReadWord(0x68);
        if (mode <= 3)
        {
            short radius = unchecked((short)profile.ReadWord(0));
            int end = mode == 1 ? radius : unchecked((short)(radius * 3 / 4));
            camera = CW(CW(CW(camera, 0x3E, end), 0x44, mode == 1 ? 0 : end / 2), 0x32, mode == 1 ? 682 : mode == 3 ? 512 : 256);
        }
        NativeBattleEffect68CameraSnapshot snapshot = inputs.ReadCamera(frame);
        int dx = V(snapshot.Eye8, 0) - V(snapshot.Target8, 0);
        int dz = V(snapshot.Eye8, 4) - V(snapshot.Target8, 4);
        int distance = unchecked((int)(long)Math.Sqrt(unchecked(dx * dx + dz * dz)));
        ImmutableArray<NativeBattleEffectArena> models = inputs.ReadModels(frame);
        int primary = PhysicalIndex(D(frame, 0x25FFAA8));
        camera = CW(CW(CW(CW(CW(CW(camera, 0x48, snapshot.Zoom), 0x4A, 576), 0x3C, -distance), 0x42, V(snapshot.Eye8, 2) - V(snapshot.Target8, 2)), 0x38, V(models[primary], 0x0E)), 0x36, (NativeWorldScriptedPathMath.NativeAngle(snapshot.Direction88, snapshot.Direction7C) - 1024) & 0xFFF);
        frame = WriteBytes(frame, Camera, camera, 80);
        if (mode <= 3)
        {
            int sampled = mode == 1 ? primary : task.ReadByte(0x2D);
            NativeBattleEffectArena retained = NativeBattleEffectNodeContextMemory.Copy(camera, 0x18, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
            NativeBattleEffect68PointStep anchor = inputs.Point502170(frame, sampled, 0xF1, 0, retained);
            frame = anchor.Frame;
            camera = ReadBytes(frame, Camera, 80);
            camera = NativeBattleEffectNodeContextMemory.Copy(anchor.Output8, 0, camera, 0x18, 8);
            if (profile.ReadWord(8) == 0)
            {
                models = inputs.ReadModels(frame);
                camera = NativeBattleEffectNodeContextMemory.Copy(models[sampled], 0x1C, camera, 0x18, 8);
            }
            camera = CW(camera, 0x1A, 0);
            frame = WriteBytes(frame, Camera, camera, 80);
            (frame, NativeBattleActionMatrix yaw) = Yaw(frame, V(inputs.ReadModels(frame)[primary], 0x0E));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(yaw, new(mode == 1 ? unchecked((short)profile.ReadWord(2)) : (short)0, unchecked((short)profile.ReadWord(4)), 0));
            camera = ReadBytes(frame, Camera, 80);
            camera = CW(CW(CW(camera, 0x18, V(camera, 0x18) + rotated.X), 0x1A, V(camera, 0x1A) + rotated.Y), 0x1C, V(camera, 0x1C) + rotated.Z);
        }
        snapshot = inputs.ReadCamera(frame);
        camera = NativeBattleEffectNodeContextMemory.Copy(snapshot.Target8, 0, camera, 0x10, 8);
        frame = WriteBytes(frame, Camera, camera, 80);
        return Write(frame, address, Next(Read(frame, address)));
    }

    public static NativeBattleEffectArena InterpolateCamera(NativeBattleEffectArena camera, out bool complete)
    {
        NativeBattleScanCameraInterpolation step = NativeBattleScanCameraTransition.Advance84EB40(ByteOwner(camera, 80));
        complete = step.Complete;
        return Arena(step.Camera);
    }

    private static NativeBattleEffect68Frame AdvanceCamera(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleEffectArena camera = InterpolateCamera(ReadBytes(frame, Camera, 80), out bool complete);
        frame = WriteBytes(frame, Camera, camera, 80);
        if (complete)
        {
            frame = Write(frame, address, Done(Read(frame, address)));
        }
        return PublishCamera(frame, inputs);
    }

    private static NativeBattleEffect68Frame CameraTask(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        return Phase(Read(frame, address), 3) switch
        {
            0 => InitializeCamera(frame, address, resources, inputs),
            1 => AdvanceCamera(frame, address, inputs),
            _ => frame
        };
    }

    private static NativeBattleEffect68Frame PublishCamera(NativeBattleEffect68Frame frame, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleEffectArena camera = ReadBytes(frame, Camera, 80);
        (frame, NativeBattleActionMatrix yaw) = Yaw(frame, V(camera, 0x34));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(yaw, new(0, V(camera, 0x40), V(camera, 0x3A)));
        camera = ReadBytes(frame, Camera, 80);
        camera = CW(CW(CW(camera, 0, V(camera, 8) + rotated.X), 2, V(camera, 10) + rotated.Y), 4, V(camera, 12) + rotated.Z);
        frame = WriteBytes(frame, Camera, camera, 80);
        NativeBattleEffectArena eye = NativeBattleEffectNodeContextMemory.Copy(camera, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
        NativeBattleEffectArena target = NativeBattleEffectNodeContextMemory.Copy(camera, 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
        return inputs.Execute(frame, new NativeBattleEffect68PublishCamera(eye, target, unchecked((ushort)V(camera, 0x46))));
    }

    private static NativeBattleEffect68Frame ActiveCamera(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        (ushort category, NativeBattleScanByteOwner profile) = Profile(frame, Read(frame, address).ReadByte(0x2D), resources, inputs);
        frame = PutWord(frame, Camera + 0x4C, category);
        NativeBattleEffectArena camera = ReadBytes(frame, Camera, 80);
        if (profile.ReadByte(0x0E) == 1)
        {
            camera = CW(camera, 0x34, (V(camera, 0x34) + 64) & 0xFFF);
        }
        if (unchecked((short)profile.ReadWord(4)) < -384)
        {
            int value = unchecked((short)(V(camera, 0x40) - V(camera, 0x44) / 32));
            camera = CW(camera, 0x40, value > 0 ? 0 : value);
        }
        frame = WriteBytes(frame, Camera, camera, 80);
        frame = PublishCamera(frame, inputs);
        if (W(frame, 0x26005B2) == 1)
        {
            frame = PutWord(PutWord(frame, Highlight, 0), Exit, 1);
            frame = Write(frame, address, Next(Read(frame, address)));
        }
        return frame;
    }

    private static NativeBattleEffect68Frame DelayedCamera(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
        frame = Write(frame, address, task);
        if (S(task, 0x66) < 0)
        {
            (frame, _) = Child(frame, 0x25FD6E0, 0x7DDED0, 0x6C, address, inputs);
            frame = Write(frame, address, Next(Read(frame, address).WriteWord(0x66, 8)));
        }
        return frame;
    }

    private static NativeBattleEffect68Frame DelayedResults(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
        frame = Write(frame, address, task);
        if (S(task, 0x66) < 0)
        {
            (frame, _) = Child(frame, 0x25FFC40, 0x7DE540, 0x78, address, inputs);
            (frame, _) = Child(frame, 0x25FD6E0, 0x7DE110, 0x6C, address, inputs);
            frame = Write(frame, address, Next(Read(frame, address)));
        }
        return frame;
    }

    private static NativeBattleEffect68Frame ExitCamera(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadByte(0x28) != 0)
        {
            return frame;
        }
        int actor = task.ReadByte(0x2D);
        NativeBattleModulePoolTask root = Read(frame, task.ReadDword(0x10));
        if (unchecked((sbyte)task.ReadByte(0x2A)) == S(root, 0x58))
        {
            (frame, _) = Child(frame, 0x25FFC40, 0x7DE830, 0x78, address, inputs);
        }
        else if (V(ReadBytes(frame, Camera, 80), 0x3A) <= -8192)
        {
            (frame, uint child) = Child(frame, 0x25FFC40, 0x7DD950, 0x78, address, inputs);
            frame = Write(frame, child, Read(frame, child).WriteWord(0x68, 3));
        }
        NativeBattleEffectArena flags = inputs.ReadModels(frame)[actor];
        flags = CW(NativeBattleEffectArena.Clear(), 0, BinaryPrimitives.ReadUInt16LittleEndian(flags.Read(0, 2)) & 0x87FF);
        frame = inputs.Execute(frame, new NativeBattleEffect68WriteModelBytes(actor, 0, flags, 2));
        return Write(frame, address, Next(Read(frame, address)));
    }

    private static NativeBattleEffect68Frame RestoreCamera(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(task, 3))
        {
            case 0:
                NativeBattleEffect68CameraSnapshot saved = inputs.ReadCamera(frame);
                frame = inputs.Execute(frame, new NativeBattleEffect68PublishCamera(saved.SavedEye8, saved.SavedTarget8, saved.SavedZoom));
                task = Next(Read(frame, address).WriteWord(0x66, 1));
                break;
            case 1:
                task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                if (S(task, 0x66) <= 0)
                {
                    task = Done(task);
                }
                break;
        }
        return Write(frame, address, task);
    }
}
