using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291Animation(uint Address, ImmutableArray<byte> Bytes);

public static class NativeBattlePandemona291Animations
{
    public static NativeBattlePandemona291Animation Rewrite6F2E40(NativeBattlePandemona291Animation source) => Rewrite(source, true);
    public static NativeBattlePandemona291Animation Rewrite6F2E60(NativeBattlePandemona291Animation source) => Rewrite(source, false);

    private static NativeBattlePandemona291Animation Rewrite(NativeBattlePandemona291Animation source, bool addQuarterTurn)
    {
        if (source.Bytes.IsDefault || source.Bytes.Length < 8)
        {
            throw new InvalidDataException("Native Pandemona static animation requires its current complete header.");
        }
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan().Slice(4, 4));
        if (count == 0 || count > (uint)((source.Bytes.Length - 8) / 8))
        {
            throw new NotSupportedException("Native Pandemona static animation requires its actual nonzero bounded record count.");
        }
        byte[] bytes = source.Bytes.ToArray();
        for (uint index = 0; index < count; index++)
        {
            int record = checked(8 + (int)index * 8);
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(record + (addQuarterTurn ? 2 : 4), 2));
            if (addQuarterTurn)
            {
                value = unchecked((ushort)(value + 4096));
            }
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(record + 6, 2), value);
        }
        return source with { Bytes = bytes.ToImmutableArray() };
    }
}
