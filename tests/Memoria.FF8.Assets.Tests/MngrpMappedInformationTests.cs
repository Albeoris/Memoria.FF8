using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Memoria.FF8.Assets.Tests;

public sealed class MngrpMappedInformationTests
{
    [Fact]
    public void PackedMapOffsetsPreserveAliasesUnusedHeaderWordsAndRawStorage()
    {
        MngrpFilePair files = Fixture();
        MngrpArchive archive = MngrpArchive.Read(files, Encoding.UTF8);
        MngrpTextBlockSection blocks = archive.GetSection<MngrpTextBlockSection>(128);
        MngrpTextBlockMapSection map = archive.GetSection<MngrpTextBlockMapSection>(127);
        Assert.Equal(2, blocks.Blocks.Count);
        Assert.Equal(new[] { "a", "b" }, blocks.Blocks[0].Texts.Select(text => text.Value));
        Assert.Equal(new[] { "c", "de" }, blocks.Blocks[1].Texts.Select(text => text.Value));
        Assert.Equal(new int?[] { 1, 0, 1 }, map.References.Select(reference => reference.BlockIndex));
        Assert.Equal(files.Content, archive.Write().Content);
        Assert.Equal(files.Header, archive.Write().Header);
    }

    [Fact]
    public void EditedTextUpdatesEveryMappedReferenceAndPreservesPhysicalGaps()
    {
        MngrpArchive archive = MngrpArchive.Read(Fixture(), Encoding.UTF8);
        MngrpTextBlockSection blocks = archive.GetSection<MngrpTextBlockSection>(128);
        blocks.Blocks[0].Texts[1].Value = "a longer body";
        MngrpFilePair written = archive.Write();
        MngrpArchive restored = MngrpArchive.Read(written, Encoding.UTF8);
        MngrpTextBlockSection changed = restored.GetSection<MngrpTextBlockSection>(128);
        MngrpTextBlockMapSection map = restored.GetSection<MngrpTextBlockMapSection>(127);
        Assert.Equal("a longer body", changed.Blocks[0].Texts[1].Value);
        Assert.Equal("de", changed.Blocks[1].Texts[1].Value);
        Assert.Equal(3, map.References[1].StoredOffset);
        Assert.NotEqual(16, map.References[0].StoredOffset);
        Assert.Equal(map.References[0].StoredOffset, map.References[2].StoredOffset);
        Assert.Equal(new byte[] { 0x7E }, changed.Blocks[0].TrailingBytes);
        Assert.Equal(new byte[] { 0xCC }, changed.TrailingData);
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE }, written.Content.AsSpan(2048, 3).ToArray());
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("header")]
    [InlineData("title")]
    [InlineData("body")]
    [InlineData("link")]
    public void MappedNodesRequireAvailableResourcesHeadersStringsAndLogicalLinks(string defect)
    {
        MngrpFilePair files = Fixture();
        switch (defect)
        {
            case "resource":
                Word(files.Content, 6, 1);
                break;
            case "header":
                Word(files.Content, 4, 2045);
                break;
            case "title":
            case "body":
                BinaryPrimitives.WriteUInt32LittleEndian(files.Content, 1);
                Word(files.Content, 4, 0);
                files.Content.AsSpan(2048).Fill((byte)'x');
                Word(files.Content, 2048, 65535);
                Word(files.Content, 2050, 65535);
                Word(files.Content, 2052, 65535);
                if (defect == "body")
                {
                    files.Content[2056] = (byte)'a';
                    files.Content[2057] = 0;
                }
                break;
            case "link":
                Word(files.Content, 2048 + 3, 3);
                break;
        }
        Assert.Throws<InvalidDataException>(() => MngrpArchive.Read(files, Encoding.UTF8));
    }

    [Fact]
    public void SelectedMappedSectionKeepsRegistryDependenciesAndIsolatesUnrelatedErrors()
    {
        MngrpFilePair files = Fixture();
        BinaryPrimitives.WriteUInt32LittleEndian(files.Header.AsSpan(87 * 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(files.Header.AsSpan(87 * 8 + 4), 2);
        Assert.Throws<InvalidDataException>(() => MngrpArchive.Read(files, Encoding.UTF8));
        var blocks = Assert.IsType<MngrpTextBlockSection>(MngrpArchive.ReadSection(files.Content, files.Header, Encoding.UTF8, 128));
        var map = Assert.IsType<MngrpTextBlockMapSection>(MngrpArchive.ReadSection(files.Content, files.Header, Encoding.UTF8, 127));
        Assert.Equal(2, blocks.Blocks.Count);
        Assert.Equal("b", blocks.Blocks[0].Texts[1].Value);
        Assert.Equal(new int?[] { 1, 0, 1 }, map.References.Select(reference => reference.BlockIndex));
    }

    [Fact]
    public void SelectedRecordSectionUsesItsRegisteredTextCompanion()
    {
        byte[] content = new byte[18];
        content[2] = 7;
        content[16] = (byte)'x';
        byte[] header = new byte[2048];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(192 * 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(192 * 8 + 4), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(200 * 8), 17);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(200 * 8 + 4), 2);
        var records = Assert.IsType<MngrpTextRecordSection>(MngrpArchive.ReadSection(content, header, Encoding.UTF8, 192));
        Assert.Equal(200, records.TextSlotIndex);
        Assert.Equal("x", Assert.Single(records.Records).Text.Value);
        Assert.Equal(7, records.Records[0].Payload[0]);
    }

    private static MngrpFilePair Fixture()
    {
        byte[] content = new byte[4096];
        byte[] header = new byte[2048];
        BinaryPrimitives.WriteUInt32LittleEndian(content, 3);
        Word(content, 4, 16);
        Word(content, 8, 3);
        Word(content, 12, 16);
        int start = 2048;
        content[start] = 0xDE;
        content[start + 1] = 0xAD;
        content[start + 2] = 0xBE;
        foreach (int offset in new[] { 3, 16 })
        {
            Word(content, start + offset, 65535);
            Word(content, start + offset + 2, 65535);
            Word(content, start + offset + 4, 65535);
        }
        Word(content, start + 3 + 6, 65535);
        Word(content, start + 16 + 6, 0);
        "a\0b\0"u8.CopyTo(content.AsSpan(start + 11));
        content[start + 15] = 0x7E;
        "c\0de\0"u8.CopyTo(content.AsSpan(start + 24));
        content[start + 29] = 0xCC;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(127 * 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(127 * 8 + 4), 2048);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(128 * 8), 2049);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(128 * 8 + 4), 2048);
        return new(content, header);
    }

    private static void Word(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)value));
}
