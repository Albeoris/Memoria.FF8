using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeMovieCameraReader
{
    public static NativeMovieCameraFlags Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 96 || (bytes.Length - 8) % 44 != 0 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6, 2)) != (bytes.Length - 8) / 44 - 1)
        {
            throw new NotSupportedException("Native CAM requires its original eight-byte header and complete 44-byte record bank.");
        }
        byte[] flags = new byte[(bytes.Length - 8) / 44];
        for (int index = 0; index < flags.Length; index++)
        {
            flags[index] = bytes[8 + index * 44 + 40];
        }
        NativeMovieCameraFlags result = new(1, Convert.ToHexString(SHA256.HashData(bytes)), flags);
        result.Validate();
        return result;
    }
}
