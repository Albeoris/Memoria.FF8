using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    public NativeWorldMusicResource ReadMusicFileResource(int index)
    {
        string relativePath = new NativeMusicCatalog(resources).WorldMusicFile(index);
        byte[] container = Read(relativePath.Split('/')[^1]);
        return NativeWorldDataReader.ReadMusicFile(container, index);
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
