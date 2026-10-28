using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class FfnxTextureResolverCacheTests
{
    [Fact]
    public void Cropped_effect_results_reuse_normalized_pixels_without_mutating_decoded_source_or_logical_dimensions()
    {
        ModFile file = File("magic/mag012.tim-0_00.dds", 4, 4, 25);
        FfnxTextureResolver resolver = new(new(1, [], [file]));
        TextureReplacement first = resolver.Resolve(Context(1, 1, 2, 2, 4, 4))!;
        TextureReplacement repeated = resolver.Resolve(Context(1, 1, 2, 2, 4, 4))!;
        Assert.Same(first.Image, repeated.Image);
        Assert.Equal((byte)127, repeated.Image[0, 0].A);
        Assert.Equal((byte)255, ModImageDecoder.Decode(file).Image[0, 0].A);
        TextureReplacement changedCrop = resolver.Resolve(Context(0, 0, 2, 2, 4, 4))!;
        Assert.NotSame(first.Image, changedCrop.Image);
        TextureReplacement changedLogicalSize = resolver.Resolve(Context(1, 1, 2, 2, 4, 4, logicalWidth: 8))!;
        Assert.Equal(8, changedLogicalSize.LogicalWidth);
        Assert.Equal(2, repeated.LogicalWidth);
        Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context(1, 1, 2, 2, 8, 4)));
        Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context(3, 1, 2, 2, 4, 4)));
    }

    [Fact]
    public void Exact_and_fallback_queries_preserve_palette_format_language_and_catalog_order_priorities()
    {
        ModFile baseTwo = File("magic/mag012.tim-0_02.dds", 4, 4, 10);
        ModFile englishTwo = File("eng/magic/mag012.tim-0_02.dds", 4, 4, 20);
        ModFile lowerPalette = File("magic/mag012.tim-0_01.dds", 4, 4, 30);
        FfnxTextureResolver resolver = new(new(1, [], [baseTwo, englishTwo, lowerPalette]));
        Assert.Equal((byte)20, resolver.Resolve(Context(palette: 2))!.Image[0, 0].R);
        Assert.Equal((byte)10, resolver.Resolve(Context(palette: 2, language: "fr"))!.Image[0, 0].R);
        Assert.Equal((byte)30, resolver.Resolve(Context(palette: 9))!.Image[0, 0].R);
        FfnxTextureResolver tiedFallback = new(new(1, [], [baseTwo, englishTwo]));
        Assert.Equal((byte)10, tiedFallback.Resolve(Context(palette: 9))!.Image[0, 0].R);
        Assert.Equal((byte)20, tiedFallback.Resolve(Context(palette: 2))!.Image[0, 0].R);
        ModFile preferredDds = File("magic/mag012.tim-0_00.dds", 4, 4, 40);
        ModFile unreadPng = new("png", ModFormat.Junction, "mods/textures/eng/magic/mag012.tim-0_00.png", 1, () => throw new IOException("DDS must remain preferred."));
        FfnxTextureResolver formats = new(new(1, [], [unreadPng, preferredDds]));
        Assert.Equal((byte)40, formats.Resolve(Context())!.Image[0, 0].R);
        Assert.Equal((byte)40, formats.Resolve(Context())!.Image[0, 0].R);
    }

    [Fact]
    public void Missing_queries_and_revisions_remain_scoped_to_their_immutable_catalog_and_file_identity()
    {
        FfnxTextureResolver missing = new(new(1, [], []));
        Assert.Null(missing.Resolve(Context()));
        Assert.Null(missing.Resolve(Context()));
        ModFile oldFile = File("magic/mag012.tim-0_00.dds", 4, 4, 10, revision: 1);
        ModFile newFile = File("magic/mag012.tim-0_00.dds", 4, 4, 20, revision: 2);
        TextureReplacement old = new FfnxTextureResolver(new(2, [], [oldFile])).Resolve(Context())!;
        TextureReplacement current = new FfnxTextureResolver(new(3, [], [newFile])).Resolve(Context())!;
        Assert.Equal((byte)10, old.Image[0, 0].R);
        Assert.Equal((byte)20, current.Image[0, 0].R);
        Assert.EndsWith("@2", current.Provenance);
        Assert.Null(missing.Resolve(Context()));
    }

    [Fact]
    public void Region_entry_limit_evicts_the_least_recently_used_image_while_existing_outputs_stay_valid()
    {
        FfnxTextureResolver resolver = new(new(1, [], [File("magic/mag012.tim-0_00.dds", 300, 1, 42)]));
        TextureBuildContext context = Context(0, 0, 1, 1, 300, 1);
        PixelImage first = resolver.Resolve(context)!.Image;
        PixelImage second = resolver.Resolve(Context(1, 0, 1, 1, 300, 1))!.Image;
        for (int x = 2; x < 256; x++)
        {
            resolver.Resolve(Context(x, 0, 1, 1, 300, 1));
        }
        Assert.Same(first, resolver.Resolve(context)!.Image);
        resolver.Resolve(Context(256, 0, 1, 1, 300, 1));
        Assert.Same(first, resolver.Resolve(context)!.Image);
        Assert.NotSame(second, resolver.Resolve(Context(1, 0, 1, 1, 300, 1))!.Image);
        Assert.Equal((byte)42, second[0, 0].R);
    }

    [Fact]
    public void Region_byte_budget_evicts_images_before_the_entry_limit_is_reached()
    {
        FfnxTextureResolver resolver = new(new(1, [], [File("magic/mag012.tim-0_00.dds", 1024, 1024, 51)]));
        TextureBuildContext firstContext = Context(0, 0, 512, 512, 1024, 1024);
        PixelImage first = resolver.Resolve(firstContext)!.Image;
        for (int x = 1; x <= 16; x++)
        {
            resolver.Resolve(Context(x, 0, 512, 512, 1024, 1024));
        }
        Assert.NotSame(first, resolver.Resolve(firstContext)!.Image);
        Assert.Equal((byte)51, first[0, 0].R);
    }

    [Fact]
    public void An_image_larger_than_the_byte_budget_is_returned_without_being_retained()
    {
        FfnxTextureResolver resolver = new(new(1, [], [File("magic/mag012.tim-0_00.dds", 2049, 2049, 61)]));
        TextureBuildContext context = Context(0, 0, 2049, 2049, 2049, 2049);
        PixelImage first = resolver.Resolve(context)!.Image;
        PixelImage second = resolver.Resolve(context)!.Image;
        Assert.NotSame(first, second);
        Assert.Equal(first[0, 0], second[0, 0]);
    }

    [Fact]
    public void Registered_effect_page_crops_and_clut_selection_reuse_only_their_selected_source()
    {
        ModFile first = File("magic/mag012.tim-0_0_320_224.dds", 256, 256, 70);
        ModFile other = File("magic/mag012.tim-0_0_336_224.dds", 256, 256, 80);
        FfnxTextureResolver resolver = new(new(1, [], [first, other]));
        TextureReplacement a = resolver.Resolve(PageContext(320))!;
        Assert.Same(a.Image, resolver.Resolve(PageContext(320))!.Image);
        TextureReplacement b = resolver.Resolve(PageContext(336))!;
        Assert.Equal((byte)70, a.Image[0, 0].R);
        Assert.Equal((byte)80, b.Image[0, 0].R);
    }

    private static TextureBuildContext Context(int x = 0, int y = 0, int width = 4, int height = 4, int canvasWidth = 4, int canvasHeight = 4, int palette = 0, string language = "en", int? logicalWidth = null)
    {
        return new(new(new("consumer"), "page"), new("test", GameDataEdition.Classic, ["classic-ffnx"], language), 1, "native-effect-texture", logicalWidth ?? width, height, () => throw new InvalidOperationException("Pure external-file selection must not require original pixels.")) { NativeAlias = new(new("magic/mag012.tim"), "texture/0", palette, x, y, width, height, canvasWidth, canvasHeight) };
    }

    private static TextureBuildContext PageContext(int paletteX)
    {
        TextureBuildContext source = Context(0, 0, 4, 4, 4, 4);
        return new(source.Key, source.Source, 1, source.Layout, 4, 4, () => throw new InvalidOperationException()) { NativeAlias = source.NativeAlias, NativePage = new(0, 8, 12, 4, 4, paletteX, 224, [new("effect", "mag012.tim", 0, 0, 0, 64, 256, 8, -1)]) };
    }

    private static ModFile File(string name, int width, int height, byte red, long revision = 1)
    {
        return new("test", ModFormat.Junction, "mods/textures/" + name, revision, () => Dds(width, height, red));
    }

    private static byte[] Dds(int width, int height, byte red)
    {
        byte[] bytes = new byte[checked(128 + width * height * 4)];
        Write(0, 0x20534444);
        Write(4, 124);
        Write(8, 0x100F);
        Write(12, (uint)height);
        Write(16, (uint)width);
        Write(20, (uint)width * 4);
        Write(28, 1);
        Write(76, 32);
        Write(80, 0x41);
        Write(88, 32);
        Write(92, 0xFF);
        Write(96, 0xFF00);
        Write(100, 0xFF0000);
        Write(104, 0xFF000000);
        Write(108, 0x1000);
        for (int offset = 128; offset < bytes.Length; offset += 4)
        {
            bytes[offset] = red;
            bytes[offset + 3] = 255;
        }
        return bytes;

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
