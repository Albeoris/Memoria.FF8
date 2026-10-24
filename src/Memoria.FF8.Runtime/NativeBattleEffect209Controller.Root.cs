using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect209Controller
{
    public static NativeBattleEffect209Step AdvanceRoot639100(NativeBattleEffect209Frame frame, uint address, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        uint bank = frame.State.ReadDword(ArenaGlobal) + ((Read(frame, address).ReadByte(12) & 1) != 0 ? 0x10000u : 0u);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, bank) };
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        if (survivors == 0)
        {
            (frame, uint cleanup) = Append(frame, RootDescriptor, CleanupCallback);
            if (cleanup == 0)
            {
                throw new InvalidDataException("Effect209 required cleanup allocation failed.");
            }
            frame = Write(frame, cleanup, Read(frame, cleanup).WriteWord(12, 0).WriteWord(14, 0));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect209Step AdvanceCleanup639170(NativeBattleEffect209Frame frame, uint address, NativeBattleEffect209Inputs inputs)
    {
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209CommonTextureLoad508630(frame.State.ReadDword(ArenaGlobal), address + 14));
        }
        ushort completion = Read(frame, address).ReadWord(14);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, completion != 0 ? 2 : 0);
    }
}
