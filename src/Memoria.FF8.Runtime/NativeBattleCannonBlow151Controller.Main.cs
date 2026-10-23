namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCannonBlow151Controller
{
    public static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceMain(NativeBattleCannonBlow151Frame frame, uint address, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        short index = Signed(Read(frame, address), 14);
        RequireLane(index);
        if (age == 0)
        {
            frame = PrepareTrajectory(frame, index, inputs);
        }
        if (age == 1)
        {
            (frame, uint child) = Spawn(frame, 0x60D260, index);
            frame = DrawRandom(frame, out ushort angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out ushort size);
            ushort quarter = (ushort)((size % 768 + 2048) / 4);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, quarter).WriteWord(30, quarter));
            (frame, child) = Spawn(frame, 0x60D3B0, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(1 << index)));
            (frame, child) = Spawn(frame, 0x60D670, index);
            frame = DrawRandom(frame, out angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out size);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, (ushort)(size % 1536 + 6400)));
            (frame, child) = Spawn(frame, 0x60D790, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, 0).WriteWord(26, 5));
            frame = DrawRandom(frame, out size);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, (ushort)((size & 511) + 3072)));
            (frame, child) = Spawn(frame, 0x60D850, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(1 << index)).WriteWord(28, 0).WriteWord(30, 5));
            (frame, child) = Spawn(frame, 0x60DA10, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(256 << index)).WriteWord(28, 0).WriteWord(30, 5));
            (frame, child) = Spawn(frame, 0x60DC20, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(256 << index)).WriteWord(28, 0).WriteWord(30, 5));
        }
        if (age == 14)
        {
            uint target = 0x244F888u + (uint)(index * 8);
            (frame, uint child) = Spawn(frame, 0x60DE40, index);
            frame = WriteTaskView(frame, child, 16, frame.State.ReadView(target, 8), 8);
            frame = DrawRandom(frame, out ushort angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out ushort speed);
            frame = Write(frame, child, Read(frame, child).WriteWord(26, (ushort)(speed % 140 + 80)));
            frame = DrawRandom(frame, out ushort size);
            ushort quarter = (ushort)((size % 1280 + 3584) / 4);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, quarter).WriteWord(30, quarter));
            (frame, child) = Spawn(frame, 0x60E060, index);
            frame = WriteTaskView(frame, child, 16, frame.State.ReadView(target, 8), 8);
            frame = DrawRandom(frame, out angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out size);
            quarter = (ushort)(((size & 1023) + 7680) / 4);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, quarter).WriteWord(30, quarter));
        }
        if (age == 14)
        {
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
            int count = header.ReadByte(16);
            int first = count == 0 ? 0 : header.FirstContactIndex ?? throw new NotSupportedException("Cannon Blow requires its physical contact rows.");
            for (int contact = 0; contact < count; contact++)
            {
                int actor = inputs.Packets.ReadContact(first + contact).Primary.ActorIndex;
                NativeBattleCannonBlow151PointStep bone = inputs.ReadBone502170(frame, actor, 0xF1, 0);
                frame = bone.Frame;
                for (int particle = 0; particle < 5; particle++)
                {
                    (frame, uint child) = RequireAppend(frame, 0x60E1B0);
                    frame = DrawRandom(frame, out ushort delay);
                    NativeBattleModulePoolTask particleTask = CopyTask(Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(delay % 6 + particle * 2)), 16, bone.Point8, 8);
                    frame = DrawRandom(frame, out ushort x);
                    particleTask = particleTask.WriteWord(16, unchecked((ushort)(Signed(particleTask, 16) + x % 800 - 400)));
                    frame = DrawRandom(frame, out ushort y);
                    particleTask = particleTask.WriteWord(18, unchecked((ushort)(Signed(particleTask, 18) + y % 800 - 400)));
                    frame = DrawRandom(frame, out ushort z);
                    particleTask = particleTask.WriteWord(20, unchecked((ushort)(Signed(particleTask, 20) + z % 800 - 400)));
                    frame = DrawRandom(frame, out ushort size);
                    frame = Write(frame, child, particleTask.WriteWord(28, (ushort)((size & 2047) + 2048)));
                }
            }
        }
        if (age is >= 14 and <= 17)
        {
            frame = inputs.Execute(frame, new NativeBattleCannonBlow151AdjustShake((short)((age & 1) == 0 ? -64 : 64)));
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattleCannonBlow151ContactsAll(Parameters(frame).HeaderIndex7C));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleCannonBlow151Sound(resources.Sound, 0, 128));
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) > 40 ? 2 : 0);
    }

    private static (NativeBattleCannonBlow151Frame Frame, uint Address) Spawn(NativeBattleCannonBlow151Frame frame, uint callback, short index)
    {
        (frame, uint child) = RequireAppend(frame, callback);
        return (Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)index))), child);
    }

    private static NativeBattleCannonBlow151Frame WriteTaskView(NativeBattleCannonBlow151Frame frame, uint address, int offset, NativeBattleEffectArena view, int length) => Write(frame, address, CopyTask(Read(frame, address), offset, view, length));

    public static void RequireLane(short index)
    {
        if (index != 0)
        {
            throw new NotSupportedException("Cannon Blow requires its actually produced trajectory lane zero.");
        }
    }
}
