using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect313Controller
{
    public static NativeBattleEffect313Step AdvanceRoot59CA20(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        short age = Signed(Read(frame, address), 12);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(frame.State.ReadDword(ArenaGlobal) + ((age & 1) == 0 ? 0u : 0x8000u))) };
        (frame, int children) = AdvancePool(frame, 1, resources, inputs);
        (frame, int records) = AdvanceRecords59CA70(frame, resources, inputs);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return new(frame, (children | records) == 0 ? 2 : 0);
    }

    public static NativeBattleEffect313Step AdvanceCleanup59C9E0(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Inputs inputs)
    {
        short age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteWord(14, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect313AppendTextureLoad508630(checked(frame.State.ReadDword(ArenaGlobal) + 0x10000), address + 14));
        }
        bool complete = Read(frame, address).ReadWord(14) != 0;
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return new(frame, complete ? 2 : 0);
    }
}
