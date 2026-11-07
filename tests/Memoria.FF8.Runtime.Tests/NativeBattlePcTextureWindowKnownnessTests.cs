using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcTextureWindowKnownnessTests
{
    [Fact]
    public void Opaque_capture_preserves_partial_words_and_reads_only_known_neighbors()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess() with
        {
            VramRegions = [new(0, [1, 2, 3, 4, 5, 6])],
            VramUnknownRegions = [new(1, 2)]
        };
        NativeBattlePcTextureWindowPixels pixels = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 3, 1), textures);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, pixels.Data);
        Assert.Equal(new[] { true, false, false, true, true, true }, pixels.Known!.Value);
        Assert.Throws<NotSupportedException>(() => pixels.ReadWord(0, 0));
        Assert.Throws<NotSupportedException>(() => pixels.ReadWord(1, 0));
        Assert.Equal((ushort)0x0605, pixels.ReadWord(2, 0));
        Assert.Equal(new byte[] { 5, 6 }, pixels.ReadKnownSpan(4, 2).ToArray());
        Assert.Throws<NotSupportedException>(() => pixels.ReadKnownSpan(0, 4).ToArray());
    }

    [Fact]
    public void Knownness_only_publication_changes_cannot_reuse_or_deduplicate_old_pixels()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess() with
        {
            VramRegions = [new(0, [1, 2])],
            VramUnknownRegions = [new(1, 1)]
        };
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 1, 1);
        NativeBattlePcTextureWindowPixels unknown = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, textures);
        textures = textures.PublishVramSpan(1, [2]);
        NativeBattlePcTextureWindowPixels known = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, textures);

        Assert.NotSame(unknown, known);
        Assert.False(unknown.HasSameStoredPixels(known));
        Assert.Equal(unknown.Data.ToArray(), known.Data.ToArray());
        Assert.Null(known.Known);
        Assert.Equal((ushort)0x0201, known.ReadWord(0, 0));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(0, 0));
        Assert.Same(known, NativeBattlePcTextureWindowPixels.Capture(64, rectangle, textures));
    }

    [Fact]
    public void Saved_pixels_restore_bits_and_mask_independently()
    {
        NativeBattlePcTextureWindowPixels original = new(0, 0, 2, 1, [1, 2, 3, 4]) { Known = [true, false, true, true] };
        string json = JsonSerializer.Serialize(original);
        NativeBattlePcTextureWindowPixels first = JsonSerializer.Deserialize<NativeBattlePcTextureWindowPixels>(json)!;
        NativeBattlePcTextureWindowPixels second = JsonSerializer.Deserialize<NativeBattlePcTextureWindowPixels>(json)!;

        Assert.True(first.HasSameStoredPixels(second));
        Assert.Equal(json, JsonSerializer.Serialize(first));
        Assert.Equal((ushort)0x0403, first.ReadWord(1, 0));
        Assert.Throws<NotSupportedException>(() => second.ReadWord(0, 0));
        first = first with { Known = null };
        Assert.Equal((ushort)0x0201, first.ReadWord(0, 0));
        Assert.Throws<NotSupportedException>(() => second.ReadWord(0, 0));
    }

    [Fact]
    public void Old_json_without_mask_is_known_and_equivalent_to_explicit_known_mask()
    {
        NativeBattlePcTextureWindowPixels original = new(0, 0, 1, 1, [7, 8]);
        string json = JsonSerializer.Serialize(original);
        Assert.DoesNotContain("Known", json);
        NativeBattlePcTextureWindowPixels restored = JsonSerializer.Deserialize<NativeBattlePcTextureWindowPixels>(json)!;

        Assert.Equal((ushort)0x0807, restored.ReadWord(0, 0));
        Assert.True(restored.HasSameStoredPixels(restored with { Known = [true, true] }));
        Assert.Throws<InvalidDataException>(() => (restored with { Known = [true] }).Validate());
    }

    [Fact]
    public void Page_tail_is_known_while_opaque_vram_lanes_remain_unknown()
    {
        int address = 2 * (511 * 1024 + 960);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess() with
        {
            VramRegions = [new(address, [1, 2])],
            VramUnknownRegions = [new(address, 1)]
        };
        textures = textures with { VramReadTail = textures.VramReadTail!.Value.SetItem(0, 3).SetItem(1, 4) };
        NativeBattlePcTextureWindowPixels pixels = NativeBattlePcTextureWindowPixels.Capture(95, new(0, 255, 256, 1), textures);

        Assert.Throws<NotSupportedException>(() => pixels.ReadWord(960, 511));
        Assert.Equal((ushort)0x0403, pixels.ReadWord(1024, 511));
    }
}
