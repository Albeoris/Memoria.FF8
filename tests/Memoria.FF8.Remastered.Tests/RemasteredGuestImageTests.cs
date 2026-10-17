using System.Buffers.Binary;
using Memoria.FF8.Assets.Remastered;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredGuestImageTests
{
    [Fact]
    public void ReadsOnlyExplicitGuestSectionsAndAdjacentSectionBytes()
    {
        using SyntheticResources files = new();
        byte[] bytes = SyntheticResources.Dat(new SerializedSection(".one", 0x1000, [1, 2, 3, 4]), new SerializedSection(".two", 0x1004, [5, 6]));
        RemasteredGuestImage image = new(files.Write(bytes));
        Assert.Equal(0x400000u, image.ImageBase);
        Assert.Equal(new byte[] { 3, 4, 5, 6 }, image.Read(0x401002, 4));
        Assert.Empty(image.Read(0x401000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.Read(0x401000, -1));
        Assert.Throws<InvalidDataException>(() => image.Read(0x3FFFFF, 1));
        Assert.Throws<InvalidDataException>(() => image.Read(0x400000, 1));
        Assert.Throws<InvalidDataException>(() => image.Read(0x401005, 2));
        Assert.Throws<InvalidDataException>(() => image.Read(uint.MaxValue, 2));
    }

    [Fact]
    public void LoaderReadsZeroTailWithoutTreatingGapsAsInitializedStorage()
    {
        using SyntheticResources files = new();
        byte[] bytes = SyntheticResources.Dat(new SerializedSection(".data", 0x1000, [1, 2, 3, 4]));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(256), 8);
        RemasteredGuestImage image = new(files.Write(bytes));
        Assert.Equal(new byte[] { 3, 4, 0, 0, 0, 0 }, image.ReadLoader(0x401002, 6));
        Assert.Throws<InvalidDataException>(() => image.Read(0x401004, 1));
        Assert.Throws<InvalidDataException>(() => image.ReadLoader(0x401008, 1));
        Assert.Throws<InvalidDataException>(() => image.ReadLoader(0x400FFF, 2));
        Assert.Throws<InvalidDataException>(() => image.ReadLoader(uint.MaxValue, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.ReadLoader(0x401000, -1));
    }

    [Theory]
    [InlineData("short-expanded")]
    [InlineData("long-expanded")]
    [InlineData("truncated-zlib")]
    [InlineData("missing-checksum")]
    [InlineData("trailing-zlib")]
    [InlineData("concatenated-zlib")]
    [InlineData("section-name")]
    [InlineData("section-overlap")]
    public void RejectsMalformedSectionSerialization(string corruption)
    {
        using SyntheticResources files = new();
        SerializedSection section = new(".data", 0x1000, [1, 2, 3, 4]);
        byte[] compressed = SyntheticResources.Compress(section.Data);
        section = corruption switch
        {
            "short-expanded" => section with { ExpandedSize = 3 },
            "long-expanded" => section with { ExpandedSize = 5 },
            "truncated-zlib" => section with { Compressed = compressed[..^1] },
            "missing-checksum" => section with { Compressed = compressed[..^4] },
            "trailing-zlib" => section with { Compressed = [.. compressed, 0] },
            "concatenated-zlib" => section with { Compressed = [.. compressed, .. compressed] },
            "section-name" => section with { SerializedName = ".other" },
            "section-overlap" => section,
            _ => throw new ArgumentException("Unknown synthetic corruption.", nameof(corruption))
        };
        byte[] bytes = corruption == "section-overlap" ? SyntheticResources.Dat(section, new SerializedSection(".two", 0x1002, [5, 6])) : SyntheticResources.Dat(section);
        Assert.Throws<InvalidDataException>(() => new RemasteredGuestImage(files.Write(bytes)));
    }

    [Theory]
    [InlineData("machine")]
    [InlineData("optional-size")]
    [InlineData("optional-magic")]
    [InlineData("section-count")]
    [InlineData("section-rva")]
    [InlineData("expanded-size")]
    [InlineData("compressed-size")]
    [InlineData("library-count")]
    [InlineData("iat-count")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    public void RejectsMalformedHeaderCountsAndBounds(string corruption)
    {
        using SyntheticResources files = new();
        byte[] bytes = SyntheticResources.Dat(new SerializedSection(".data", 0x1000, [1, 2, 3, 4]));
        switch (corruption)
        {
            case "machine":
                bytes[0] = 0;
                break;
            case "optional-size":
                bytes[16] = 0;
                break;
            case "optional-magic":
                bytes[20] = 0;
                break;
            case "section-count":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(244), uint.MaxValue);
                break;
            case "section-rva":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(260), 0x3FFF);
                break;
            case "expanded-size":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(288), uint.MaxValue);
                break;
            case "compressed-size":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(292), uint.MaxValue);
                break;
            case "library-count":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 8), uint.MaxValue);
                break;
            case "iat-count":
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), 1);
                break;
            case "truncated":
                bytes = bytes[..^1];
                break;
            case "trailing":
                bytes = [.. bytes, 0];
                break;
            default:
                throw new ArgumentException("Unknown synthetic corruption.", nameof(corruption));
        }
        Assert.Throws<InvalidDataException>(() => new RemasteredGuestImage(files.Write(bytes)));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("vector-count")]
    [InlineData("final-address")]
    [InlineData("iat-address")]
    [InlineData("ascii")]
    public void ValidatesSerializedImportVectorsAndFinalAddressList(string variant)
    {
        using SyntheticResources files = new();
        byte[] section = SyntheticResources.Dat(new SerializedSection(".data", 0x1000, [1, 2, 3, 4]));
        using MemoryStream bytes = new();
        bytes.Write(section.AsSpan(0, section.Length - 8));
        using BinaryWriter writer = new(bytes, System.Text.Encoding.ASCII, true);
        writer.Write(1u);
        writer.Write(new byte[20]);
        SyntheticResources.WriteText(writer, variant == "ascii" ? "libraryé" : "library");
        writer.Write(1u);
        writer.Write(0x1000u);
        SyntheticResources.WriteText(writer, "symbol");
        writer.Write(variant == "vector-count" ? 0u : 1u);
        writer.Write(variant == "iat-address" ? uint.MaxValue : 0x401000u);
        SyntheticResources.WriteText(writer, "symbol");
        writer.Write(1u);
        writer.Write(variant == "final-address" ? 0x401001u : 0x401000u);
        string path = files.Write(bytes.ToArray());
        if (variant == "valid")
        {
            RemasteredGuestImage image = new(path);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, image.Read(0x401000, 4));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => new RemasteredGuestImage(path));
        }
    }
}
