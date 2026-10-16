using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeMovieCameraReader
{
    public static NativeMovieCameraFlags Read(ReadOnlySpan<byte> bytes)
    {
        return Read(bytes, allowRecordCountHeader: false);
    }

    /// <summary>Replacement CAM authors may store the record count instead of the original terminal index.</summary>
    public static NativeMovieCameraFlags ReadPresentation(ReadOnlySpan<byte> bytes)
    {
        return Read(bytes, allowRecordCountHeader: true);
    }

    private static NativeMovieCameraFlags Read(ReadOnlySpan<byte> bytes, bool allowRecordCountHeader)
    {
        int records = (bytes.Length - 8) / 44;
        int header = bytes.Length >= 8 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6, 2)) : -1;
        if (bytes.Length < 96 || (bytes.Length - 8) % 44 != 0 || header < 1 || header >= records && (!allowRecordCountHeader || header != records))
        {
            throw new NotSupportedException("Native CAM requires its original eight-byte header and complete 44-byte record bank.");
        }
        // Native55A140/55A620 uses the header terminal, not the physical bank size.
        // Preserve source authentication across any complete unused trailing records.
        int playableRecords = allowRecordCountHeader && header == records ? records : header + 1;
        byte[] flags = new byte[playableRecords];
        for (int index = 0; index < flags.Length; index++)
        {
            flags[index] = bytes[8 + index * 44 + 40];
        }
        NativeMovieCameraFlags result = new(1, Convert.ToHexString(SHA256.HashData(bytes)), flags);
        result.Validate();
        return result;
    }
}
