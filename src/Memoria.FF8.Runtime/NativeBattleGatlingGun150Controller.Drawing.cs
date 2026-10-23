using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun150Controller
{
    public static (NativeBattleGatlingGun150Frame Frame, int Result) AdvanceMesh(NativeBattleGatlingGun150Frame frame, uint address, NativeBattleGatlingGun150Resources resources, NativeBattleGatlingGun150Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x60E6D0;
        bool third = task.Callback == 0x60F1C0;
        if (!first && !third && task.Callback != 0x60EBD0)
        {
            throw new NotSupportedException("Gatling Gun mesh callback is unsupported.");
        }
        short age = Signed(task, 12);
        short scale = Signed(task, 0x1C);
        NativeBattleVector angles = first ? new(0, Signed(task, 0x18), Signed(task, 0x1A)) : third ? new(1024, Signed(task, 0x18), 0) : new(0, Signed(task, 0x18), 0);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(angles), scale, scale, scale) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        if (third)
        {
            matrix = matrix with { Rotation = NativeBattleGfMatrices.Multiply56C090(matrix, NativeBattleGfMatrices.Euler56CD50(new(0, Signed(task, 0x1A), 0))).Rotation };
        }
        if (first)
        {
            frame = SetupParticle150(frame, TaskView(task, 0x10, 8), 0, unchecked((short)-(scale >> 3)), inputs);
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Gatling Gun mesh requires its current geometry registers."), matrix, true, false, null) } };
        }
        else
        {
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        }
        (frame, uint temporary) = Allocate(frame, 88);
        uint source = first ? 0xDC24E0u : third ? 0xDC2AA8u : 0xDC27E0u;
        bool fade = third ? age >= 1 : age >= 0;
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(fade ? 0xF3u : 0x33u));
        if (fade)
        {
            int cue = third ? unchecked((age - 1) << 10) : unchecked(age << (first ? 11 : 9));
            context = context.Write(12, BitConverter.GetBytes(cue));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleGatlingGun150DrawStatic88(temporary, source));
        if (first)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun150DrawStatic88(temporary, source));
        }
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (!first)
        {
            short speed = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed))).WriteWord(0x1E, unchecked((ushort)(speed - speed / 8)));
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        bool done = first ? Signed(task, 12) >= 2 : third ? Signed(task, 12) >= 5 : Signed(task, 12) > 8;
        return (Write(frame, address, task), done ? 2 : 0);
    }

    public static (NativeBattleGatlingGun150Frame Frame, int Result) AdvanceSprite(NativeBattleGatlingGun150Frame frame, uint address, NativeBattleGatlingGun150Resources resources, NativeBattleGatlingGun150Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x60ED10)
        {
            throw new NotSupportedException("Gatling Gun sprite callback is unsupported.");
        }
        short scale = Signed(task, 0x1C);
        frame = SetupParticle150(frame, TaskView(task, 0x10, 8), scale, unchecked((short)-(scale >> 3)), inputs);
        (frame, uint temporary) = Allocate(frame, 180);
        ushort phase = task.ReadWord(12);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xDC1FA4u)).Write(4, BitConverter.GetBytes(phase)).Write(0x24, new byte[2]), 180);
        frame = inputs.Execute(frame, new NativeBattleGatlingGun150DrawSprite180(temporary, 0xDC1FA4));
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 16 ? 2 : 0);
    }

    private static NativeBattleGatlingGun150Frame SetupParticle150(NativeBattleGatlingGun150Frame frame, NativeBattleEffectArena point, short scale, short depth, NativeBattleGatlingGun150Inputs inputs)
    {
        NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, w, scale, depth) };
    }
}
