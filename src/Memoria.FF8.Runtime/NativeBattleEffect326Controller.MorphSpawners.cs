namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame InitializeMorph62F1C0(NativeBattleEffect326Frame frame)
    {
        (frame, uint tint) = AppendRequired326(frame, 0x62F220);
        frame = Write(frame, tint, Read(frame, tint).WriteWord(12, 0).WriteWord(14, 0));
        (frame, uint emit) = AppendRequired326(frame, 0x62F2B0);
        frame = Write(frame, emit, Read(frame, emit).WriteWord(12, 0).WriteWord(14, 0xA10).WriteWord(0x1C, 255));
        (frame, uint mesh) = AppendRequired326(frame, 0x62F400);
        return Write(frame, mesh, Read(frame, mesh).WriteWord(12, 0).WriteWord(0x1C, 0x400).WriteWord(0x20, 0x400));
    }

    public static NativeBattleEffect326Step AdvanceTint62F220(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool paused = (inputs.ReadFlags(frame) & 0x201) != 0;
        if (Signed(task, 14) > 0)
        {
            if (!paused)
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))));
            }
            return new(frame, 0);
        }
        int age = Signed(task, 12);
        uint lane = unchecked((uint)(age < 1 ? 0 : 12 * (age - 1)));
        frame = frame with { State = frame.State.WriteDword(0x24F1290, unchecked((lane << 16) | (lane << 8) | lane)) };
        if (paused)
        {
            return new(frame, 0);
        }
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) >= 11)
        {
            frame = frame with { State = frame.State.WriteDword(0x24F1290, 0x00808080) };
            return new(frame, 2);
        }
        return new(frame, 0);
    }

}
