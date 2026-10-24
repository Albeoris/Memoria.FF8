using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect318Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect318Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect318Sprite
{
    public static NativeBattleEffect318Step Advance(NativeBattleEffect318Frame frame, uint address, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena difference = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            difference = PutWord318(difference, axis * 2, Signed(task, 0x10 + axis * 2) - Signed(task, 0x28 + axis * 2));
        }
        frame = Setup(frame, difference, inputs);
        (frame, NativeBattleVector first) = Transform(frame, frame.State.ReadView(0x2505480, 8));
        frame = Setup(frame, TaskView(task, 0x10, 8), inputs);
        (frame, NativeBattleVector second) = Transform(frame, TaskView(task, 0x28, 8));
        int heading = unchecked(-NativePrecision53.RoundInteger(Math.Atan2(first.Y - second.Y, second.X - first.X) * (double)BitConverter.Int32BitsToSingle(0x4422F983)));
        NativeBattleEffectArena context = Put318(PutWord318(PutWord318(Put318(Context(frame, temporary, 180), 0, 0xFF7A00), 4, task.ReadWord(12)), 0x24, 1), 8, unchecked((uint)heading));
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect318DrawBatch(draw.Batch, 0x250541C));
        frame = Release(frame, 180);
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        if (Signed(task, 12) >= 12)
        {
            return new(Write(frame, address, task), 2);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x18 + axis * 2, unchecked((ushort)(task.ReadWord(0x18 + axis * 2) + task.ReadWord(0x20 + axis * 2))));
            task = task.WriteWord(0x10 + axis * 2, unchecked((ushort)(task.ReadWord(0x10 + axis * 2) + task.ReadWord(0x18 + axis * 2))));
        }
        return new(Write(frame, address, task), 0);
    }

    private static NativeBattleEffect318Frame Setup(NativeBattleEffect318Frame frame, NativeBattleEffectArena point, NativeBattleEffect318Inputs inputs)
    {
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector318(point), Padding(point), 1024, -256) };
    }

    private static (NativeBattleEffect318Frame Frame, NativeBattleVector Point) Transform(NativeBattleEffect318Frame frame, NativeBattleEffectArena point)
    {
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector318(point), Padding(point)), true);
        NativeBattleVector result = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        return (Publish(frame, registers), result);
    }

    private static short? Padding(NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
}
