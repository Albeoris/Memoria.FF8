using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleDrawSlot[] ReadEnemyDrawSlots(int modelId, int level)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        int information = EnemyDrawInformation(bytes);
        int band = level < bytes[information + 244] ? 0 : level < bytes[information + 245] ? 1 : 2;
        return ReadEnemyDrawTables(bytes, information)[band];
    }

    private NativeBattleDrawSlot[][] ReadEnemyDrawTables(int modelId)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        return ReadEnemyDrawTables(bytes, EnemyDrawInformation(bytes));
    }

    private static int EnemyDrawInformation(byte[] bytes)
    {
        int sections = bytes.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : 0;
        if (sections is not (2 or 11) || bytes.Length < (sections == 11 ? 32 : 8))
        {
            throw new InvalidDataException("Enemy Draw requires its original DAT information section.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 284)
        {
            throw new InvalidDataException("Enemy Draw information exceeds its original DAT section.");
        }
        return information;
    }

    private static NativeBattleDrawSlot[][] ReadEnemyDrawTables(byte[] bytes, int information)
    {
        return Enumerable.Range(0, 3).Select(band => Enumerable.Range(0, 4).Select(slot => new NativeBattleDrawSlot(bytes[information + 260 + 8 * band + 2 * slot], bytes[information + 261 + 8 * band + 2 * slot])).ToArray()).ToArray();
    }
}
