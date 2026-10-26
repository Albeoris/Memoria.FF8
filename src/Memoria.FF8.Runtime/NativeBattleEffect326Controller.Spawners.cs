namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame InitializeOpening62B010(NativeBattleEffect326Frame frame)
    {
        (frame, uint first) = AppendRequired326(frame, 0x62B080);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0xE4A8).WriteWord(0x14, 0xEE6C).WriteWord(0x18, 0x400).WriteWord(0x1C, 0x3000));
        (frame, uint second) = AppendRequired326(frame, 0x62B4F0);
        return Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0xE570).WriteWord(0x14, 0xEE6C).WriteWord(0x18, 0x400).WriteWord(0x1C, 0x3000));
    }

    public static NativeBattleEffect326Frame InitializeScrollBurst62BA60(NativeBattleEffect326Frame frame)
    {
        (frame, uint warp) = AppendRequired326(frame, 0x62BB40);
        NativeCrtRandomState random = frame.Random.Draw(out ushort phase);
        frame = frame with { Random = random };
        frame = Write(frame, warp, Read(frame, warp).WriteWord(12, 0).WriteWord(0x18, (ushort)(phase & 0xFFF)));
        (frame, uint scroll) = AppendRequired326(frame, 0x62BC70);
        frame = Write(frame, scroll, Read(frame, scroll).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 0).WriteWord(0x1C, 0xB00));
        (frame, uint first) = AppendRequired326(frame, 0x62BDA0);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(14, 1).WriteWord(0x10, 0xFFCE).WriteWord(0x12, 60).WriteWord(0x18, 0).WriteWord(0x1C, 0xC00).WriteWord(0x1E, 0xFFFC).WriteWord(0x20, 0x500));
        (frame, uint second) = AppendRequired326(frame, 0x62BDA0);
        return Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(14, 1).WriteWord(0x10, 90).WriteWord(0x12, 10).WriteWord(0x18, 0x800).WriteWord(0x1C, 0x1200).WriteWord(0x1E, 0xFFFA).WriteWord(0x20, 0x700));
    }

    public static NativeBattleEffect326Frame InitializeRings62E810(NativeBattleEffect326Frame frame)
    {
        (frame, uint first) = AppendRequired326(frame, 0x62E8C0);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 40));
        NativeCrtRandomState random = frame.Random.Draw(out ushort phase);
        frame = frame with { Random = random };
        frame = Write(frame, first, Read(frame, first).WriteWord(0x18, (ushort)(phase & 0xFFF)).WriteWord(0x1C, 0xE00).WriteWord(0x20, 0x555).WriteWord(0x22, 0x555));
        (frame, uint second) = AppendRequired326(frame, 0x62EA10);
        frame = Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 40));
        random = frame.Random.Draw(out phase);
        frame = frame with { Random = random };
        return Write(frame, second, Read(frame, second).WriteWord(0x18, (ushort)(phase & 0xFFF)).WriteWord(0x1C, 0x500).WriteWord(0x1E, 0x500).WriteWord(0x20, 0x200));
    }

    private static (NativeBattleEffect326Frame Frame, uint Address) AppendRequired326(NativeBattleEffect326Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, NativeBattleEffect326Profile.ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect326 exhausted its required child bank.");
        }
        return (frame, address);
    }
}
