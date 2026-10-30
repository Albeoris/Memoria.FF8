using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect312Early
{
    private const uint ChildDescriptor = 0x223E1A8;
    private const uint CurveBase = 0x223F090;
    private const int CurveStride = 0x334;

    public static NativeBattleEffect312Step Advance(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect312Controller.Read(frame, address);
        return task.Callback switch
        {
            0x59D680 or 0x59D7B0 or 0x59D930 => AdvanceMesh(frame, address, task, resources, inputs),
            0x59DA50 => AdvanceCurve(frame, address, task, inputs),
            0x59DD30 => AdvanceBeam(frame, address, task, resources, inputs),
            _ => throw new NotSupportedException($"Effect312 early callback 0x{task.Callback:X8} is unsupported.")
        };
    }

    private static NativeBattleEffect312Step AdvanceMesh(NativeBattleEffect312Frame frame, uint address, NativeBattleModulePoolTask task, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        NativeBattlePackedMatrix32 source = Packed(frame.State.ReadView(0x223E148, 32));
        NativeBattleActionMatrix world = source.ReadMatrix();
        if (task.Callback == 0x59D7B0)
        {
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, Signed(task, 0x18)));
            world = world with { Rotation = NativeBattleGfMatrices.Multiply56C090(world, rotation).Rotation };
            _ = frame.State.ReadView(0x223E168, 4);
        }
        int scale = Signed(task, 0x1C);
        world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale);
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(camera, null);
        frame = NativeBattleEffect312Controller.LoadMatrix(frame, matrix);
        (frame, uint temporary) = NativeBattleEffect312Controller.Allocate(frame, 88);
        NativeBattleEffectArena context = NativeBattleEffect312Controller.ReadBytes(frame, temporary, 88);
        uint mesh = task.Callback switch { 0x59D680 => 0xCDDE9C, 0x59D7B0 => 0xCDDDCC, _ => 0xCDE73C };
        (uint flags, int? fade) = MeshFade(task.Callback, Signed(task, 12));
        context = Put(Put(Put(context, 0, mesh), 8, 0), 0x1C, flags);
        if (fade is int value)
        {
            context = Put(context, 12, unchecked((uint)value));
        }
        frame = NativeBattleEffect312Controller.WriteBytes(frame, temporary, context, 88);
        frame = NativeBattleEffect312Mesh.Draw572200(frame, temporary, mesh, matrix, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(312, task) with { ResourceGeneration = mesh } : null);
        frame = NativeBattleEffect312Controller.Release(frame, 88);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        (task, int result) = UpdateMotion(task);
        frame = NativeBattleEffect312Controller.Write(frame, address, task);
        return new(frame, result);
    }

    public static (uint Flags, int? Fade) MeshFade(uint callback, short age)
    {
        return callback switch
        {
            0x59D680 when age == 0 => (0xF3, 0x800),
            0x59D680 when age >= 12 => (0xF3, (age - 12) << 9),
            0x59D680 => (0x33, null),
            0x59D7B0 when age < 4 => (0x33, ((8 - age) << 9) | 0xC0),
            0x59D7B0 when age >= 18 => (0xF3, (age - 18) << 9),
            0x59D7B0 => (0x33, null),
            0x59D930 when age >= 1 => (0xF3, 372 * (age - 1)),
            0x59D930 => (0x33, null),
            0x59DD30 when age == 0 => (0xF3, 0x800),
            0x59DD30 when age >= 16 => (0xF3, (age - 16) << 9),
            0x59DD30 => (0x33, null),
            _ => throw new NotSupportedException("Effect312 fade requires its selected early mesh callback.")
        };
    }

    public static (NativeBattleModulePoolTask Task, int Result) UpdateMotion(NativeBattleModulePoolTask task)
    {
        int age = Signed(task, 12);
        switch (task.Callback)
        {
            case 0x59D680:
                if (age < 8)
                {
                    short velocity = Signed(task, 0x1E);
                    task = Word(task, 0x1C, Signed(task, 0x1C) - velocity);
                    task = Word(task, 0x1E, velocity - velocity / 6);
                }
                break;
            case 0x59D7B0:
                short spin = Signed(task, 0x1A);
                task = Word(task, 0x18, Signed(task, 0x18) + spin);
                task = Word(task, 0x1A, spin - spin / 8);
                break;
            case 0x59D930:
                short growth = Signed(task, 0x1E);
                task = Word(task, 0x1C, Signed(task, 0x1C) + growth);
                task = Word(task, 0x1E, growth - growth / 6);
                break;
            case 0x59DD30:
                short step = Signed(task, 0x1A);
                task = Word(task, 0x18, Signed(task, 0x18) + step);
                if (step > 2)
                {
                    task = Word(task, 0x1A, step - 1);
                }
                break;
            default:
                throw new NotSupportedException("Effect312 motion requires its selected early callback.");
        }
        task = Word(task, 12, age + 1);
        bool complete = task.Callback switch
        {
            0x59D680 => Signed(task, 12) >= 20,
            0x59D7B0 => Signed(task, 12) >= 26,
            0x59D930 => Signed(task, 12) >= 12,
            _ => Signed(task, 0x18) >= 48
        };
        return (task, complete ? 2 : 0);
    }

    private static NativeBattleEffect312Step AdvanceCurve(NativeBattleEffect312Frame frame, uint address, NativeBattleModulePoolTask task, NativeBattleEffect312Inputs inputs)
    {
        uint group = Group(task);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        if (Signed(task, 12) == 0)
        {
            NativeBattleEffectArena curve = NativeBattleEffect312Controller.ReadBytes(frame, group, CurveStride);
            NativeBattleEffectArena source = frame.State.ReadView(0x2240EB0, 8);
            curve = NativeBattleEffect312Controller.CopyBytes(source, 0, curve, 0, 8);
            curve = NativeBattleEffect312Controller.CopyBytes(source, 0, curve, 8, 8);
            foreach (int offset in new[] { 0, 4, 8, 12 })
            {
                int random = Draw(ref frame);
                curve = Word(curve, offset, Signed(curve, offset) + random % 7400 - 3700);
            }
            int dx = Signed(curve, 8) - Signed(curve, 0);
            int dz = Signed(curve, 12) - Signed(curve, 4);
            int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dz * dz));
            curve = NativeBattleEffect312Controller.CopyBytes(curve, 0, curve, 0x10, 8);
            curve = NativeBattleEffect312Controller.CopyBytes(curve, 8, curve, 0x30, 8);
            foreach (int offset in new[] { 0x1A, 0x22, 0x2A })
            {
                curve = Word(curve, offset, 0);
            }
            curve = Word(curve, 0x20, (Signed(curve, 0) + Signed(curve, 8)) >> 1);
            curve = Word(curve, 0x24, (Signed(curve, 4) + Signed(curve, 12)) >> 1);
            curve = Word(curve, 0x18, (Signed(curve, 0) + Signed(curve, 0x20)) >> 1);
            curve = Word(curve, 0x1C, (Signed(curve, 4) + Signed(curve, 0x24)) >> 1);
            curve = Word(curve, 0x28, (Signed(curve, 0x20) + Signed(curve, 8)) >> 1);
            curve = Word(curve, 0x2C, (Signed(curve, 0x24) + Signed(curve, 12)) >> 1);
            foreach (int offset in new[] { 0x18, 0x1C, 0x20, 0x24, 0x28, 0x2C })
            {
                int random = Draw(ref frame);
                curve = Word(curve, offset, Signed(curve, offset) + random % unchecked(length * 2) - length);
            }
            frame = NativeBattleEffect312Controller.WriteBytes(frame, group, curve, CurveStride);
            (frame, uint temporary) = NativeBattleEffect312Controller.Allocate(frame, 400);
            NativeBattleEffectArena work = NativeBattleEffect312Controller.ReadBytes(frame, temporary, 400);
            work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(5, curve, 0x10, work);
            for (int sample = 0; sample < 48; sample++)
            {
                curve = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(5, work, sample * 4096 / 47, curve, 0x38 + sample * 8);
            }
            frame = NativeBattleEffect312Controller.WriteBytes(frame, temporary, work, 400);
            frame = NativeBattleEffect312Controller.WriteBytes(frame, group, curve, CurveStride);
            if (inputs.CapturePresentation)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, CurveBase, Signed(task, 14));
                for (int point = 0; point < 48; point++)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, group + 0x38, point);
                }
            }
            frame = NativeBattleEffect312Controller.Release(frame, 400);
            (frame, uint first) = NativeBattleEffect312Controller.TrySpawn(frame, ChildDescriptor, 0x59DD30);
            frame = InitializeChild(frame, first, task);
            int size = Draw(ref frame) % 512 + 512;
            NativeBattleModulePoolTask child = NativeBattleEffect312Controller.Read(frame, first);
            frame = NativeBattleEffect312Controller.Write(frame, first, Word(child, 0x1C, size));
            (frame, uint second) = NativeBattleEffect312Controller.TrySpawn(frame, ChildDescriptor, 0x59F7E0);
            frame = InitializeChild(frame, second, task);
            frame = frame with { State = frame.State.WriteDword(group + 0x1B8, 0).WriteDword(group + 0x1CC, 0).WriteDword(group + 0x1E0, 0) };
            (frame, uint third) = NativeBattleEffect312Controller.TrySpawn(frame, ChildDescriptor, 0x59FBD0);
            frame = InitializeChild(frame, third, task);
        }
        task = Word(task, 12, Signed(task, 12) + 1);
        frame = NativeBattleEffect312Controller.Write(frame, address, task);
        return new(frame, Signed(task, 12) >= 16 ? 2 : 0);
    }

    private static NativeBattleEffect312Frame InitializeChild(NativeBattleEffect312Frame frame, uint address, NativeBattleModulePoolTask parent)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect312Controller.Read(frame, address);
        task = Word(Word(Word(Word(task, 12, 0), 14, parent.ReadWord(14)), 0x18, 0), 0x1A, 2);
        return NativeBattleEffect312Controller.Write(frame, address, task);
    }

    private static NativeBattleEffect312Step AdvanceBeam(NativeBattleEffect312Frame frame, uint address, NativeBattleModulePoolTask task, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        uint sample = checked(Group(task) + 0x38u + (uint)(Signed(task, 0x18) * 8));
        NativeBattleEffectArena point = NativeBattleEffect312Controller.ReadBytes(frame, sample, 8);
        int x = Signed(point, 0) - unchecked((short)frame.State.ReadWord(0x2240EC8));
        int y = Signed(point, 2) - unchecked((short)frame.State.ReadWord(0x2240ECA));
        int z = Signed(point, 4) - unchecked((short)frame.State.ReadWord(0x2240ECC));
        int squared = NativeBattleSleepingGas168.NormalizeSquare(x, y, z);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(squared);
        byte[] axisBytes = resources.ReadStatic(0xCDF660, 12);
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(BinaryPrimitives.ReadInt32LittleEndian(axisBytes), BinaryPrimitives.ReadInt32LittleEndian(axisBytes.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(axisBytes.AsSpan(8)), direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix aligned = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        world = world with { X = unchecked((short)frame.State.ReadWord(0x2240EC8)), Y = unchecked((short)frame.State.ReadWord(0x2240ECA)), Z = unchecked((short)frame.State.ReadWord(0x2240ECC)) };
        world = NativeBattlePoseMatrices.Scale56BEF0(world, Signed(task, 0x1C), Signed(task, 0x1C), unchecked(length << 12) / 5350);
        world = world with { Rotation = NativeBattleGfMatrices.Multiply56C090(aligned, world).Rotation };
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(camera, null);
        frame = NativeBattleEffect312Controller.LoadMatrix(frame, matrix);
        (frame, uint temporary) = NativeBattleEffect312Controller.Allocate(frame, 108);
        NativeBattleEffectArena context = NativeBattleEffect312Controller.ReadBytes(frame, temporary, 108);
        (uint flags, int? fade) = MeshFade(task.Callback, Signed(task, 12));
        context = Put(Put(Put(Put(context, 0, 0xCD8FF4), 8, 0), 0x1C, flags), 0x20, 0x30307F);
        if (fade is int value)
        {
            context = Put(context, 12, unchecked((uint)value));
        }
        frame = NativeBattleEffect312Controller.WriteBytes(frame, temporary, context, 108);
        frame = NativeBattleEffect312Mesh.DrawLocal(frame, temporary, 0xCD8FF4, matrix, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(312, task) with { ResourceGeneration = 0xCD8FF4 } : null);
        frame = NativeBattleEffect312Controller.Release(frame, 108);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        (task, int result) = UpdateMotion(task);
        return new(NativeBattleEffect312Controller.Write(frame, address, task), result);
    }

    private static uint Group(NativeBattleModulePoolTask task) => unchecked(CurveBase + (uint)(Signed(task, 14) * CurveStride));
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Signed(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleModulePoolTask Word(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)value));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));

    private static int Draw(ref NativeBattleEffect312Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

}
