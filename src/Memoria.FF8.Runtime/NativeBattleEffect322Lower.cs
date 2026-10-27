using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect322Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect322Lower
{
    public static NativeBattleEffect322Step Advance(NativeBattleEffect322Frame frame, uint address, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (task.Callback)
        {
            case 0x65B600:
            case 0x65B680:
                return AdvanceSprite(frame, address, resources, inputs);
            case 0x65BB20:
                return AdvanceMesh(frame, address, resources, inputs);
            case 0x65B7E0:
                NativeBattleEffectArena caller = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 0x10, 8), 0, NativeBattleEffectArena.CreateUnknown(16), 0, 8);
                caller = Put322(Put322(caller, 8, unchecked((uint)-128)), 12, checked(frame.State.ReadDword(0x2505340) + 0x1CCF0));
                NativeBattleEffect322Step programme = NativeBattleEffect322Programme.Advance(frame, address + 0x18, 0x210, caller, resources, inputs);
                return new(programme.Frame, programme.Result == 0 ? 2 : 0);
            default:
                throw new NotSupportedException("The effect requires its selected lower callback.");
        }
    }

    public static NativeBattleEffect322Frame DrawMesh572200(NativeBattleEffect322Frame frame, uint temporary88, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary88, 88);
        NativeBattleStaticMeshContext88Step draw = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(Dword322(context, 0)), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary88, draw.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect322DrawBatch(draw.Batch, 0x2505334));
    }

    public static NativeBattleEffect322Step AdvanceSprite(NativeBattleEffect322Frame frame, uint address, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool oriented = task.Callback == 0x65B680;
        uint temporary;
        int heading = 0;
        if (oriented)
        {
            (frame, temporary) = Allocate(frame, 180);
            NativeBattleEffectArena difference = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                difference = PutWord322(difference, axis * 2, Signed(task, 12 + axis * 2) - Signed(task, 0x24 + axis * 2));
            }
            frame = Setup(frame, difference, 1024, -256, inputs);
            (frame, NativeBattleVector first) = TransformIr(frame, frame.State.ReadView(0x2505338, 8));
            frame = Setup(frame, TaskView(task, 12, 8), 1024, -256, inputs);
            (frame, NativeBattleVector second) = TransformIr(frame, TaskView(task, 0x24, 8));
            heading = unchecked(-NativePrecision53.RoundInteger(Math.Atan2(first.Y - second.Y, second.X - first.X) * (double)BitConverter.Int32BitsToSingle(0x4422F983)));
        }
        else
        {
            short scale = Signed(task, 0x14);
            frame = Setup(frame, TaskView(task, 12, 8), scale, -(scale >> 2), inputs);
            (frame, temporary) = Allocate(frame, 180);
        }
        NativeBattleEffectArena context = PutWord322(PutWord322(Put322(Context(frame, temporary, 180), 0, oriented ? 0xFE42A8u : 0xFE4384u), 4, task.ReadWord(0x12)), 0x24, oriented ? 1 : 0);
        if (oriented)
        {
            context = Put322(context, 8, unchecked((uint)heading));
        }
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect322DrawBatch(draw.Batch, 0x2505334));
        frame = Release(frame, 180);
        task = Read(frame, address);
        task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + 1)));
        if (Signed(task, 0x12) >= (oriented ? 8 : 12))
        {
            return new(Write(frame, address, task), 2);
        }
        if (oriented)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + task.ReadWord(0x14 + axis * 2))));
                task = task.WriteWord(0x14 + axis * 2, unchecked((ushort)(task.ReadWord(0x14 + axis * 2) + task.ReadWord(0x1C + axis * 2))));
            }
        }
        return new(Write(frame, address, task), 0);
    }

    public static NativeBattleEffect322Step AdvanceMesh(NativeBattleEffect322Frame frame, uint address, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), Packed322(TaskView(task, 0x10, 32)).ReadMatrix());
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        int cue = NativeBattleCameraMath.Sine(unchecked(Signed(task, 12) << 10) / 8);
        int strength = Signed(task, 14);
        if (strength < 4096)
        {
            cue = unchecked(4096 - (unchecked((4096 - cue) * strength) >> 12));
        }
        NativeBattleEffectArena context = Put322(Put322(Put322(Context(frame, temporary, 88), 0, 0xFE5A50), 12, unchecked((uint)cue)), 0x1C, 0xF0);
        context = context.Write(8, new byte[] { 0, 0, unchecked((byte)Math.Min(255, (4096 - cue) >> 4)) });
        frame = PublishContext(frame, temporary, context, 88);
        frame = DrawMesh572200(frame, temporary, resources, inputs);
        frame = Release(frame, 88);
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return new(Write(frame, address, task), Signed(task, 12) < 8 ? 0 : 2);
    }

    private static NativeBattleEffect322Frame Setup(NativeBattleEffect322Frame frame, NativeBattleEffectArena point, int scale, int depth, NativeBattleEffect322Inputs inputs)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector322(point), w, unchecked((short)scale), unchecked((short)depth)) };
    }

    private static (NativeBattleEffect322Frame Frame, NativeBattleVector Point) TransformIr(NativeBattleEffect322Frame frame, NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector322(point), w), true);
        return (Publish(frame, registers), new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value)));
    }

    public static NativeBattlePackedMatrix32 Packed322(NativeBattleEffectArena source)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public static uint Dword322(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    public static short Word322(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    public static NativeBattleVector Vector322(NativeBattleEffectArena value) => new(Word322(value, 0), Word322(value, 2), Word322(value, 4));
    public static NativeBattleEffectArena Put322(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleEffectArena PutWord322(NativeBattleEffectArena value, int offset, int number) => value.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
}
