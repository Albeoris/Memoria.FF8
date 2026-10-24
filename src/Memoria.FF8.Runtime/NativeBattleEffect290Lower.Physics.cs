using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect290Lower
{
    public static NativeBattleEffect290Frame AdvanceFragments(NativeBattleEffect290Frame frame, uint main)
    {
        for (int index = 0; index < 192; index++)
        {
            uint address = checked(main + 0x1D8 + (uint)(index * 0x7C));
            NativeBattleEffectArena record = ReadBytes(frame, address, 0x7C);
            int life = Word290(record, 6);
            if (life == 0)
            {
                continue;
            }
            record = PutWord290(record, 6, life - 1);
            if (Word290(record, 6) == 0)
            {
                frame = WriteBytes(frame, address, record, 0x7C);
                frame = WriteBytes(frame, main + 0x64D8, Put290(NativeBattleEffectArena.Clear(), 0, address), 4);
                continue;
            }
            record = PutWord290(PutWord290(record, 0x10, Word290(record, 0x10) + Word290(record, 0x14)), 0x12, Word290(record, 0x12) + Word290(record, 0x16));
            for (int axis = 0; axis < 3; axis++)
            {
                record = PutWord290(record, axis * 2, Word290(record, axis * 2) + Word290(record, 8 + axis * 2));
            }
            record = PutWord290(record, 10, Word290(record, 10) + 20);
            if (Word290(record, 2) > 0)
            {
                record = PutWord290(PutWord290(PutWord290(record, 10, -Word290(record, 10) >> 1), 0x14, -Word290(record, 0x14)), 0x16, -Word290(record, 0x16));
            }
            frame = WriteBytes(frame, address, record, 0x7C);
        }
        return frame;
    }
}
