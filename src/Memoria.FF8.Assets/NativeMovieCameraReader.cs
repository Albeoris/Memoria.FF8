using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeMovieCameraReader
{
    private const int HeaderSize = 8;
    private const int RecordSize = 44;
    private const int CameraSize = 40;

    public static NativeMovieCameraFlags Read(ReadOnlySpan<byte> bytes)
    {
        return Read(bytes, allowRecordCountHeader: false);
    }

    /// <summary>Replacement CAM authors may store the record count instead of the original terminal index.</summary>
    public static NativeMovieCameraFlags ReadPresentation(ReadOnlySpan<byte> bytes)
    {
        return Read(bytes, allowRecordCountHeader: true);
    }

    public static FieldCamera ReadCamera(ReadOnlySpan<byte> bytes, int record)
    {
        if (record < 0 || HeaderSize + (record + 1L) * RecordSize > bytes.Length)
        {
            throw new InvalidDataException("Native movie camera record is outside its original CAM bank.");
        }
        return FieldCamera.Read(bytes.Slice(HeaderSize + record * RecordSize, CameraSize).ToArray());
    }

    private static NativeMovieCameraFlags Read(ReadOnlySpan<byte> bytes, bool allowRecordCountHeader)
    {
        int records = (bytes.Length - HeaderSize) / RecordSize;
        int header = bytes.Length >= HeaderSize ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6, 2)) : -1;
        if (records < 2 || (bytes.Length - HeaderSize) % RecordSize != 0 || header < 1 || header >= records && (!allowRecordCountHeader || header != records))
        {
            throw new NotSupportedException("Native CAM requires its original eight-byte header and complete 44-byte record bank.");
        }
        // Native55A140/55A620 uses the header terminal, not the physical bank size.
        // Preserve source authentication across any complete unused trailing records.
        int playableRecords = allowRecordCountHeader && header == records ? records : header + 1;
        byte[] flags = new byte[playableRecords];
        for (int index = 0; index < flags.Length; index++)
        {
            flags[index] = bytes[HeaderSize + index * RecordSize + CameraSize];
        }
        NativeMovieCameraFlags result = new(1, Convert.ToHexString(SHA256.HashData(bytes)), flags);
        result.Validate();
        return result;
    }
}
