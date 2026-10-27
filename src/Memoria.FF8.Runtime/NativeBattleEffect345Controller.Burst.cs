namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Controller
{
    private static NativeBattleEffect345Step AdvanceBurst(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 2))
        {
            case 0:
                NativeBattleEffect345PointStep point = inputs.Point502170(frame, PhysicalModelAddress(task.ReadByte(0x2D)), 241, 0, TaskView(task, 0x1C, 8));
                frame = Write(point.Frame, address, CopyTask(Read(point.Frame, address), 0x1C, point.Output8, 8));
                frame = IncrementPhase(frame, address);
                break;
            case 1:
                frame = SpawnMotes70CA80(frame, address, resources, inputs);
                break;
            case 2:
                break;
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect345Frame SpawnMotes70CA80(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask parent = Read(frame, address);
        NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, PhysicalModelAddress(parent.ReadByte(0x2D)));
        NativeBattleEffectArena basis = IdentityMatrix(NativeBattleEffectArena.Clear().MarkUnknown(0, 32));
        (frame, basis) = RotateMatrix(frame, basis, 1, Word345(model, 0xE));
        (frame, basis) = RotateMatrix(frame, basis, 2, 0x155);
        (frame, basis) = RotateMatrix(frame, basis, 0, 0xEAB);
        int age = parent.ReadWord(0x24);
        int firstCount = age switch { 1 => 4, 2 => 3, 3 => 2, 4 => 1, _ => 0 };
        int secondCount = age switch { 1 or 2 => 2, 3 or 4 => 1, _ => 0 };
        frame = SpawnMoteGroup(frame, address, basis, firstCount, false, inputs);
        frame = SpawnMoteGroup(frame, address, basis, secondCount, true, inputs);
        if (Signed(Read(frame, address), 0x24) >= 4)
        {
            frame = IncrementPhase(MarkComplete(frame, address), address);
        }
        return frame;
    }

    private static NativeBattleEffect345Frame SpawnMoteGroup(NativeBattleEffect345Frame frame, uint parent, NativeBattleEffectArena basis, int count, bool second, NativeBattleEffect345Inputs inputs)
    {
        for (int index = 0; index < count; index++)
        {
            (frame, uint child) = AppendHierarchy(frame, 5, 0x70CD70, 280, parent, inputs);
            for (int mote = 0; mote < 4; mote++)
            {
                NativeBattleModulePoolTask task = Read(frame, child);
                NativeBattleModulePoolTask owner = Read(frame, parent);
                task = task.WriteDword(0xD4 + mote * 8, owner.ReadDword(0x1C)).WriteDword(0xD8 + mote * 8, owner.ReadDword(0x20));
                frame = Write(frame, child, task);
                frame = DrawCrt(frame, out ushort yaw);
                frame = DrawCrt(frame, out ushort tilt);
                NativeBattleEffectArena matrix = basis;
                (frame, matrix) = RotateMatrix(frame, matrix, 1, unchecked((short)(yaw & 0xFFF)));
                int pitch = second ? (tilt & 0xFF) + 0x380 : ((tilt & 0x1FF) - 0x100) & 0xFFF;
                (frame, matrix) = RotateMatrix(frame, matrix, 0, unchecked((short)(pitch & 0xFFF)));
                task = CopyTask(Read(frame, child), 0x54 + mote * 32, matrix, 32);
                frame = Write(frame, child, task);
                frame = DrawCrt(frame, out ushort speed);
                short vy = unchecked((short)(-0x1000 - (speed & 0x1FFF)));
                task = Read(frame, child).WriteWord(0xF6 + mote * 8, unchecked((ushort)vy));
                NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(Packed(matrix).ReadMatrix(), Vector(TaskView(task, 0xF4 + mote * 8, 8)));
                frame = Write(frame, child, CopyTask(task, 0xF4 + mote * 8, PutPoint(NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 0xF4 + mote * 8, 8), 0, NativeBattleEffectArena.CreateUnknown(8), 0, 8), 0, velocity), 8));
            }
        }
        return frame;
    }

    private static NativeBattleEffect345Frame DrawCrt(NativeBattleEffect345Frame frame, out ushort value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out value);
        return frame with { Random = random };
    }

    private static NativeBattleEffect345Step AdvanceMotes(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 2))
        {
            case 0:
                frame = Write(frame, address, task.WriteDword(0x30, 0x1000).WriteDword(0x34, 0x1000).WriteDword(0x38, 0x1000).WriteDword(0x4C, NativeBattleEffect345Resources.MeshAddress).WriteByte(0x40, 0x28).WriteByte(0x41, 0).WriteByte(0x42, 0xA).WriteByte(0x29, 1));
                break;
            case 1:
                if (Signed(task, 0x24) >= 14)
                {
                    frame = IncrementPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
        }
        task = Read(frame, address);
        for (int mote = 0; mote < 4; mote++)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                int velocityOffset = 0xF4 + mote * 8 + lane * 2;
                int positionOffset = 0xD4 + mote * 8 + lane * 2;
                short velocity = Signed(task, velocityOffset);
                velocity = unchecked((short)(velocity - velocity / 4));
                task = task.WriteWord(velocityOffset, unchecked((ushort)velocity));
                task = task.WriteWord(positionOffset, unchecked((ushort)(task.ReadWord(positionOffset) + velocity / 16)));
            }
        }
        int age = Signed(task, 0x24);
        if ((uint)age > 14)
        {
            throw new InvalidDataException("Effect345 mote age contradicts its fresh allocation and frame14 completion.");
        }
        task = task.WriteDword(0x30, unchecked((uint)Curve(resources, 0x1529720, age))).WriteDword(0x34, unchecked((uint)Curve(resources, 0x1529740, age))).WriteDword(0x38, unchecked((uint)Curve(resources, 0x1529760, age))).WriteWord(0x50, unchecked((ushort)Curve(resources, 0x1529780, age)));
        frame = Write(frame, address, task);
        for (int mote = 0; mote < 4; mote++)
        {
            task = Read(frame, address);
            task = task.WriteDword(0x1C, task.ReadDword(0xD4 + mote * 8)).WriteDword(0x20, task.ReadDword(0xD8 + mote * 8));
            frame = Write(frame, address, task);
            frame = NativeBattleEffect345Body.Draw70CEC0(frame, address, Packed(TaskView(task, 0x54 + mote * 32, 32)), resources, inputs);
        }
        return Finish(frame, address);
    }

    private static short Curve(NativeBattleEffect345Resources resources, uint address, int index) => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(checked(address + (uint)(index * 2)), 2).AsSpan());
}
