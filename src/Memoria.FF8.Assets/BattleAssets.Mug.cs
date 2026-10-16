using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleMugDefinition ReadEnemyNativeMugData(int modelId)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections is not (2 or 11))
        {
            throw new NotSupportedException("Enemy Mug requires the original monster information section.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 333)
        {
            throw new InvalidDataException("Enemy Mug information exceeds its original resource.");
        }
        ReadOnlySpan<byte> source = bytes.AsSpan(information);
        BattleStock[] items = new BattleStock[12];
        for (int slot = 0; slot < items.Length; slot++)
        {
            items[slot] = new(source[284 + 2 * slot], source[285 + 2 * slot]);
        }
        return new(source[332], source[244], source[245], items.ToImmutableArray());
    }
}
