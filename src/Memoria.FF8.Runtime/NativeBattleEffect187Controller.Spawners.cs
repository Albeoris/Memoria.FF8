using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Frame InitializeOpening647960(NativeBattleEffect187Frame frame)
    {
        (frame, uint first) = AppendRequired(frame, 0x6479D0);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0xE4A8).WriteWord(0x14, 0xEE6C).WriteWord(0x18, 0x400).WriteWord(0x1C, 0x3000));
        (frame, uint second) = AppendRequired(frame, 0x647E40);
        return Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0xE570).WriteWord(0x14, 0xEE6C).WriteWord(0x18, 0x400).WriteWord(0x1C, 0x3000));
    }

    public static NativeBattleEffect187Frame InitializeScrollBurst6483B0(NativeBattleEffect187Frame frame)
    {
        (frame, uint warp) = AppendRequired(frame, 0x648490);
        NativeCrtRandomState random = frame.Random.Draw(out ushort phase);
        frame = frame with { Random = random };
        frame = Write(frame, warp, Read(frame, warp).WriteWord(12, 0).WriteWord(0x18, (ushort)(phase & 0xFFF)));
        (frame, uint scroll) = AppendRequired(frame, 0x6485C0);
        frame = Write(frame, scroll, Read(frame, scroll).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 0).WriteWord(0x1C, 0xB00));
        (frame, uint first) = AppendRequired(frame, 0x6486F0);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(14, 1).WriteWord(0x10, 0xFFCE).WriteWord(0x12, 60).WriteWord(0x18, 0).WriteWord(0x1C, 0xC00).WriteWord(0x1E, 0xFFFC).WriteWord(0x20, 0x500));
        (frame, uint second) = AppendRequired(frame, 0x6486F0);
        return Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(14, 1).WriteWord(0x10, 90).WriteWord(0x12, 10).WriteWord(0x18, 0x800).WriteWord(0x1C, 0x1200).WriteWord(0x1E, 0xFFFA).WriteWord(0x20, 0x700));
    }

    public static NativeBattleEffect187Frame InitializeRings64A690(NativeBattleEffect187Frame frame)
    {
        (frame, uint first) = AppendRequired(frame, 0x64A740);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 40));
        NativeCrtRandomState random = frame.Random.Draw(out ushort phase);
        frame = frame with { Random = random };
        frame = Write(frame, first, Read(frame, first).WriteWord(0x18, (ushort)(phase & 0xFFF)).WriteWord(0x1C, 0xE00).WriteWord(0x20, 0x555).WriteWord(0x22, 0x555));
        (frame, uint second) = AppendRequired(frame, 0x64A890);
        frame = Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 40));
        random = frame.Random.Draw(out phase);
        frame = frame with { Random = random };
        return Write(frame, second, Read(frame, second).WriteWord(0x18, (ushort)(phase & 0xFFF)).WriteWord(0x1C, 0x500).WriteWord(0x1E, 0x500).WriteWord(0x20, 0x200));
    }

    public static NativeBattleEffect187Frame InitializeBodyBurst64B040(NativeBattleEffect187Frame frame)
    {
        (frame, uint tint) = AppendRequired(frame, 0x64B0A0);
        frame = Write(frame, tint, Read(frame, tint).WriteWord(12, 0).WriteWord(14, 0));
        (frame, uint emit) = AppendRequired(frame, 0x64B130);
        frame = Write(frame, emit, Read(frame, emit).WriteWord(12, 0).WriteWord(14, 0xA10).WriteWord(0x1C, 255));
        (frame, uint mesh) = AppendRequired(frame, 0x64B300);
        return Write(frame, mesh, Read(frame, mesh).WriteWord(12, 0).WriteWord(0x1C, 0x400).WriteWord(0x20, 0x400));
    }

    public static NativeBattleEffect187Step AdvanceTint64B0A0(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Inputs inputs)
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
        frame = frame with { State = frame.State.WriteDword(0x24FD900, unchecked((lane << 16) | (lane << 8) | lane)) };
        if (paused)
        {
            return new(frame, 0);
        }
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, next)), unchecked((short)next) >= 11 ? 2 : 0);
    }

    private static (NativeBattleEffect187Frame Frame, uint Address) AppendRequired(NativeBattleEffect187Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect187 exhausted its mandatory child task bank.");
        }
        return (frame, address);
    }
}
