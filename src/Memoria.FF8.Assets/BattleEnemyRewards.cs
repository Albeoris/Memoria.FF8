using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class BattleEnemyRewards
{
    public static BattleEnemyRewardDefinition Read(byte[] originalDat)
    {
        int sections = BinaryPrimitives.ReadInt32LittleEndian(originalDat);
        if (sections is not (2 or 11))
        {
            throw new NotSupportedException("Enemy rewards require the original monster information section.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(originalDat.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > originalDat.Length - 336)
        {
            throw new InvalidDataException("Enemy reward records exceed the original monster information section.");
        }
        ReadOnlySpan<byte> source = originalDat.AsSpan(information);
        BattleStock[] drops = new BattleStock[12];
        for (int index = 0; index < drops.Length; index++)
        {
            drops[index] = new BattleStock(source[308 + 2 * index], source[309 + 2 * index]);
        }
        return new BattleEnemyRewardDefinition(BinaryPrimitives.ReadUInt16LittleEndian(source[256..]), BinaryPrimitives.ReadUInt16LittleEndian(source[258..]), source[244], source[245], source[248], (source[254] & 128) != 0, source[333], drops.ToImmutableArray());
    }
}
