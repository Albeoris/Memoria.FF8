using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageTextureStep(ImmutableArray<byte> Source, ImmutableArray<NativeBattleTextureCopy> Copies);

public static class NativeBattleStageTextureMotion
{
    /// <summary>51B0D0/50CB20 with original WORD-relative selector and native quarter-row256 offset.</summary>
    public static NativeBattleStageTextureStep Advance50CB20(ImmutableArray<byte>? original, ushort selector)
    {
        ImmutableArray<byte> source = original ?? throw new NotSupportedException("Native50CB20 requires its retained stage texture-animation resource.");
        ushort Word(int offset)
        {
            if (source.IsDefault || offset < 0 || offset > source.Length - 2)
            {
                throw new InvalidDataException("Native stage texture animation leaves its original WORD resource.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(offset));
        }
        int relative = Word(2 * selector);
        if (relative == 0)
        {
            return new(source, []);
        }
        int start = 2 * relative;
        if (start > source.Length - 12)
        {
            throw new InvalidDataException("Native50CB20 descriptor leaves the original stage table.");
        }
        short x = unchecked((short)Word(start));
        short y = unchecked((short)(Word(start + 2) + 256));
        short width = unchecked((short)Word(start + 4));
        short height = unchecked((short)Word(start + 6));
        byte count = source[start + 8];
        byte speed = source[start + 9];
        if (count == 0)
        {
            //50CA80 reuses its source rectangle with height replaced by the displaced strip size.
            return new(source, [new(x, y, width, height, x, unchecked((short)(y - speed))), new(x, unchecked((short)(y - speed)), width, speed, x, unchecked((short)(height + y - speed)))]);
        }
        ushort counter = Word(start + 10);
        ushort next = (ushort)((counter + speed) % (8 * count + 8));
        byte[] changed = source.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(start + 10), next);
        int frame = next >> 3;
        if (frame == counter >> 3)
        {
            return new(changed.ToImmutableArray(), []);
        }
        short sourceX = unchecked((short)Word(start + 12 + 4 * frame));
        short sourceY = unchecked((short)(Word(start + 14 + 4 * frame) + 256));
        return new(changed.ToImmutableArray(), [new(sourceX, sourceY, width, height, x, y)]);
    }
}
