using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class LocalizationNonFieldContractsTests
{
    [Fact]
    public void EuropeanWordmarkTilesRoundTripWithoutInventingUnicode()
    {
        var encoding = FF8Encoding.CreateEuropean();
        byte[] bytes = Enumerable.Range(0xCC, 0xE2 - 0xCC + 1).Select(value => (byte)value).ToArray();
        string text = encoding.GetString(bytes);
        Assert.Equal(bytes.Length, System.Text.RegularExpressions.Regex.Matches(text, @"\{Glyph [0-9]+\}").Count);
        Assert.Equal(bytes, encoding.GetBytes(text));
        Assert.DoesNotContain("{Glyph ", FF8Encoding.CreateJapanese().GetString(bytes));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(203)]
    [InlineData(227)]
    [InlineData(256)]
    public void WordmarkGlyphReferencesRejectValuesOutsideTheirNativeSet(int value)
    {
        Assert.Throws<FormatException>(() => FF8Encoding.CreateEuropean().GetBytes("{Glyph " + value + "}"));
    }

    [Fact]
    public void SourceWordmarksExposeTheirNativeAtlasRectanglesAndOrdinal()
    {
        using InMemoryResourceSource source = Source();
        source.Set(new("main/namedic.bin"), [1, 0, 4, 0, 0xDE, 0xDF, 0], "bin");
        var result = LocalizationSourceExporter.Read(source, new Archives(), "en", TestContext.Current.CancellationToken);
        var row = Assert.Single(result.Entries, row => row.File == "namedic_menu");
        Assert.Equal("$en_namedic_menu_0000", row.Key);
        Assert.Equal("{Glyph 222}{Glyph 223}", row.Text);
        Assert.Equal("menu/sysfnt.tex", row.Metadata["Source Font"]);
        Assert.Equal("222,223", row.Metadata["Native Glyph IDs"]);
        Assert.Equal("222:12,108,12,12;223:24,108,12,12", row.Metadata["Native Glyph Rectangles"]);
        Assert.False(row.Metadata.ContainsKey("Decode Error"));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(194)]
    [InlineData(255)]
    public void ItemControlConsumesExactlyOneUnsignedByte(int selector)
    {
        var encoding = FF8Encoding.CreateEuropean();
        byte[] encoded = [0x0D, (byte)selector, 0x41];
        string text = encoding.GetString(encoded);
        Assert.StartsWith("{Item " + selector + "}", text);
        Assert.Equal(encoded, encoding.GetBytes(text));
        Assert.ThrowsAny<Exception>(() => encoding.GetString([0x0D]));
    }

    [Fact]
    public void NamedicAcceptsOnlyBoundedZeroAlignmentPadding()
    {
        var encoding = FF8Encoding.CreateEuropean();
        byte[] bytes = NamedicBin.WriteStrings([""], encoding);
        Assert.Equal(5, bytes.Length);
        Assert.Equal([""], NamedicBin.ReadStrings([.. bytes, 0, 0, 0], encoding));
        Assert.Throws<InvalidDataException>(() => NamedicBin.ReadStrings([.. bytes, 0, 0, 1], encoding));
        Assert.Throws<InvalidDataException>(() => NamedicBin.ReadStrings([.. bytes, 0, 0, 0, 0], encoding));
    }

    [Fact]
    public void WorldFinalZeroPointerIsASentinelAndItemOperandsKeepTheirIdentity()
    {
        using InMemoryResourceSource source = Source();
        byte[] bytes = new byte[77];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 64);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(56), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), 8);
        bytes[72] = 0x0D;
        bytes[73] = 194;
        source.Set(new("world/wmsetus.obj"), bytes, "bin");
        var result = LocalizationSourceExporter.Read(source, new Archives(), "en", TestContext.Current.CancellationToken);
        var row = Assert.Single(result.Entries, row => row.Category == "World");
        Assert.Equal("$en_messages_world_0000", row.Key);
        Assert.Equal("{Item 194}", row.Text);
        Assert.DoesNotContain(result.Gaps, gap => gap.Category == "World");
    }

    [Theory]
    [InlineData("fr", "wmsetfr.obj")]
    [InlineData("de", "wmsetgr.obj")]
    [InlineData("es", "wmsetsp.obj")]
    [InlineData("it", "wmsetit.obj")]
    [InlineData("jp", "wmsetjp.obj")]
    public void WorldSourceSelectsTheRequestedLanguageBankWithoutChangingTableIdentity(string language, string file)
    {
        using InMemoryResourceSource source = Source();
        byte[] bytes = new byte[76];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 64);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(56), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), 8);
        bytes[72] = 0x0D;
        bytes[73] = 194;
        source.Set(new("world/" + file), bytes, "bin");
        var result = LocalizationSourceExporter.Read(source, new Archives(), language, TestContext.Current.CancellationToken);
        var row = Assert.Single(result.Entries, row => row.Category == "World");
        Assert.Equal("$" + language + "_messages_world_0000", row.Key);
        Assert.Equal("world/" + file, row.Metadata["Resource"]);
        Assert.Equal("{Item 194}", row.Text);
        Assert.DoesNotContain(result.Gaps, gap => gap.Category == "World");
    }

    [Fact]
    public void CardNullPointersKeepOrdinalsAndFollowingText()
    {
        using InMemoryResourceSource source = Source();
        byte[] bytes = [3, 0, 8, 0, 0, 0, 8, 0, 0x41, 0];
        source.Set(new("text/card-names-alternate"), bytes, "bin");
        var result = LocalizationSourceExporter.Read(source, new Archives(), "en", TestContext.Current.CancellationToken);
        var rows = result.Entries.Where(row => row.File == "namesalternate_cards").ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal(rows[0].Text, rows[2].Text);
        Assert.Empty(rows[1].Text);
        Assert.Equal("$en_namesalternate_cards_0001", rows[1].Key);
        Assert.Equal("true", rows[1].Metadata["Native Null Pointer"]);
        Assert.DoesNotContain(result.Gaps, gap => gap.Resource.StartsWith("text/card-names-alternate"));
    }

    [Fact]
    public void EmptyEnemyScriptSectionStillExportsName()
    {
        using InMemoryResourceSource source = Source();
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 11);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), 48);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 72);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), 72);
        bytes[48] = 0x41;
        source.Set(new("battle/c0m144.dat"), bytes, "bin");
        var result = LocalizationSourceExporter.Read(source, new Archives("c0m144.dat"), "en", TestContext.Current.CancellationToken);
        Assert.Single(result.Entries, row => row.File == "c0m144_names");
        Assert.DoesNotContain(result.Gaps, gap => gap.Resource == "battle/c0m144.dat");
    }

    private static InMemoryResourceSource Source() => new(new("synthetic", GameDataEdition.Remastered, []));

    private sealed class Archives(params string[] names) : IGameArchiveSource
    {
        public IFlArchive OpenArchive(string family) => family == "battle" ? new Archive(names) : throw new FileNotFoundException();
    }

    private sealed class Archive(string[] names) : IFlArchive
    {
        public IReadOnlyList<IFlArchiveEntry> Entries { get; } = names.Select(name => (IFlArchiveEntry)new Entry(name)).ToArray();
        public IFlArchiveEntry AddEntry(string relativePath) => throw new NotSupportedException();
        public void RemoveEntry(string relativePath) => throw new NotSupportedException();
        public void Flush() { }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Entry(string path) : IFlArchiveEntry
    {
        public string RelativePath => path;
        public uint Offset => 0;
        public uint Size => 0;
        public Stream OpenForRead() => throw new NotSupportedException();
        public Stream OpenForWrite(uint desiredSize) => throw new NotSupportedException();
    }
}
