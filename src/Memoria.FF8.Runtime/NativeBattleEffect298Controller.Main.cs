using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    private static (NativeBattleEffect298Frame Frame, int Result) AdvanceRoot(NativeBattleEffect298Frame frame, uint address, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, CursorGlobal, unchecked(arena + (uint)(alternate ? FirstPacketOffset : SecondPacketOffset)));
        frame = Write(frame, address, Read(frame, address).WriteDword(16, alternate ? 0u : 1u));
        int age = Signed(root, 12);
        if (age == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect298Music47E3C0(240));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, 0x25078C0, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect298 main reset did not supply its mandatory native task.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x227A);
            NativeBattleEffect298VibrationStep vibration = inputs.StartVibration(frame);
            frame = vibration.Frame;
            NativeBattleModulePoolTask task = Read(frame, main).WriteDword(16, unchecked((uint)vibration.Handle)).WriteDword(0x89EC, main + 0x2EC).WriteDword(0x89F0, main + 0x83EC);
            NativeBattleGraphicsModel caster = inputs.ReadModel(frame, frame.State.ReadDword(CasterGlobal));
            NativeBattleModelTransform transform = caster.Transform ?? throw new NotSupportedException("Effect298 requires its current caster transform.");
            task = CopyTask(task, 20, PointView(transform.Position28, transform.Vertical34), 8);
            frame = Write(frame, main, task);
            frame = BirthScript670C10(frame, 0x1056754, resources);
            frame = BirthScript670C10(frame, 0x10567E8, resources);
            frame = BirthScript670C10(frame, 0x1056560, resources);
            frame = inputs.Execute(frame, new NativeBattleEffect298InitializeIo534110(IoOffset));
        }
        if (Read(frame, address).ReadByte(15) != 0 && age < 117)
        {
            foreach (int pool in new[] { 4, 1, 3, 2 })
            {
                (frame, _) = AdvancePool(frame, pool, resources, inputs);
            }
        }
        if (age == 118)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect298QueueFile5341D0(360, unchecked(arena + FirstPacketOffset), 0));
        }
        if (Read(frame, address).ReadByte(15) != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect298AdvanceIo534210());
        }
        if (age > 118)
        {
            NativeBattleEffect298IoPoll poll = inputs.PollIo5342C0(frame, 0);
            return (poll.Frame, poll.Busy ? 0 : 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static (NativeBattleEffect298Frame Frame, int Result) AdvanceMain(NativeBattleEffect298Frame frame, uint address, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age >= 115)
        {
            return (frame, 2);
        }
        uint caster = frame.State.ReadDword(CasterGlobal);
        if (age >= 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect298DrawOffsetBody672330(caster, address + 28));
            frame = inputs.Execute(frame, new NativeBattleEffect298ModelFields(caster, 0, 4));
        }
        frame = DrawChunks6725F0(frame, address, resources, inputs);
        frame = DrawSmoke672C40(frame, address, resources, inputs);
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect298Sound501330(resources.Sound, 0, 128));
        }
        else if (age == 111)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect298StopVibration4A2940(unchecked((int)Read(frame, address).ReadDword(16))));
        }
        if (age >= 90)
        {
            NativeBattleModelTransform transform = inputs.ReadModel(frame, caster).Transform ?? throw new NotSupportedException("Effect298 requires its current caster position.");
            frame = inputs.Execute(frame, new NativeBattleEffect298ModelFields(caster, 0, 0, unchecked((short)(transform.Position28.Z - 128))));
        }
        frame = AdvanceLargeRows672BB0(frame, address);
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static NativeBattleEffect298Frame AdvanceLargeRows672BB0(NativeBattleEffect298Frame frame, uint main)
    {
        for (int index = 0; index < 192; index++)
        {
            uint address = main + 0x2EC + (uint)(index * 0xAC);
            NativeBattleEffectArena row = ReadBytes(frame, address, 0xAC);
            ushort remaining = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(row.Read(6, 2));
            if (remaining == 0)
            {
                continue;
            }
            remaining = unchecked((ushort)(remaining - 1));
            row = row.Write(6, BitConverter.GetBytes(remaining));
            if (remaining == 0)
            {
                frame = WriteBytes(frame, address, row, 0xAC);
                frame = Write(frame, main, Read(frame, main).WriteDword(0x89EC, address));
                continue;
            }
            short Word(int offset) => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));
            foreach ((int destination, int velocity) in new[] { (16, 20), (18, 22), (0, 8), (4, 12), (2, 10) })
            {
                row = row.Write(destination, BitConverter.GetBytes(unchecked((short)(Word(destination) + Word(velocity)))));
            }
            row = row.Write(10, BitConverter.GetBytes(unchecked((short)(Word(10) + 20))));
            if (Word(2) > 0)
            {
                row = row.Write(10, BitConverter.GetBytes(unchecked((short)(-Word(10) >> 1)))).Write(20, BitConverter.GetBytes(unchecked((short)-Word(20)))).Write(22, BitConverter.GetBytes(unchecked((short)-Word(22))));
            }
            frame = WriteBytes(frame, address, row, 0xAC);
        }
        return frame;
    }
}
