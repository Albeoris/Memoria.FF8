using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRawTextureContentTests
{
    [Fact]
    public void Inspector_distinguishes_reference_aliases_equal_contents_dimensions_and_colors_without_mutating_cache()
    {
        BattleRenderer renderer = new(320, 216);
        Assert.Equal(0, renderer.InspectNativeRawTextureContent().RawEntries);
        PixelImage first = new(2, 2, Enumerable.Repeat(new Rgba32(1, 2, 3, 4), 4).ToArray());
        Add(renderer, 0, first);
        Add(renderer, 1, new(2, 2, first.Pixels.ToArray()));
        Add(renderer, 2, first);
        Add(renderer, 3, new(1, 4, first.Pixels.ToArray()));
        Add(renderer, 4, new(2, 2, Enumerable.Repeat(new Rgba32(1, 2, 3, 5), 4).ToArray()));
        Rgba32[] original = first.Pixels.ToArray();

        NativeBattleRawTextureContentSummary result = renderer.InspectNativeRawTextureContent();

        Assert.Equal("RetainedRawCacheAfterFinalPresent", result.Scope);
        Assert.Equal(5, result.RawEntries);
        Assert.Equal(4, result.ReferenceUniqueImages);
        Assert.Equal(3, result.ContentUniqueImages);
        Assert.Equal(64, result.ReferenceUniqueImageBytes);
        Assert.Equal(48, result.ContentUniqueImageBytes);
        Assert.Equal(16, result.DuplicateImageBytes);
        Assert.Equal(5, result.EntriesUsedInLastRender);
        Assert.Equal(result, renderer.InspectNativeRawTextureContent());
        Assert.Equal(original, first.Pixels);
        renderer.ClearNativeRawTextureCache();
        Assert.Equal(0, renderer.InspectNativeRawTextureContent().RawEntries);
        Assert.Equal(0, renderer.InspectNativeRawTextureContent().DuplicateImageBytes);
    }

    [Fact]
    public void Inspector_reports_only_surviving_lru_entries()
    {
        BattleRenderer renderer = new(320, 216);
        for (int index = 0; index < 257; index++)
        {
            Add(renderer, index, new(1, 1, [new Rgba32((byte)index, (byte)(index >> 8), 0)]));
        }
        NativeBattleRawTextureContentSummary result = renderer.InspectNativeRawTextureContent();
        Assert.Equal(256, result.RawEntries);
        Assert.Equal(256, result.ReferenceUniqueImages);
        Assert.Equal(256, result.ContentUniqueImages);
        Assert.Equal(1024, result.ContentUniqueImageBytes);
        Assert.Equal(0, result.DuplicateImageBytes);
    }

    private static void Add(BattleRenderer renderer, int identity, PixelImage image)
    {
        Type keyType = typeof(BattleRenderer).GetNestedType("NativeRawTextureKey", BindingFlags.NonPublic)!;
        object key = Activator.CreateInstance(keyType, [(ushort)0x100, (ushort)identity, null, 0, 0, image.Width, image.Height, NativeBattlePcDirectTextureFormat.Rgb555])!;
        NativeBattlePcTextureWindowPixels pixels = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, image.Width, image.Height), NativeBattleEffectTextureState.FromColdProcess());
        typeof(BattleRenderer).GetMethod("RetainNativeRawTexture", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(renderer, [key, image, pixels, null]);
    }
}
