using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect321Controller
{
    public static NativeBattleEffect321Step AdvanceRoot598D00(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, (age & 1) == 0 ? FirstPacketAddress : SecondPacketAddress) };
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        if (survivors == 0)
        {
            (frame, uint cleanup) = AppendRequired(frame, CleanupCallback);
            frame = Write(frame, cleanup, Read(frame, cleanup).WriteWord(12, 0).WriteWord(14, 0));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect321Step AdvanceCleanup598D60(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect321AppendTextureLoad508630(FirstPacketAddress, address + 14));
        }
        bool complete = Read(frame, address).ReadWord(14) != 0;
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return new(frame, complete ? 2 : 0);
    }
}
