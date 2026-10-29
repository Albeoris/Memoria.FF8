using System.Collections.Immutable;
using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBladeShot12Controller
{
    public static (NativeBattleBladeShot12Frame Frame, int Result) AdvanceSprite(NativeBattleBladeShot12Frame frame, uint address, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback is not (0x6E9B50 or 0x6E9C10))
        {
            throw new NotSupportedException("Blade Shot sprite callback is unsupported.");
        }
        (frame, uint temporary) = Allocate(frame, 180);
        int heading = 0;
        if (task.Callback == 0x6E9C10)
        {
            NativeBattleEffectArena difference = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                difference = difference.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(Signed(task, 0x14 + axis * 2) - Signed(task, 0x2C + axis * 2)))));
            }
            frame = Setup12(frame, inputs, difference, 2048, -512);
            (frame, NativeBattleVector first) = Transform12(frame, frame.State.ReadView(0x2556108, 8));
            frame = Setup12(frame, inputs, TaskView(task, 0x14, 8), 2048, -512);
            (frame, NativeBattleVector second) = Transform12(frame, TaskView(task, 0x2C, 8));
            heading = unchecked(-NativePrecision53.RoundInteger(Math.Atan2(first.Y - second.Y, second.X - first.X) * (double)BitConverter.Int32BitsToSingle(0x4422F983)));
        }
        else
        {
            short size = Signed(task, 0x22);
            frame = Setup12(frame, inputs, TaskView(task, 0x14, 8), size, unchecked((short)-(size >> 2)));
        }
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(task.ReadDword(0x10))).Write(4, BitConverter.GetBytes(task.ReadWord(12))).Write(0x24, BitConverter.GetBytes((ushort)(task.Callback == 0x6E9C10 ? 1 : 0)));
        if (task.Callback == 0x6E9C10)
        {
            context = context.Write(8, BitConverter.GetBytes(heading));
        }
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleBladeShot12DrawSprite180(temporary, task.ReadDword(0x10)) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(12, task) with { ResourceGeneration = task.ReadDword(0x10) } });
        frame = Release(frame, 180);
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        if (Signed(task, 12) >= Signed(task, 0x1A))
        {
            return (Write(frame, address, task), 2);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            int velocity = 0x1C + axis * 2;
            task = task.WriteWord(velocity, unchecked((ushort)(task.ReadWord(velocity) + task.ReadWord(0x24 + axis * 2))));
            task = task.WriteWord(0x14 + axis * 2, unchecked((ushort)(task.ReadWord(0x14 + axis * 2) + task.ReadWord(velocity))));
        }
        return (Write(frame, address, task), 0);
    }

    public static (NativeBattleBladeShot12Frame Frame, int Result) AdvanceMesh(NativeBattleBladeShot12Frame frame, uint address, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x6E9D60)
        {
            throw new NotSupportedException("Blade Shot mesh callback is unsupported.");
        }
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattlePackedMatrix32 rotation = NativeBattleBladeShot12Geometry.Rotation6E9AB0(TaskView(task, 0x18, 6), new(new byte[32].ToImmutableArray(), new bool[32].ToImmutableArray()));
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.Bytes.AsSpan().Slice(index * 2))).ToImmutableArray(), Signed(task, 0x10), Signed(task, 0x12), Signed(task, 0x14), 4096);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        int cue = NativeBattleCameraMath.Sine(unchecked(Signed(task, 12) << 10) / 8);
        if (Signed(task, 0x16) < 4096)
        {
            cue = unchecked(4096 - (unchecked((4096 - cue) * Signed(task, 0x16)) >> 12));
        }
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0x1364F14u)).Write(8, new byte[] { 0, 0, unchecked((byte)((4096 - cue) >> 5)) }).Write(12, BitConverter.GetBytes((cue >> 1) + 2048)).Write(0x1C, BitConverter.GetBytes(0xC0u));
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleBladeShot12DrawCustomMesh88(temporary, 0x1364F14) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(12, task) });
        frame = Release(frame, 88);
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 8 ? 2 : 0);
    }

    public static NativeBattleBladeShot12Frame DrawTransient6EA3E0(NativeBattleBladeShot12Frame frame, uint address, int age, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs, int copy = 0)
    {
        if (unchecked((uint)age) >= 20)
        {
            return frame;
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = (age << 12) / 20;
        (frame, uint temporary) = Allocate(frame, 88);
        short[] rotation = NativeBattleDuelSharedPackets.Rotation6D9510(Signed(task, 0x56)).ToArray();
        rotation[6] = unchecked((short)(unchecked(rotation[6] * (4096 + phase)) >> 12));
        rotation[8] = unchecked((short)(unchecked(rotation[8] * (4096 + phase)) >> 12));
        NativeBattleGraphicsModel target = inputs.ReadModel(frame, frame.State.ReadDword(0x25460F8));
        NativeBattleActionMatrix local = new(rotation.ToImmutableArray(), unchecked(Signed(task, 0x4C) + (unchecked(rotation[2] * phase) >> 14)), (target.Transform ?? throw new NotSupportedException("Blade Shot transient mesh requires its current recipient transform.")).Angle36, unchecked(Signed(task, 0x50) + (unchecked(rotation[0] * phase) >> 14)), 4096);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0x136401Cu)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(NativeBattleCameraMath.Sine(phase >> 2))).Write(0x1C, BitConverter.GetBytes(0xF0u));
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleBladeShot12DrawStatic88(temporary, 0x136401C) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(12, task, 0x100 + copy) });
        return Release(frame, 88);
    }

    private static NativeBattleBladeShot12Frame Setup12(NativeBattleBladeShot12Frame frame, NativeBattleBladeShot12Inputs inputs, NativeBattleEffectArena point, short size, short depth)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), xyz, known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null, size, depth) };
    }

    private static (NativeBattleBladeShot12Frame Frame, NativeBattleVector Point) Transform12(NativeBattleBladeShot12Frame frame, NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        NativeBattleGeometryRegisterStep step = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters!);
        step = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(step, 0, vector, known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null));
        NativeBattleVector result = new(unchecked((short)step.Colors.Ir[0]!.Value), unchecked((short)step.Colors.Ir[1]!.Value), unchecked((short)step.Colors.Ir[2]!.Value));
        return (frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } }, result);
    }
}
