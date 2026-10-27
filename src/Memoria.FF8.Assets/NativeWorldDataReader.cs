using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Validates standalone world resources through their current native consumers.</summary>
public static class NativeWorldDataReader
{
    public static NativeWorldMusicResource ReadMusicFile(byte[] container, int index)
    {
        byte music = NativeWorldMusicResource.FileMusic(index);
        if (container.Length < 8)
        {
            throw new InvalidDataException("Native world music file lacks its offset/end header.");
        }
        int offset = BinaryPrimitives.ReadInt32LittleEndian(container);
        int end = BinaryPrimitives.ReadInt32LittleEndian(container.AsSpan(4));
        if (offset < 8 || end > container.Length || end < offset || end - offset < 16)
        {
            throw new InvalidDataException("Native world music file has an invalid selected buffer range.");
        }
        ReadOnlySpan<byte> payload = container.AsSpan(offset, end - offset);
        if (!payload[..4].SequenceEqual("AKAO"u8) || payload[4] != music + 1)
        {
            throw new InvalidDataException("Native world music file payload differs from543340's selected song.");
        }
        NativeWorldMusicFile file = new(index, $"dat/music{index}.obj", container.Length, Convert.ToHexString(SHA256.HashData(container)), offset, end);
        NativeWorldMusicResource result = new(1, -1, music, payload.Length, Convert.ToHexString(SHA256.HashData(payload)), file);
        result.Validate();
        return result;
    }

    public static void ValidateRail(byte[] bytes)
    {
        if (bytes.Length != 14 * 2048)
        {
            throw new InvalidDataException("Native rail requires fourteen complete fixed-size chunks.");
        }
        for (int track = 0; track < 14; track++)
        {
            ReadOnlySpan<byte> chunk = bytes.AsSpan(track * 2048, 2048);
            int points = chunk[0];
            int stops = chunk[1];
            if (points is < 2 or > 127 || stops > 4 || 12 + points * 16 > chunk.Length)
            {
                throw new InvalidDataException("Native rail header exceeds its fixed original chunk.");
            }
            for (int stop = 0; stop < stops; stop++)
            {
                if (chunk[4 + stop] >= points || chunk[8 + stop] >= points)
                {
                    throw new InvalidDataException("Native rail stop leaves its point table.");
                }
            }
            NativeWorldRailPoint? previous = null;
            for (int point = 0; point < points; point++)
            {
                ReadOnlySpan<byte> entry = chunk.Slice(12 + point * 16, 16);
                NativeWorldRailPoint current = new(BinaryPrimitives.ReadInt32LittleEndian(entry), BinaryPrimitives.ReadInt32LittleEndian(entry[4..]), BinaryPrimitives.ReadInt32LittleEndian(entry[8..]), BinaryPrimitives.ReadInt32LittleEndian(entry[12..]));
                if (previous is not null && NativeWorldScriptedPathMath.SegmentLength(previous, current) < 8)
                {
                    throw new NotSupportedException("Native scripted path requires nonzero shift-three segment denominators.");
                }
                previous = current;
            }
        }
    }

    public static void ValidateTerrain(byte[] bytes)
    {
        if (bytes.Length != 835 * WorldAssets.SegmentBytes)
        {
            throw new InvalidDataException("Unexpected WMX segment count.");
        }
        for (int segment = 0; segment < 835; segment++)
        {
            _ = WorldAssetReader.ReadSegment(bytes.AsSpan(segment * WorldAssets.SegmentBytes, WorldAssets.SegmentBytes).ToArray(), 0, segment);
        }
    }

    public static void ValidateTerrainTextures(byte[] bytes)
    {
        const int pageBytes = 0x12800;
        int[] pages = Enumerable.Range(0, 16).SelectMany(page => new[] { WorldAssets.SelectTerrainTexturePage(page, 0), WorldAssets.SelectTerrainTexturePage(page, 1) }).Distinct().ToArray();
        foreach (int page in pages)
        {
            int start = page * pageBytes;
            if (start > bytes.Length - 8)
            {
                throw new InvalidDataException("Native terrain texture page is absent from its resource.");
            }
            var texture = WorldAssetReader.ReadTim(bytes, start);
            if (texture.End > start + pageBytes)
            {
                throw new InvalidDataException("Native terrain texture crosses its fixed page boundary.");
            }
            for (int palette = 0; palette < 16; palette++)
            {
                _ = texture.Image.Decode(palette);
            }
        }
    }

    public static void ValidateCharacters(byte[] bytes)
    {
        if (bytes.Length < 12 || BinaryPrimitives.ReadInt32LittleEndian(bytes) != bytes.Length)
        {
            throw new InvalidDataException("Invalid original world character container length.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(bytes.Length - 4));
        if (count is < 1 or > 64)
        {
            throw new InvalidDataException("The world character footer has an invalid model count.");
        }
        for (int shape = 0; shape < count; shape++)
        {
            _ = WorldAssetReader.ReadWorldCharacter(bytes, shape);
        }
    }
}
