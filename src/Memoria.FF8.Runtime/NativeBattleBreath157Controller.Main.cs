namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath157Controller
{
    public static (NativeBattleBreath157Frame Frame, int Result) AdvanceMain(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        short index = Signed(Read(frame, address), 14);
        if ((uint)index >= 3)
        {
            throw new NotSupportedException("Breath main leaves its three physical travel points.");
        }
        if (age == 0)
        {
            uint point = 0x23ECE00u + (uint)(index * 8);
            frame = Word(Word(Word(frame, point, 0), point + 2, 0), point + 4, 0);
            (frame, uint child) = RequireAppend(frame, 0x6049F0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(28, 16384).WriteWord(30, 2048));
        }
        if (age == 8)
        {
            (frame, uint child) = RequireAppend(frame, 0x604B30);
            int length = unchecked((int)frame.State.ReadDword(0x23EB5F4));
            int distance = (unchecked(length * 11 * 256) >> 12) / 4;
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)index).WriteWord(28, 3328).WriteWord(20, unchecked((ushort)distance)));
            frame = Write(frame, address, Read(frame, address).WriteWord(20, unchecked((ushort)distance)));
        }
        if (age == 9)
        {
            (frame, uint child) = RequireAppend(frame, 0x604C80);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)index).WriteWord(20, Read(frame, address).ReadWord(20)));
        }
        if (age == 12)
        {
            (frame, uint first) = RequireAppend(frame, 0x604E90);
            frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(14, (ushort)index));
            frame = DrawRandom(frame, out ushort angle);
            frame = Write(frame, first, Read(frame, first).WriteWord(16, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out ushort increment);
            frame = Write(frame, first, Read(frame, first).WriteWord(18, (ushort)(increment % 70 + 25)));
            frame = DrawRandom(frame, out ushort offset);
            int length = unchecked((int)frame.State.ReadDword(0x23EB5F4));
            int distance = (unchecked((length + offset % 600 - 300) * 5 * 256) >> 12) / 5;
            frame = Write(frame, first, Read(frame, first).WriteWord(20, unchecked((ushort)distance)));
            frame = DrawRandom(frame, out angle);
            frame = Write(frame, first, Read(frame, first).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out increment);
            frame = Write(frame, first, Read(frame, first).WriteWord(26, (ushort)(increment % 70 + 25)));
            frame = DrawRandom(frame, out ushort size);
            ushort scale = (ushort)((size % 1536 + 1792) / 8);
            frame = Write(frame, first, Read(frame, first).WriteWord(28, scale).WriteWord(30, scale));
            (frame, uint second) = RequireAppend(frame, 0x6050A0);
            frame = Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(14, (ushort)index).WriteWord(28, scale).WriteWord(30, scale));
            frame = DrawRandom(frame, out angle);
            frame = Write(frame, second, Read(frame, second).WriteWord(24, (ushort)(angle & 4095)));
            frame = DrawRandom(frame, out increment);
            frame = Write(frame, second, Read(frame, second).WriteWord(26, (ushort)(increment % 50 + 20)));
            frame = DrawRandom(frame, out angle);
            frame = Write(frame, second, Read(frame, second).WriteWord(32, (ushort)(angle & 4095)));
        }
        if (age == 15)
        {
            (frame, uint child) = RequireAppend(frame, 0x6052A0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 22 ? 2 : 0);
    }
}
