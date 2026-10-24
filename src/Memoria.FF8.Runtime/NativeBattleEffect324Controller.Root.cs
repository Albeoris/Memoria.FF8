using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect324Controller
{
    public static NativeBattleEffect324Step AdvanceRoot5979A0(NativeBattleEffect324Frame frame, uint address, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint cursor = (root.ReadWord(12) & 1) == 0 ? FirstPacketAddress : SecondPacketAddress;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, cursor) };
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }
}
