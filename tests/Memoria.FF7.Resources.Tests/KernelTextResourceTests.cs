using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class KernelTextResourceTests
{
    [Fact]
    public void ArchiveUsesOneBoundedStreamAndExactlyEighteenLengthPrefixedSections()
    {
        byte[] decoded = new byte[18 * 5];
        for (int index = 0; index < 18; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(index * 5), 1);
            decoded[index * 5 + 4] = (byte)index;
        }
        IReadOnlyList<ReadOnlyMemory<byte>> sections = KernelTextArchive.Read(LiteralFrame(decoded));
        Assert.Equal(18, sections.Count);
        Assert.All(Enumerable.Range(0, 18), index => Assert.Equal(new byte[] { (byte)index }, sections[index].ToArray()));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.Read(LiteralFrame(decoded[..^5])));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.Read(LiteralFrame([.. decoded, 0])));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.Read(LiteralFrame(decoded), maximumDecodedBytes: decoded.Length - 1));
        decoded[0] = 255;
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.Read(LiteralFrame(decoded)));
    }

    [Fact]
    public void OffsetStringsExpandBackwardDictionariesAndPreserveNativeTermination()
    {
        Assert.Equal(new[] { "ABCD", "ABCD" }, KernelTextArchive.ReadPlainStrings([4, 0, 9, 0, 0x21, 0x22, 0x23, 0x24, 255, 0xF9, 4, 255], 2));
        Assert.Equal(new[] { "ABC", "ABC" }, KernelTextArchive.ReadPlainStrings([4, 0, 8, 0, 0x21, 0x22, 0x23, 255, 0xF9, 3], 2));
        Assert.Equal(new[] { "A" }, KernelTextArchive.ReadPlainStrings([2, 0, 0xF8, 255, 0x21, 255], 1));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.ReadPlainStrings([0, 0, 255], 1));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.ReadPlainStrings([2, 0, 0x21], 1));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.ReadPlainStrings([2, 0, 0xF9], 1));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.ReadPlainStrings([2, 0, 0x21, 0xF9, 0, 255], 1));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.ReadPlainStrings([2, 0, 0xF8], 1));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.ReadPlainStrings([2, 0, 0xEA, 0], 1));
        Assert.Throws<NotSupportedException>(() => KernelTextArchive.ReadPlainStrings([2, 0, 0xEA, 0, 0, 255], 1));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.ReadPlainStrings([2, 0, 0x21, 0x22, 255], 1, maximumStringBytes: 2));
    }

    [Fact]
    public void OriginalInventoryLabelsUseTheirNativeSectionAndItemNumberRanges()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the kernel menu text check.");
        Ff7KernelTextResources resources = Ff7KernelTextResources.Load(Ff7Installation.Discover(root));
        Assert.Equal(320, resources.ItemNames.Count);
        Assert.Equal(320, resources.ItemDescriptions.Count);
        Assert.Equal("Potion", resources.ItemNames[0]);
        Assert.Equal("Ether", resources.ItemNames[3]);
        Assert.Equal("Buster Sword", resources.ItemNames[128]);
        Assert.Equal("Bronze Bangle", resources.ItemNames[256]);
        Assert.Equal("Power Wrist", resources.ItemNames[288]);
        Assert.Equal("Restores HP by 100", resources.ItemDescriptions[0]);
        Assert.Equal("Initial equipment", resources.ItemDescriptions[128]);
        Assert.Equal(string.Empty, resources.ItemNames[127]);
        Assert.False(resources.ItemNames.ContainsKey(320));
    }

    private static byte[] LiteralFrame(ReadOnlySpan<byte> decoded)
    {
        List<byte> packed = new();
        for (int offset = 0; offset < decoded.Length; offset += 8)
        {
            int count = Math.Min(8, decoded.Length - offset);
            packed.Add((byte)((1 << count) - 1));
            packed.AddRange(decoded.Slice(offset, count).ToArray());
        }
        byte[] framed = new byte[packed.Count + 4];
        BinaryPrimitives.WriteInt32LittleEndian(framed, packed.Count);
        packed.CopyTo(framed, 4);
        return framed;
    }
}
