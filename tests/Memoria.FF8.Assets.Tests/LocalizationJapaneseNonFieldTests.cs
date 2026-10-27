using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class LocalizationJapaneseNonFieldTests
{
    [Fact]
    public void CommonJapanesePagesAndControlParametersDecodeWithoutAFieldCharacterTable()
    {
        const string expected = "あいうⅧ闘念{Var 28}";
        byte[] bytes = FF8Encoding.CreateJapanese(FF8TextLanguage.JP).GetBytes(expected);
        Assert.Contains((byte)0x19, bytes);
        Assert.Contains((byte)0x1a, bytes);
        Assert.Contains((byte)0x1b, bytes);
        Assert.Contains((byte)0x1c, bytes);
        using InMemoryResourceSource source = Source();
        source.Set(new("menu/namedic.bin"), NamedTable(bytes), "bin");

        LocalizationSourceCatalog result = LocalizationSourceExporter.Read(source, new MissingArchives(), "jp", TestContext.Current.CancellationToken);

        LocalizationSourceEntry row = Assert.Single(result.Entries, row => row.File == "namedic_menu");
        Assert.Equal("$jp_namedic_menu_0000", row.Key);
        Assert.Equal(expected, row.Text);
        Assert.False(row.Metadata.ContainsKey("Decode Error"));
    }

    [Fact]
    public void FieldOnlyAndTruncatedEscapesRetainMissingSourceSlotsWithoutPoisoningTheirNeighbours()
    {
        byte[] supported = FF8Encoding.CreateJapanese(FF8TextLanguage.JP).GetBytes("あいう");
        using InMemoryResourceSource source = Source();
        source.Set(new("menu/namedic.bin"), NamedTable(supported, [0x1c, 0x20], [0x19], supported), "bin");

        LocalizationSourceCatalog result = LocalizationSourceExporter.Read(source, new MissingArchives(), "jp", TestContext.Current.CancellationToken);
        LocalizationSourceEntry[] rows = result.Entries.Where(row => row.File == "namedic_menu").ToArray();

        Assert.Equal(4, rows.Length);
        Assert.Equal("あいう", rows[0].Text);
        Assert.Equal("あいう", rows[3].Text);
        foreach (LocalizationSourceEntry row in rows[1..3])
        {
            Assert.Empty(row.Text);
            Assert.Equal("false", row.Metadata["Source Present"]);
            Assert.NotEmpty(row.Metadata["Decode Error"]!);
            Assert.Contains(result.Gaps, gap => gap.Resource.EndsWith("/namedic_menu/" + row.Metadata["Source Ordinal"], StringComparison.Ordinal));
        }
        Assert.All(result.Entries, row => Assert.DoesNotContain('\uFDD0', row.Text));
    }

    [Fact]
    public void InvalidWorldStringRetainsItsOriginalOrdinalAndDoesNotSuppressFollowingMessages()
    {
        byte[] supported = FF8Encoding.CreateJapanese(FF8TextLanguage.JP).GetBytes("あい");
        byte[] bank = new byte[8 + 3 + supported.Length + 1];
        BinaryPrimitives.WriteInt32LittleEndian(bank, 8);
        BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(4), 11);
        bank[8] = 0x1c;
        bank[9] = 0x20;
        supported.CopyTo(bank, 11);
        byte[] bytes = new byte[64 + bank.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(13 * 4), 64);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14 * 4), bytes.Length);
        bank.CopyTo(bytes, 64);
        using InMemoryResourceSource source = Source();
        source.Set(new("world/wmsetjp.obj"), bytes, "bin");

        LocalizationSourceCatalog result = LocalizationSourceExporter.Read(source, new MissingArchives(), "jp", TestContext.Current.CancellationToken);
        LocalizationSourceEntry[] rows = result.Entries.Where(row => row.File == "messages_world").ToArray();

        Assert.Equal(2, rows.Length);
        Assert.Equal("$jp_messages_world_0000", rows[0].Key);
        Assert.Equal("false", rows[0].Metadata["Source Present"]);
        Assert.Equal("$jp_messages_world_0001", rows[1].Key);
        Assert.Equal("あい", rows[1].Text);
    }

    [Fact]
    public void OptionalInstalledJapaneseCommonResourcesExportTheirActualSourceRows()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_LOCALIZATION_GAME_DIR for optional Japanese source export validation.");
        LocalizationSourceCatalog result = LocalizationSourceExporter.ReadNonFields(directory, "jp", TestContext.Current.CancellationToken);
        string? reportDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(Path.Combine(reportDirectory, "jp-common-nonfield-summary.json"), JsonSerializer.Serialize(new
            {
                Language = "jp",
                Entries = result.Entries.Count,
                Categories = result.Entries.GroupBy(row => row.Category).ToDictionary(group => group.Key, group => group.Count()),
                Tables = result.Entries.GroupBy(row => row.File).ToDictionary(group => group.Key, group => group.Count()),
                Missing = result.Entries.Where(row => row.Metadata.GetValueOrDefault("Source Present") == "false").Select(row => new { row.Key, Error = row.Metadata.GetValueOrDefault("Decode Error") }).ToArray(),
                Gaps = result.Gaps
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.Contains(result.Entries, row => row.File == "kernel_magicspells_name" && row.Text.Any(character => character is >= '\u3040' and <= '\u9FFF'));
        Assert.Contains(result.Entries, row => row.File == "mngrp_slot087_table");
        Assert.Equal(110, result.Entries.Count(row => row.File == "names_cards"));
        Assert.Equal(160, result.Entries.Count(row => row.File == "scan_monsters"));
        Assert.Equal(151, result.Entries.Count(row => row.File == "messages_world"));
        Assert.Contains(result.Entries, row => row.File == "messages_world" && row.Metadata.GetValueOrDefault("Source Present") == "false");
        Assert.DoesNotContain(result.Entries, row => row.Category == "Fields");
        Assert.All(result.Entries, row =>
        {
            Assert.StartsWith("$jp_", row.Key);
            Assert.DoesNotContain('\uFDD0', row.Text);
            Assert.Equal(64, row.Metadata["Source SHA256"]!.Length);
        });
        Assert.Equal(result.Entries.Count, result.Entries.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count());
    }

    private static byte[] NamedTable(params byte[][] rows)
    {
        int position = 2 + rows.Length * 2;
        byte[] bytes = new byte[position + rows.Sum(row => row.Length + 1)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)rows.Length));
        for (int index = 0; index < rows.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2 + index * 2), checked((ushort)position));
            rows[index].CopyTo(bytes, position);
            position += rows[index].Length + 1;
        }
        return bytes;
    }

    private static InMemoryResourceSource Source() => new(new("synthetic", GameDataEdition.Remastered, [], "ja"));
    private sealed class MissingArchives : IGameArchiveSource
    {
        public IFlArchive OpenArchive(string family) => throw new FileNotFoundException("Synthetic archive unavailable.");
    }
}
