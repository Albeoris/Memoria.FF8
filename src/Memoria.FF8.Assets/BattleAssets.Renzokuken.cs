using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public ImmutableArray<ushort> ReadNativeRenzokukenMagicIds(int modelId)
    {
        byte[] data = Read($"c0m{modelId:000}.dat");
        int sections = BinaryPrimitives.ReadInt32LittleEndian(data);
        if (sections is not (2 or 11))
        {
            throw new NotSupportedException("Native Renzokuken selection requires the original enemy information section.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > data.Length - 350)
        {
            throw new InvalidDataException("Native Renzokuken selector WORDs exceed the original enemy information record.");
        }
        //48D200 reaches indexes2..6 from crisis1..4 plus48F120(2); retain their actual indexed positions.
        return Enumerable.Range(0, 7).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(information + 336 + 2 * index))).ToImmutableArray();
    }
}
