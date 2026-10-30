using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class OriginalSavePreviewSequence
{
    public static byte[] Refresh(byte[] content, string destinationPath)
    {
        byte[] block = NativePcSaveCodec.Decode(content).Block;
        string directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath))!;
        uint maximum = 0;
        for (int bank = 1; bank <= 2; bank++)
        {
            for (int number = 1; number <= 30; number++)
            {
                string path = Path.Combine(directory, $"slot{bank}_save{number:00}.ff8");
                if (!File.Exists(path))
                {
                    continue;
                }
                try
                {
                    byte[] saved = NativePcSaveCodec.Decode(File.ReadAllBytes(path)).Block;
                    uint serial = BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(460));
                    maximum = Math.Max(maximum, serial);
                }
                catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    // Native enumeration treats unreadable or invalid slot images as empty.
                }
            }
        }
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(394));
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(394), unchecked((ushort)(count + 1)));
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(460), unchecked(maximum + 1));
        return NativePcSaveCodec.Encode(new NativePcSaveData(block));
    }
}
