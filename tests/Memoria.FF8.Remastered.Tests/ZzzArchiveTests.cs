using System.Buffers.Binary;
using System.Text;
using Memoria.FF8.Assets.Remastered;

namespace Memoria.FF8.Remastered.Tests;

public sealed class ZzzArchiveTests
{
    [Fact]
    public void EntryStreamsAreIndependentSeekableBoundedAndSurviveArchiveDisposal()
    {
        using SyntheticResources files = new();
        using ZzzArchive archive = new(files.Write(SyntheticResources.Zzz(("data\\Example.bin", [1, 2, 3]), ("next.bin", [4, 5]))));
        ZzzArchiveEntry entry = Assert.IsType<ZzzArchiveEntry>(archive.Find("DATA/example.bin"));
        Assert.Equal("data/Example.bin", entry.Path);
        using Stream first = archive.Open(entry);
        using Stream second = archive.Open("data/example.bin");
        Assert.True(first.CanSeek);
        Assert.False(first.CanWrite);
        Assert.Equal(1, first.ReadByte());
        Assert.Equal(0, second.Position);
        Assert.Equal(3, first.Seek(-1, SeekOrigin.End) + 1);
        Assert.Equal(3, first.ReadByte());
        Assert.Equal(-1, first.ReadByte());
        Assert.Throws<IOException>(() => first.Seek(1, SeekOrigin.End));
        Assert.Throws<IOException>(() => first.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => first.WriteByte(6));
        Assert.Null(archive.Find("missing.bin"));
        Assert.Throws<FileNotFoundException>(() => archive.Open("missing.bin"));
        archive.Dispose();
        Assert.Equal(1, second.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => archive.Open(entry));
    }

    [Theory]
    [InlineData("../file")]
    [InlineData("data/../file")]
    [InlineData("data\\..\\file")]
    [InlineData("/file")]
    [InlineData("\\\\server\\file")]
    [InlineData("C:/file")]
    [InlineData("data//file")]
    [InlineData("data/./file")]
    [InlineData("data/file\0")]
    public void RejectsNonRelativeOrTraversalPaths(string name)
    {
        using SyntheticResources files = new();
        Assert.Throws<InvalidDataException>(() => new ZzzArchive(files.Write(SyntheticResources.Zzz((name, [1])))));
    }

    [Fact]
    public void RejectsDuplicateNormalizedPaths()
    {
        using SyntheticResources files = new();
        byte[] bytes = SyntheticResources.Zzz(("data\\file", [1]), ("DATA/file", [2]));
        Assert.Throws<InvalidDataException>(() => new ZzzArchive(files.Write(bytes)));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("name-length")]
    [InlineData("truncated-index")]
    [InlineData("utf8")]
    [InlineData("offset")]
    [InlineData("length")]
    [InlineData("index-overlap")]
    [InlineData("entry-overlap")]
    public void RejectsMalformedIndexAndPayloadBounds(string corruption)
    {
        using SyntheticResources files = new();
        byte[] bytes = SyntheticResources.Zzz(("a", [1, 2]), ("b", [3, 4]));
        switch (corruption)
        {
            case "count":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, uint.MaxValue);
                break;
            case "name-length":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uint.MaxValue);
                break;
            case "truncated-index":
                bytes = bytes[..10];
                break;
            case "utf8":
                bytes[8] = 0xFF;
                break;
            case "offset":
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(9), ulong.MaxValue);
                break;
            case "length":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(17), uint.MaxValue);
                break;
            case "index-overlap":
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(9), 0);
                break;
            case "entry-overlap":
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(26), BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(9)) + 1);
                break;
            default:
                throw new ArgumentException("Unknown synthetic corruption.", nameof(corruption));
        }
        Assert.Throws<InvalidDataException>(() => new ZzzArchive(files.Write(bytes)));
    }
}
