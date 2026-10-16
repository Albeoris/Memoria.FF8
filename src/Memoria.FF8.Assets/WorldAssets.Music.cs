using System.Security.Cryptography;
using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    public NativeWorldMusicResource ReadMusicFileResource(int index)
    {
        string relativePath = new NativeMusicCatalog(gameDirectory).WorldMusicFile(index);
        byte[] container = Read(relativePath.Split('/')[^1]);
        if (container.Length < 8)
        {
            throw new InvalidDataException("Native world music file lacks its offset/end header.");
        }
        int offset = BinaryPrimitives.ReadInt32LittleEndian(container);
        int end = BinaryPrimitives.ReadInt32LittleEndian(container.AsSpan(4));
        if (offset < 8 || end > container.Length || end - offset < 16)
        {
            throw new InvalidDataException("Native world music file has an invalid selected buffer range.");
        }
        byte music = NativeWorldMusicResource.FileMusic(index);
        ReadOnlySpan<byte> payload = container.AsSpan(offset, end - offset);
        if (!payload[..4].SequenceEqual("AKAO"u8) || payload[4] != music + 1)
        {
            throw new InvalidDataException("Native world music file payload differs from543340's selected song.");
        }
        NativeWorldMusicFile file = new(index, relativePath, container.Length, Convert.ToHexString(SHA256.HashData(container)), offset, end);
        NativeWorldMusicResource result = new(1, -1, music, payload.Length, Convert.ToHexString(SHA256.HashData(payload)), file);
        result.Validate();
        return result;
    }

    public NativeWorldMusicResource ReadMusicResource(byte music)
    {
        int section = music switch
        {
            41 => 42,
            24 => 44,
            _ => throw new NotSupportedException($"Original world music{music} requires its unimplemented asynchronous file-loading contract.")
        };
        ReadOnlySpan<byte> payload = wmset.AsSpan(sections[section], sections[section + 1] - sections[section]);
        if (payload.Length < 16 || !payload[..4].SequenceEqual("AKAO"u8) || payload[4] != music + 1)
        {
            throw new InvalidDataException("Original WMSET music payload has a different AKAO identity.");
        }
        return new NativeWorldMusicResource(1, section, music, payload.Length, Convert.ToHexString(SHA256.HashData(payload)));
    }
}
