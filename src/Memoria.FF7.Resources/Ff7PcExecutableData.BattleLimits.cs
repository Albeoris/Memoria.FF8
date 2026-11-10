using System.Reflection.PortableExecutable;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<byte> LoadBeatRushReel(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        return Array.AsReadOnly(ReadVirtual(stream, pe, 0x91EAD0, 16));
    }

    public static IReadOnlyList<Ff7BattleAttack> LoadBattleLimits(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        const int limitCount = 0x38;
        byte[] records = ReadVirtual(stream, pe, 0x91F6D4, limitCount * Ff7BattleAttack.RecordSize);
        Ff7BattleAttack[] attacks = new Ff7BattleAttack[limitCount];
        for (int index = 0; index < attacks.Length; index++)
        {
            attacks[index] = Ff7BattleAttack.Read(records.AsSpan(index * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
        }
        return Array.AsReadOnly(attacks);
    }
}
