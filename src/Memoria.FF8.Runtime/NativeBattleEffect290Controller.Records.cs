using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect290Controller
{
    public static (NativeBattleEffect290Frame Frame, uint Address) AllocateRecord675FD0(NativeBattleEffect290Frame frame, uint main)
    {
        uint first = main + 0x1D8;
        uint last = main + 0x5E5C;
        uint row = Read(frame, main).ReadDword(0x64D8);
        if (row < first || row > last || (row - first) % 124 != 0)
        {
            throw new InvalidDataException("Effect290 record hint leaves its current native bank.");
        }
        if (Word290(ReadBytes(frame, row + 6, 2), 0) != 0)
        {
            row = first;
            while (Word290(ReadBytes(frame, row + 6, 2), 0) != 0)
            {
                row += 124;
                if (row > last)
                {
                    return (frame, 0);
                }
            }
        }
        frame = Write(frame, main, Read(frame, main).WriteDword(0x64D8, row >= last ? first : row + 124));
        NativeBattleEffect290RandomStep random = InternalRandom676080(frame);
        frame = random.Frame;
        uint source = (random.Value >> 13) switch
        {
            0 => 0x1067400,
            1 => 0x10674B8,
            2 => 0x1067570,
            3 => 0x1067628,
            _ => throw new InvalidDataException("Effect290 internal random value leaves its native selector.")
        };
        frame = WriteBytes(frame, row + 24, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(source)), 4);
        return (frame, row);
    }

    public static NativeBattleEffect290RandomStep InternalRandom676080(NativeBattleEffect290Frame frame)
    {
        uint value = unchecked(frame.State.ReadDword(InternalRandomGlobal) * 125u + 14u) & 0x7FFF;
        return new(frame with { State = frame.State.WriteDword(InternalRandomGlobal, value) }, (int)value);
    }
}
