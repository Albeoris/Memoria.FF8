using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTextureCopy(short SourceX, short SourceY, short Width, short Height, short DestinationX, short DestinationY);
public sealed record NativeBattleTextureMotionStep(ImmutableArray<byte> Source, ImmutableArray<NativeBattleTextureCopy> Copies);

public static class NativeBattleTextureMotion
{
    public static NativeBattleTextureMotionStep Advance50C950(ImmutableArray<byte> source, ushort? allocationMask, byte selector, bool loaded)
    {
        if (source.IsDefault)
        {
            throw new InvalidDataException("Native50C950 texture table is uninitialized.");
        }
        if (source.IsEmpty)
        {
            return new(source, []);
        }
        int directory = 2 * selector;
        if (directory > source.Length - 2)
        {
            throw new InvalidDataException("Native50C950 selector leaves its actual WORD directory.");
        }
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(directory));
        if (offset == 0)
        {
            return new(source, []);
        }
        if (offset > source.Length - 8)
        {
            throw new InvalidDataException("Native50C950 descriptor leaves its actual eight-byte header.");
        }
        ReadOnlySpan<byte> descriptor = source.AsSpan().Slice(offset);
        int count = descriptor[5];
        int speed = descriptor[6];
        if (count == 0)
        {
            int slot = TextureSlot(allocationMask, descriptor[0]);
            short x = unchecked((short)(descriptor[1] + (slot / 2 + 10) * 64));
            short y = unchecked((short)(descriptor[2] + (slot & 1) * 128));
            short shiftedY = unchecked((short)(y - speed));
            return new(source, [new(x, y, descriptor[3], descriptor[4], x, shiftedY), new(x, shiftedY, descriptor[3], descriptor[4], x, unchecked((short)(shiftedY + descriptor[4])))]);
        }
        int prior = speed == 8 ? descriptor[7] : descriptor[7] >> 3;
        byte next = unchecked((byte)(speed == 8 ? (descriptor[7] + 1) % (count + 1) : (descriptor[7] + speed) % (8 * count + 8)));
        int variant = speed == 8 ? next : next >> 3;
        byte[] changed = source.ToArray();
        changed[offset + 7] = next;
        ImmutableArray<byte> result = changed.ToImmutableArray();
        NativeBattleTextureCopy? copy = loaded && variant != prior ? Plan50C780(result, allocationMask, selector, variant) : null;
        return new(result, copy is null ? [] : [copy]);
    }

    public static NativeBattleTextureCopy? Plan50C780(ImmutableArray<byte>? source, ushort? allocationMask, byte selector, int variant)
    {
        ImmutableArray<byte> table = source ?? throw new NotSupportedException("Native50C780 requires its original resource DWORD48 texture-motion table; unknown is distinct from a NULL table.");
        if (table.IsDefault)
        {
            throw new InvalidDataException("Native texture-motion table cannot be an uninitialized immutable array.");
        }
        if (table.IsEmpty)
        {
            return null;
        }
        int directory = 2 * selector;
        if (directory > table.Length - 2)
        {
            throw new InvalidDataException("Native50C780 texture selector leaves its original WORD directory.");
        }
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan().Slice(directory));
        if (offset == 0)
        {
            return null;
        }
        long variantOffset = offset + 8L + 2L * variant;
        if (offset > table.Length - 5 || variantOffset < 0 || variantOffset > table.Length - 2)
        {
            throw new InvalidDataException("Native50C780 texture variant leaves its original byte descriptor.");
        }
        ReadOnlySpan<byte> data = table.AsSpan().Slice(offset);
        int selected = TextureSlot(allocationMask, data[0]);
        int x = (selected / 2 + 10) * 64;
        int y = (selected & 1) * 128;
        return new(checked((short)(x + table[(int)variantOffset])), checked((short)(y + table[(int)variantOffset + 1])), data[3], data[4], checked((short)(x + data[1])), checked((short)(y + data[2])));
    }

    private static int TextureSlot(ushort? allocationMask, int skip)
    {
        ushort mask = allocationMask ?? throw new NotSupportedException("Native texture motion requires the model resource's actual texture allocation WORD2.");
        for (int slot = 0; slot < 12; slot++)
        {
            if ((mask & (1 << slot)) != 0 && skip-- == 0)
            {
                return slot;
            }
        }
        throw new NotSupportedException("Native texture-motion ordinal does not identify a proved allocated slot.");
    }
}
