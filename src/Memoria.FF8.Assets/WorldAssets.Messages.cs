using System.Buffers.Binary;
using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    public string ReadWorldMessage(int message)
    {
        //542DA0 binds20400C4 to zero-based section13;5438D0 reads its relative DWORD offsets.
        ReadOnlySpan<byte> section = wmset.AsSpan(sections[13], sections[14] - sections[13]);
        int tableBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section));
        if (tableBytes < 4 || tableBytes % 4 != 0 || tableBytes > section.Length || message < 0 || message >= tableBytes / 4)
        {
            throw new InvalidDataException("Original world message index or pointer table is invalid.");
        }
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section.Slice(message * 4, 4)));
        int end = section.Slice(start).IndexOf((byte)0);
        if (start < tableBytes || end < 0)
        {
            throw new InvalidDataException("Original world message has no bounded text terminator.");
        }
        return FF8Encoding.CreateEuropean().GetString(section.Slice(start, end));
    }
}
