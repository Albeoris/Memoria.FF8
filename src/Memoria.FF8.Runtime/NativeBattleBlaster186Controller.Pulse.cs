namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlaster186Controller
{
    public static (NativeBattleBlaster186Frame Frame, int Result) AdvancePulse(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age is >= 0 and <= 12 && (age & 1) != 0)
        {
            (frame, uint child) = RequireAppend(frame, 0x5C0280);
            frame = DrawRandom(frame, out ushort duration);
            short lifetime = (short)(duration % 10 + 10);
            short spin = (short)((task.ReadByte(14) & 1) == 0 ? 512 : -512);
            frame = DrawRandom(frame, out ushort phase);
            frame = DrawRandom(frame, out ushort scale);
            short size = (short)(scale % 0xA00 + 0xB00);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)lifetime)).WriteWord(24, unchecked((ushort)spin)).WriteWord(26, (ushort)(phase % 5)).WriteWord(28, unchecked((ushort)size)).WriteWord(30, unchecked((ushort)(size / 4))).WriteWord(32, unchecked((ushort)(lifetime - 4))).WriteWord(34, 4));
            frame = Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)(task.ReadWord(14) + 1))));
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 30 ? 2 : 0);
    }
}
