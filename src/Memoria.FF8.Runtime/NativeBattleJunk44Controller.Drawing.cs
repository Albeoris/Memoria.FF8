using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleJunk44Controller
{
    public static (NativeBattleJunk44Frame Frame, int Result) AdvanceDebris6E84D0(NativeBattleJunk44Frame frame, uint address, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 120);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(Signed(task, 0x1C), Signed(task, 0x1E), Signed(task, 0x20))) with { X = Signed(task, 0x0C), Y = Signed(task, 0x0E), Z = Signed(task, 0x10) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
        NativeBattleEffectArena block = Context(frame, temporary, 120);
        for (int index = 0; index < 9; index++)
        {
            block = block.Write(index * 2, BitConverter.GetBytes(matrix.Rotation[index]));
        }
        block = block.Write(20, BitConverter.GetBytes(matrix.X)).Write(24, BitConverter.GetBytes(matrix.Y)).Write(28, BitConverter.GetBytes(matrix.Z));
        (byte[] matrixBytes, bool[] matrixKnown) = block.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 composed = new(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray());
        frame = PublishContext(frame, temporary, block, 120);
        frame = LoadMatrix(frame, composed);
        int remaining = Signed(task, 0x12);
        NativeBattleEffectArena context = Context(frame, temporary + 32, 88).Write(0x1C, BitConverter.GetBytes(remaining < 4 ? 0xC3u : 0u));
        if (remaining < 4)
        {
            context = context.Write(8, new byte[4]).Write(12, BitConverter.GetBytes(unchecked((4 - remaining) << 10)));
        }
        byte[] table = resources.DebrisTable.AsSpan().Slice(checked((int)(task.ReadDword(0x2C) - 0x1362730)), 24).ToArray();
        int divisor = BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(16));
        int selected = remaining % divisor;
        if (selected < 0 || selected >= 4)
        {
            throw new InvalidDataException("Junk debris remainder leaves its actual material table row.");
        }
        uint mesh = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(selected * 4));
        context = context.Write(0, BitConverter.GetBytes(mesh));
        frame = inputs.Execute(PublishContext(frame, temporary + 32, context, 88), new NativeBattleJunk44DrawStatic88(temporary + 32, mesh));
        frame = Release(frame, 120);
        task = Read(frame, address).WriteWord(0x12, unchecked((ushort)(Read(frame, address).ReadWord(0x12) - 1)));
        if (task.ReadWord(0x12) == 0)
        {
            return (Write(frame, address, task), 2);
        }
        short velocity = unchecked((short)(task.ReadWord(0x16) + 50));
        short y = unchecked((short)(task.ReadWord(0x0E) + velocity));
        task = task.WriteWord(0x16, unchecked((ushort)velocity)).WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + task.ReadWord(0x14)))).WriteWord(0x0E, unchecked((ushort)y));
        uint modelAddress = task.ReadDword(0x30);
        short minimum = modelAddress == 0 ? (short)0 : inputs.ReadModel(frame, modelAddress).MinimumY146 ?? throw new NotSupportedException("Junk collision requires the current model minimum Y WORD.");
        if (y >= minimum)
        {
            task = task.WriteWord(0x0E, unchecked((ushort)(y - velocity))).WriteWord(0x16, unchecked((ushort)(-velocity / 3)));
            frame = DrawRandom(Write(frame, address, task), out ushort rotation);
            task = Read(frame, address).WriteWord(0x24, unchecked((ushort)((rotation & 0x1FF) - 0x100)));
            frame = Write(frame, address, task);
            if (modelAddress != 0)
            {
                frame = Write(frame, address, task.WriteWord(0x0E, unchecked((ushort)minimum)));
                frame = SpawnProgrammes44(frame, modelAddress, resources, inputs);
                frame = ApplyModelContacts6E87A0(frame, modelAddress, inputs);
                task = Read(frame, address).WriteWord(0x18, 80).WriteDword(0x30, 0);
            }
            else
            {
                int count = BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(18));
                int range = BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(20));
                int size = BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(22));
                frame = SpawnPointSprites6E8360(frame, count, TaskView(task, 0x0C, 8), unchecked(count * 200), range, size, resources, inputs);
                task = Read(frame, address);
            }
        }
        task = task.WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x18)))).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x24)))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + task.ReadWord(0x26))));
        return (Write(frame, address, task), 0);
    }

    private static NativeBattleJunk44Frame SpawnProgrammes44(NativeBattleJunk44Frame frame, uint modelAddress, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        NativeBattleJunk44PointStep anchor = inputs.ReadAnchor571400(frame, modelAddress);
        frame = DrawRandom(anchor.Frame, out ushort firstAngle);
        int angle = firstAngle;
        for (int index = 0; index < 8; index++)
        {
            (frame, uint address) = Append(frame, 0x2545110, 0x6E8810);
            if (address == 0)
            {
                break;
            }
            short x = unchecked((short)(Word44(anchor.Point8, 0) + NativeBattleCameraMath.Sine(angle) / 16));
            short z = unchecked((short)(Word44(anchor.Point8, 4) + NativeBattleCameraMath.Cosine(angle) / 16));
            frame = DrawRandom(frame, out ushort height);
            NativeBattleGraphicsModel model = inputs.ReadModel(frame, modelAddress);
            short minimum = model.MinimumY146 ?? throw new NotSupportedException("Junk programme birth requires current minimum Y.");
            short modelY = (model.Transform ?? throw new NotSupportedException("Junk programme birth requires current model position.")).Position28.Y;
            short y = unchecked((short)(minimum + modelY - (height & 255) - 128));
            frame = DrawRandom(frame, out ushort yaw);
            frame = DrawRandom(frame, out ushort speed);
            int angularVelocity = (speed & 255) + 256;
            frame = DrawRandom(frame, out ushort sign);
            if (sign < 0x4000)
            {
                angularVelocity = -angularVelocity;
            }
            NativeBattleModulePoolTask task = Read(frame, address).WriteWord(0x0C, unchecked((ushort)x)).WriteWord(0x0E, unchecked((ushort)y)).WriteWord(0x10, unchecked((ushort)z)).WriteWord(0x12, (ushort)(index + 8)).WriteWord(0x14, yaw).WriteWord(0x16, unchecked((ushort)angularVelocity));
            NativeBattleGfAnimationWorkspace workspace = new(TaskView(task, 0x18, 64).ReadSnapshot(0, 64).Data.ToImmutableArray());
            workspace = workspace.Initialize7016B0(resources.ReadProgramme(0x1354CE0), 64);
            frame = WriteBytes(Write(frame, address, task), address + 0x18, workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), 64);
            frame = DrawRandom(frame, out ushort spacing);
            angle = unchecked(angle + (spacing & 255) + 0x180);
        }
        return frame;
    }
}
