using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDoom8StripResourcesTests
{
    [Fact]
    public void Original_tables_include_the_endpoint_height_and_each_actual_placement_and_range_sentinel()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        NativeBattleDoom8StripTables tables = resources.RequireStripTables();
        Assert.Equal(new short[] { -96, -8, 8, 96 }, tables.Widths);
        Assert.Equal(18, tables.Heights.Length);
        Assert.Equal((short)-256, tables.Heights[8]);
        Assert.Equal((short)0, tables.Heights[16]);
        Assert.Equal((short)0, tables.Heights[17]);
        Assert.Equal(new short[] { 0, 6, 12, 16, 127 }, resources.RequirePlacementSelectors());
        Assert.Equal(14, tables.Ranges.Length);
        Assert.Equal(new NativeBattleDoom8StripRange(0, 6), tables.Ranges[0]);
        Assert.Equal(new NativeBattleDoom8StripRange(14, 16), tables.Ranges[12]);
        Assert.Equal(new NativeBattleDoom8StripRange(127, 127), tables.Ranges[13]);
        NativeExecutableMemory original = new(game.ReadNativeExecutable());
        Assert.Equal(original.Read(0x1636C08, 8), Words(tables.Widths));
        Assert.Equal(original.Read(0x1636C10, 36), Words(tables.Heights));
        Assert.Equal(original.Read(0x1636C34, 10), Words(resources.RequirePlacementSelectors()));
        Assert.Equal(original.Read(0x1636C40, 56), Words(tables.Ranges.SelectMany(range => new[] { range.First, range.Last }).ToImmutableArray()));
    }

    [Fact]
    public void Overlay_and_cold_target_pointer_keep_original_PE_identity_and_survive_JSON()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        ImmutableArray<uint> colors = resources.RequireOverlayColors();
        Assert.Equal(new uint[] { 0x00C0C0C0, 0x00808080, 0x00404040, 0xFF000000 }, colors);
        Assert.Equal(0x275F510u, resources.RequireColdTargetPointer());
        NativeExecutableMemory original = new(game.ReadNativeExecutable());
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(original.Read(0x1636C04, 4)), resources.RequireColdTargetPointer());
        for (int index = 0; index < colors.Length; index++)
        {
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(original.Read(0x163ACB4u + (uint)index * 4, 4)), colors[index]);
        }
        NativeBattleDoom8Resources restored = JsonSerializer.Deserialize<NativeBattleDoom8Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources.StripTables), JsonSerializer.Serialize(restored.RequireStripTables()));
        Assert.Equal(resources.RequirePlacementSelectors().ToArray(), restored.RequirePlacementSelectors().ToArray());
        Assert.Equal(colors.ToArray(), restored.RequireOverlayColors().ToArray());
        Assert.Equal(resources.RequireColdTargetPointer(), restored.RequireColdTargetPointer());
    }

    [Fact]
    public void Missing_new_resources_remain_explicitly_unsupported_without_changing_older_resource_JSON()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources() with { StripTables = null, PlacementSelectors = null, OverlayColors = null, ColdTargetPointer = null };
        string json = JsonSerializer.Serialize(resources);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(resources.StripTables), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(resources.PlacementSelectors), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(resources.OverlayColors), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(resources.ColdTargetPointer), out _));
        NativeBattleDoom8Resources restored = JsonSerializer.Deserialize<NativeBattleDoom8Resources>(json)!;
        Assert.Throws<NotSupportedException>(() => restored.RequireStripTables());
        Assert.Throws<NotSupportedException>(() => restored.RequirePlacementSelectors());
        Assert.Throws<NotSupportedException>(() => restored.RequireOverlayColors());
        Assert.Throws<NotSupportedException>(() => restored.RequireColdTargetPointer());
    }

    [Fact]
    public void Resource_validation_rejects_missing_endpoint_height_truncated_tables_and_invalid_native_bounds()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        NativeBattleDoom8StripTables tables = resources.RequireStripTables();
        Assert.Throws<InvalidDataException>(() => (resources with { StripTables = tables with { Widths = tables.Widths.RemoveAt(3) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { StripTables = tables with { Heights = tables.Heights.RemoveAt(17) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { StripTables = tables with { Ranges = tables.Ranges.RemoveAt(13) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { StripTables = tables with { Ranges = tables.Ranges.SetItem(0, new(0, 17)) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { StripTables = tables with { Ranges = tables.Ranges.SetItem(13, new(126, 127)) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { PlacementSelectors = resources.RequirePlacementSelectors().RemoveAt(4) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { PlacementSelectors = resources.RequirePlacementSelectors().SetItem(0, -1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { OverlayColors = resources.RequireOverlayColors().RemoveAt(3) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ColdTargetPointer = resources.RequireColdTargetPointer() + 4 }).Validate());
    }

    private static byte[] Words(ImmutableArray<short> values)
    {
        byte[] bytes = new byte[values.Length * 2];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), values[index]);
        }
        return bytes;
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Doom strip ingress.");
        return new(directory!);
    }
}
