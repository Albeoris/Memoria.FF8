namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWildCannonBlow156Controller
{
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) AdvanceMain(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        short index = Signed(Read(frame, address), 14);
        if ((uint)index >= 4)
        {
            throw new NotSupportedException("Wild Cannon Blow main leaves its four physical trajectory rows.");
        }
        if (age == 0)
        {
            frame = PrepareTrajectory(frame, index, inputs);
            (frame, uint child) = Spawn(frame, 0x605DF0, index);
            frame = DrawRandom(frame, out ushort angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out ushort size);
            ushort quarter = (ushort)((size % 768 + 2048) / 4);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, quarter).WriteWord(30, quarter));
            (frame, child) = Spawn(frame, 0x605F40, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(1 << index)));
            (frame, child) = Spawn(frame, 0x606200, index);
            frame = DrawRandom(frame, out angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out size);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, (ushort)(size % 1536 + 6400)));
            (frame, child) = Spawn(frame, 0x606320, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, 0).WriteWord(26, 5));
            frame = DrawRandom(frame, out size);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, (ushort)((size & 511) + 3072)));
            (frame, child) = Spawn(frame, 0x6063E0, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(1 << index)).WriteWord(28, 0).WriteWord(30, 5));
            (frame, child) = Spawn(frame, 0x6065A0, index);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(256 << index)).WriteWord(28, 0).WriteWord(30, 5));
        }
        if (age == 13)
        {
            uint target = 0x23F0318u + (uint)(index * 8);
            (frame, uint child) = Spawn(frame, 0x6067B0, index);
            frame = WriteTaskView(frame, child, 16, frame.State.ReadView(target, 8), 8);
            frame = DrawRandom(frame, out ushort angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out ushort speed);
            frame = Write(frame, child, Read(frame, child).WriteWord(26, (ushort)(speed % 140 + 80)));
            frame = DrawRandom(frame, out ushort size);
            ushort quarter = (ushort)((size % 1280 + 3584) / 4);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, quarter).WriteWord(30, quarter));
            (frame, child) = Spawn(frame, 0x606910, index);
            frame = WriteTaskView(frame, child, 16, frame.State.ReadView(target, 8), 8);
            frame = DrawRandom(frame, out angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out size);
            quarter = (ushort)(((size & 1023) + 7680) / 4);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, quarter).WriteWord(30, quarter));
        }
        if (age == 15)
        {
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
            int count = header.ReadByte(16);
            int first = count == 0 ? 0 : header.FirstContactIndex ?? throw new NotSupportedException("Wild Cannon Blow requires its physical contact rows.");
            for (int contact = 0; contact < count; contact++)
            {
                int actor = inputs.Packets.ReadContact(first + contact).Primary.ActorIndex;
                (frame, uint child) = RequireAppend(frame, 0x606A60);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(24, (ushort)(256 << index)));
                NativeBattleWildCannonBlow156PointStep bone = inputs.ReadBone502170(frame, actor, 0xF1, 0);
                frame = WriteTaskView(bone.Frame, child, 16, bone.Point8, 6);
            }
        }
        if (age is >= 13 and <= 16)
        {
            frame = inputs.Execute(frame, new NativeBattleWildCannonBlow156AdjustShake((short)((age & 1) == 0 ? -64 : 64)));
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 20 ? 2 : 0);
    }

    private static (NativeBattleWildCannonBlow156Frame Frame, uint Address) Spawn(NativeBattleWildCannonBlow156Frame frame, uint callback, short index)
    {
        (frame, uint child) = RequireAppend(frame, callback);
        return (Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)index))), child);
    }

    private static NativeBattleWildCannonBlow156Frame WriteTaskView(NativeBattleWildCannonBlow156Frame frame, uint address, int offset, NativeBattleEffectArena view, int length)
    {
        (byte[] bytes, bool[] known) = view.ReadSnapshot(0, length);
        NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return Write(frame, address, task);
    }
}
