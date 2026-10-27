using System.Buffers.Binary;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldDataReaderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void MusicPreservesCompleteContainerAndSelectedPayloadIdentities(int index)
    {
        byte[] bytes = Music(index);
        NativeWorldMusicResource result = NativeWorldDataReader.ReadMusicFile(bytes, index);

        Assert.Equal(NativeWorldMusicResource.FileMusic(index), result.MusicId);
        Assert.Equal($"dat/music{index}.obj", result.File!.RelativePath);
        Assert.Equal(8, result.File.PayloadOffset);
        Assert.Equal(24, result.File.PayloadEnd);
        Assert.Equal(16, result.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), result.File.Sha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes.AsSpan(8, 16))), result.Sha256);
    }

    [Theory]
    [InlineData(-1, 24)]
    [InlineData(7, 24)]
    [InlineData(8, 33)]
    [InlineData(16, 15)]
    [InlineData(16, 24)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void MusicRejectsInvalidSelectedRanges(int start, int end)
    {
        byte[] bytes = Music(0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes, start);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), end);

        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ReadMusicFile(bytes, 0));
    }

    [Fact]
    public void MusicRejectsMissingHeaderAndWrongSong()
    {
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ReadMusicFile(new byte[7], 0));
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ReadMusicFile(Music(1), 0));
        byte[] bytes = Music(0);
        bytes[8] = 0;
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ReadMusicFile(bytes, 0));
    }

    [Fact]
    public void RailChecksEveryChunkAndBothStopDirections()
    {
        byte[] bytes = Rail();
        NativeWorldDataReader.ValidateRail(bytes);
        bytes[13 * 2048 + 8] = 2;
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateRail(bytes));
        bytes[13 * 2048 + 8] = 1;
        bytes[13 * 2048 + 4] = 2;
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateRail(bytes));
    }

    [Fact]
    public void RailRejectsIncompleteChunksAndUnusableSegmentDenominators()
    {
        byte[] bytes = Rail();
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateRail(bytes[..^1]));
        bytes[0] = 128;
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateRail(bytes));
        bytes = Rail();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), 0);
        Assert.Throws<NotSupportedException>(() => NativeWorldDataReader.ValidateRail(bytes));
    }

    [Fact]
    public void TerrainValidatesReplacementSegmentsBeyondTheOrdinaryMapGrid()
    {
        byte[] bytes = Terrain();
        NativeWorldDataReader.ValidateTerrain(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(834 * WorldAssets.SegmentBytes + 64), WorldAssets.SegmentBytes - 1);

        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateTerrain(bytes));
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateTerrain(bytes[..^1]));
    }

    [Fact]
    public void TerrainTexturesValidateAlternatePageNineteenAndItsPaletteDomain()
    {
        byte[] bytes = TerrainTextures();
        NativeWorldDataReader.ValidateTerrainTextures(bytes);
        bytes[19 * 0x12800] = 0;

        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateTerrainTextures(bytes));
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateTerrainTextures(bytes[..(19 * 0x12800)]));
    }

    [Fact]
    public void CharactersRejectMissingOrInvalidFooterDomain()
    {
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateCharacters(new byte[11]));
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 65);

        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateCharacters(bytes));
        bytes[0] = 15;
        Assert.Throws<InvalidDataException>(() => NativeWorldDataReader.ValidateCharacters(bytes));
    }

    [Fact]
    public void InstalledWorldFamiliesReachCurrentConsumers()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        foreach (string path in Paths())
        {
            using Stream input = source.Find(new ResourceKey(path))!.OpenRead();
            using MemoryStream content = new();
            input.CopyTo(content);
            Validate(path, content.ToArray());
        }
    }

    [Fact]
    public void ExternalWorldFixturesReachCurrentConsumersWithoutRewritingBytes()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_DATA_FIXTURES");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Configure optional native world data fixtures.");
        string[] fixtures = Directory.GetDirectories(directory!);
        Assert.NotEmpty(fixtures);
        foreach (string fixture in fixtures)
        {
            foreach (string path in Paths())
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(fixture, path.Replace('/', Path.DirectorySeparatorChar)));
                byte[] original = bytes.ToArray();
                Validate(path, bytes);
                Assert.Equal(original, bytes);
            }
        }
    }

    private static IEnumerable<string> Paths()
    {
        for (int index = 0; index < 6; index++)
        {
            yield return $"world/music{index}.obj";
        }
        yield return "world/rail.obj";
        yield return "world/wmx.obj";
        yield return "world/texl.obj";
        yield return "world/chara.one";
    }

    private static void Validate(string path, byte[] bytes)
    {
        string name = Path.GetFileName(path);
        if (name.StartsWith("music", StringComparison.Ordinal))
        {
            _ = NativeWorldDataReader.ReadMusicFile(bytes, name[5] - '0');
        }
        else if (name == "rail.obj")
        {
            NativeWorldDataReader.ValidateRail(bytes);
        }
        else if (name == "wmx.obj")
        {
            NativeWorldDataReader.ValidateTerrain(bytes);
        }
        else if (name == "texl.obj")
        {
            NativeWorldDataReader.ValidateTerrainTextures(bytes);
        }
        else
        {
            NativeWorldDataReader.ValidateCharacters(bytes);
        }
    }

    private static byte[] Music(int index)
    {
        byte[] bytes = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 24);
        "AKAO"u8.CopyTo(bytes.AsSpan(8));
        bytes[12] = checked((byte)(NativeWorldMusicResource.FileMusic(index) + 1));
        return bytes;
    }

    private static byte[] Rail()
    {
        byte[] bytes = new byte[14 * 2048];
        for (int track = 0; track < 14; track++)
        {
            int start = track * 2048;
            bytes[start] = 2;
            bytes[start + 1] = 1;
            bytes[start + 8] = 1;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(start + 28), 16);
        }
        return bytes;
    }

    private static byte[] Terrain()
    {
        byte[] bytes = new byte[835 * WorldAssets.SegmentBytes];
        for (int segment = 0; segment < 835; segment++)
        {
            for (int block = 0; block < 16; block++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(segment * WorldAssets.SegmentBytes + 4 + block * 4), 68);
            }
        }
        return bytes;
    }

    private static byte[] TerrainTextures()
    {
        TimImage image = new(TimPixelMode.Indexed8, 0, 0, 1, 1, [0, 0], new TimClut(0, 0, 256, 16, new ushort[256 * 16]));
        byte[] tim = new TimFile([image]).Write();
        byte[] bytes = new byte[20 * 0x12800];
        for (int page = 0; page < 20; page++)
        {
            tim.CopyTo(bytes, page * 0x12800);
        }
        return bytes;
    }
}
