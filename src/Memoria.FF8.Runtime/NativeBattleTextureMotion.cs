using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTextureCopy(short SourceX, short SourceY, short Width, short Height, short DestinationX, short DestinationY);

public static class NativeBattleTextureMotion
{
    public static NativeBattleTextureCopy? Plan50C780(ImmutableArray<byte>? source, ushort? allocationMask, byte selector, byte variant)
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
        if (offset > table.Length - 10 - 2 * variant)
        {
            throw new InvalidDataException("Native50C780 texture variant leaves its original byte descriptor.");
        }
        ReadOnlySpan<byte> data = table.AsSpan().Slice(offset);
        ushort mask = allocationMask ?? throw new NotSupportedException("Native50C780 requires the model resource's actual texture allocation WORD2.");
        int skip = data[0];
        int selected = -1;
        for (int slot = 0; slot < 12; slot++)
        {
            if ((mask & (1 << slot)) != 0 && skip-- == 0)
            {
                selected = slot;
                break;
            }
        }
        if (selected < 0)
        {
            throw new NotSupportedException("Native50C780 texture-motion ordinal does not identify a proved allocated slot.");
        }
        int x = (selected / 2 + 10) * 64;
        int y = (selected & 1) * 128;
        return new(checked((short)(x + data[8 + 2 * variant])), checked((short)(y + data[9 + 2 * variant])), data[3], data[4], checked((short)(x + data[1])), checked((short)(y + data[2])));
    }
}
