using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>An original B84CCC file-directory selection read through the battle archive.</summary>
public sealed record NativeBattleArchiveFile(ushort FileId, string Name, ImmutableArray<byte> Bytes)
{
    public int Section(int index) => NativeBattleDatDirectory.Section(Bytes.AsSpan(), index);
}

public static class NativeBattleDatDirectory
{
    public static int Section(ReadOnlySpan<byte> bytes, int index)
    {
        if (bytes.Length < 8)
        {
            throw new InvalidDataException("Native battle DAT directory requires its complete original resource.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (count is < 1 or > 16 || bytes.Length < 8 + count * 4 || index < 0 || index > count)
        {
            throw new InvalidDataException("Native battle DAT directory is incomplete or its section selection is invalid.");
        }
        int previous = 8 + count * 4;
        for (int section = 0; section < count; section++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes[(4 + section * 4)..]);
            if (offset < previous || offset > bytes.Length)
            {
                throw new InvalidDataException("Native battle DAT has an invalid section offset.");
            }
            previous = offset;
        }
        // 507120 consumes section offsets; 482610/52D400 obtain the loaded extent from the file itself.
        return index == count ? bytes.Length : BinaryPrimitives.ReadInt32LittleEndian(bytes[(4 + index * 4)..]);
    }
}
