using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSceneCaptureImageCacheTests
{
    [Theory]
    [InlineData(0, 0, 0, NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(0, 2, 0, NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(0, 2, 1, NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(1, 0, 0, NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(1, 2, 0, NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(1, 2, 1, NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(2, 0, 0, NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(2, 0, 0, NativeBattlePcDirectTextureFormat.Rgb565)]
    [InlineData(2, 2, 1, NativeBattlePcDirectTextureFormat.Rgb555)]
    public void Repeated_source_reuses_the_image_and_matches_scalar_native_decoding(int depth, int blend, byte shift, NativeBattlePcDirectTextureFormat format)
    {
        NativeBattlePreparedPrimitiveFrame frame = Frame(depth, blend, shift);
        NativeBattlePrimitiveCapabilities capabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { DirectTextureFormat = format };
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        PixelImage first = Render(renderer, capture, frame, capabilities);
        Assert.Equal(ReferencePixels(frame, format), first.Pixels);
        Assert.Same(first, Render(renderer, capture, frame, capabilities));
    }

    [Fact]
    public void Malformed_second_source_page_payload_is_rejected_before_image_retention()
    {
        NativeBattlePreparedPrimitiveFrame frame = Frame(0, 0, 0);
        var pages = frame.LastTextureSnapshots!.Value.ToBuilder();
        NativeBattlePcTextureWindowPixels page = pages[1];
        pages[1] = page with { Data = page.Data.RemoveAt(page.Data.Length - 1) };
        NativeBattlePreparedPrimitiveFrame malformed = frame with { LastTextureSnapshots = pages.ToImmutable() };
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);

        Assert.Throws<InvalidDataException>(() => Render(renderer, capture, malformed));
        Assert.Equal(ReferencePixels(frame, NativeBattlePcDirectTextureFormat.Rgb555), Render(renderer, capture, frame).Pixels);
    }

    [Fact]
    public void Reusing_sample_indices_with_changed_snapshot_words_cannot_reuse_the_old_image()
    {
        NativeBattlePreparedPrimitiveFrame frame = Frame(0, 0, 0);
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        PixelImage first = Render(renderer, capture, frame);
        var pages = frame.LastTextureSnapshots!.Value.ToBuilder();
        NativeBattlePcTextureWindowPixels page = pages[0];
        pages[0] = page with { Data = page.Data.SetItem(0, (byte)(page.Data[0] ^ 1)) };
        NativeBattlePreparedPrimitiveFrame changed = frame with { LastTextureSnapshots = pages.ToImmutable() };
        Assert.Same(frame.LastCommands[0].DrawMode.SceneCaptureSample, changed.LastCommands[0].DrawMode.SceneCaptureSample);
        PixelImage second = Render(renderer, capture, changed);
        Assert.NotSame(first, second);
        Assert.NotEqual(first.Pixels[0], second.Pixels[0]);
        Assert.Equal(ReferencePixels(changed, NativeBattlePcDirectTextureFormat.Rgb555), second.Pixels);
    }

    [Fact]
    public void Changed_palette_blend_shift_and_device_format_use_their_own_exact_images()
    {
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        NativeBattlePreparedPrimitiveFrame indexed = Frame(0, 0, 0);
        PixelImage first = Render(renderer, capture, indexed);
        NativeBattleLogicalResolvedCommand command = indexed.LastCommands[0];
        NativeBattleSceneCaptureSample sample = command.DrawMode.SceneCaptureSample!;
        NativeBattlePreparedPrimitiveFrame paletteChanged = indexed with { LastCommands = [command with { DrawMode = command.DrawMode with { SceneCaptureSample = sample with { Palette = sample.Palette.SetItem(2, 0x7FFF) } } }] };
        PixelImage paletteImage = Render(renderer, capture, paletteChanged);
        Assert.NotSame(first, paletteImage);
        Assert.Equal(ReferencePixels(paletteChanged, NativeBattlePcDirectTextureFormat.Rgb555), paletteImage.Pixels);
        NativeBattlePreparedPrimitiveFrame alternate = indexed with { LastCommands = [command with { DrawMode = command.DrawMode with { TexturePage = (ushort)(command.DrawMode.TexturePage!.Value | 0x40) } }] };
        PixelImage alternateImage = Render(renderer, capture, alternate);
        Assert.NotSame(first, alternateImage);
        Assert.Equal(ReferencePixels(alternate, NativeBattlePcDirectTextureFormat.Rgb555), alternateImage.Pixels);
        NativeBattlePreparedPrimitiveFrame shifted = alternate with { LastColorState = alternate.LastColorState! with { AlternatePaletteShift = 1 } };
        PixelImage shiftedImage = Render(renderer, capture, shifted);
        Assert.NotSame(alternateImage, shiftedImage);
        Assert.Equal(ReferencePixels(shifted, NativeBattlePcDirectTextureFormat.Rgb555), shiftedImage.Pixels);
        NativeBattlePreparedPrimitiveFrame direct = Frame(2, 0, 0);
        PixelImage rgb555 = Render(renderer, capture, direct);
        PixelImage rgb565 = Render(renderer, capture, direct, NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { DirectTextureFormat = NativeBattlePcDirectTextureFormat.Rgb565 });
        Assert.NotSame(rgb555, rgb565);
        Assert.Equal(ReferencePixels(direct, NativeBattlePcDirectTextureFormat.Rgb565), rgb565.Pixels);
    }

    [Fact]
    public void Warm_cache_does_not_hide_an_unsupported_alpha_palette_profile()
    {
        NativeBattlePreparedPrimitiveFrame frame = Frame(0, 2, 0);
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        Render(renderer, capture, frame);
        NativeBattlePreparedPrimitiveFrame unsupported = frame with { LastColorState = frame.LastColorState! with { SubtractUsesNormalizedColor = true } };
        Assert.Throws<NotSupportedException>(() => Render(renderer, capture, unsupported));
        Assert.Throws<NotSupportedException>(() => Render(renderer, capture, frame with { LastColorState = frame.LastColorState! with { AlternatePaletteShift = null } }));
        Assert.Throws<NotSupportedException>(() => Render(renderer, capture, Frame(2, 0, 0), NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { DirectTextureFormat = null }));
    }

    [Fact]
    public void Count_limit_evicts_the_least_recently_used_image()
    {
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        NativeBattlePreparedPrimitiveFrame[] frames = Enumerable.Range(0, 65).Select(seed => Frame(2, 0, 0, seed)).ToArray();
        PixelImage first = Render(renderer, capture, frames[0]);
        PixelImage second = Render(renderer, capture, frames[1]);
        for (int index = 2; index < 64; index++)
        {
            Render(renderer, capture, frames[index]);
        }
        Assert.Same(first, Render(renderer, capture, frames[0]));
        Render(renderer, capture, frames[64]);
        Assert.Same(first, Render(renderer, capture, frames[0]));
        Assert.NotSame(second, Render(renderer, capture, frames[1]));
    }

    [Fact]
    public void Many_palette_images_charge_the_shared_snapshot_owner_once()
    {
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        NativeBattlePreparedPrimitiveFrame frame = Frame(0, 0, 0);
        PixelImage first = Render(renderer, capture, frame);
        for (int seed = 1; seed < 60; seed++)
        {
            Render(renderer, capture, PaletteVariant(frame, seed));
        }
        Assert.Same(first, Render(renderer, capture, frame));
    }

    [Fact]
    public void Snapshot_owner_stays_charged_until_its_last_image_is_evicted_and_is_charged_again_on_return()
    {
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        NativeBattlePreparedPrimitiveFrame ownerA = WithAdditionalSnapshotPages(Frame(2, 0, 0), 320);
        NativeBattleLogicalResolvedCommand commandA = ownerA.LastCommands[0];
        NativeBattlePreparedPrimitiveFrame ownerASecond = ownerA with { LastCommands = [commandA with { DrawMode = commandA.DrawMode with { TexturePage = (ushort)(commandA.DrawMode.TexturePage!.Value | 0x20) } }] };
        NativeBattlePreparedPrimitiveFrame ownerB = WithAdditionalSnapshotPages(Frame(2, 0, 0, 1), 176);
        NativeBattlePreparedPrimitiveFrame ownerC = WithAdditionalSnapshotPages(Frame(2, 0, 0, 2), 16);
        PixelImage firstA = Render(renderer, capture, ownerA);
        PixelImage secondA = Render(renderer, capture, ownerASecond);
        PixelImage firstB = Render(renderer, capture, ownerB);
        Render(renderer, capture, ownerC);
        PixelImage returnedA = Render(renderer, capture, ownerASecond);
        Assert.NotSame(secondA, returnedA);
        Assert.NotSame(firstB, Render(renderer, capture, ownerB));
        Assert.NotSame(firstA, Render(renderer, capture, ownerA));
    }

    [Fact]
    public void Admission_recalculates_owner_cost_when_eviction_removes_the_incoming_owners_last_reference()
    {
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        NativeBattlePreparedPrimitiveFrame ownerA = WithAdditionalSnapshotPages(Frame(2, 0, 0), 256);
        NativeBattlePreparedPrimitiveFrame ownerB = WithAdditionalSnapshotPages(Frame(0, 0, 0), 240);
        NativeBattlePreparedPrimitiveFrame largerImageA = Frame(0, 0, 0) with { LastTextureSnapshots = ownerA.LastTextureSnapshots };
        Render(renderer, capture, ownerA);
        PixelImage firstB = Render(renderer, capture, ownerB);
        Render(renderer, capture, largerImageA);
        Assert.NotSame(firstB, Render(renderer, capture, ownerB));
    }

    [Fact]
    public void Byte_limit_evicts_large_images_before_the_count_limit()
    {
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        NativeBattlePreparedPrimitiveFrame firstFrame = Frame(0, 0, 0);
        PixelImage first = Render(renderer, capture, firstFrame);
        for (int seed = 1; seed < 60; seed++)
        {
            Render(renderer, capture, Frame(0, 0, 0, seed));
        }
        Assert.NotSame(first, Render(renderer, capture, firstFrame));
    }

    private static NativeBattlePreparedPrimitiveFrame PaletteVariant(NativeBattlePreparedPrimitiveFrame frame, int seed)
    {
        NativeBattleLogicalResolvedCommand command = frame.LastCommands[0];
        NativeBattleSceneCaptureSample sample = command.DrawMode.SceneCaptureSample!;
        ImmutableArray<ushort> palette = sample.Palette.Select(word => unchecked((ushort)(word + seed))).ToImmutableArray();
        return frame with { LastCommands = [command with { DrawMode = command.DrawMode with { SceneCaptureSample = sample with { Palette = palette } } }] };
    }

    private static NativeBattlePreparedPrimitiveFrame WithAdditionalSnapshotPages(NativeBattlePreparedPrimitiveFrame frame, int count)
    {
        var snapshots = frame.LastTextureSnapshots!.Value.ToBuilder();
        for (int index = 0; index < count; index++)
        {
            snapshots.Add(new(0, 0, 256, 256, new byte[256 * 256 * 2].ToImmutableArray()));
        }
        return frame with { LastTextureSnapshots = snapshots.ToImmutable() };
    }

    private static NativeBattlePreparedPrimitiveFrame Frame(int depth, int blend, byte shift, int seed = 0)
    {
        NativeBattleSceneCapture capture = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.ScreenSample) { X = 576, Y = 256 };
        ImmutableArray<NativeBattlePcTextureWindowPixels>.Builder snapshots = ImmutableArray.CreateBuilder<NativeBattlePcTextureWindowPixels>();
        for (int left = 0; left < 320; left += 256)
        {
            int width = Math.Min(256, 320 - left);
            byte[] data = new byte[width * 216 * 2];
            for (int y = 0; y < 216; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2 * (y * width + x), 2), unchecked((ushort)(0x3210 + (left + x) * 37 + y * 71 + seed)));
                }
            }
            snapshots.Add(new(576 + left, 256, width, 216, data.ToImmutableArray()));
        }
        int paletteLength = depth == 0 ? 16 : depth == 1 ? 256 : 0;
        ImmutableArray<ushort> palette = Enumerable.Range(0, paletteLength).Select(index => index == 0 ? (ushort)0 : index == 1 ? (ushort)0x8000 : unchecked((ushort)(index * 1237))).ToImmutableArray();
        ushort page = (ushort)(0x19 | depth << 7 | blend << 5);
        NativeBattleLogicalCommand quad = new(new("scene", 0, 0), 1, 0x2C, [0x2C808080, 0, 0, 320, (uint)page << 16 | 255, 216u << 16, 0xFF00, 320u | 216u << 16, 0xFFFF], 1, [0, 0, 0, 0], null) { OrderingBucket = 17, SceneCapture = capture };
        NativeBattlePrimitiveDrawMode mode = new(page) { TextureWindowBits = 0, SceneCaptureSample = new(capture, [0, 1]) { Palette = palette } };
        return new([new(quad, mode, 1)], []) { LastTextureSnapshots = snapshots.ToImmutable(), LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() with { AlternatePaletteShift = shift } };
    }

    private static Rgba32[] ReferencePixels(NativeBattlePreparedPrimitiveFrame frame, NativeBattlePcDirectTextureFormat format)
    {
        NativeBattleLogicalResolvedCommand command = frame.LastCommands[0];
        NativeBattleSceneCaptureSample sample = command.DrawMode.SceneCaptureSample!;
        int depth = command.DrawMode.TexturePage!.Value >> 7 & 3;
        int pixelsPerWord = 4 >> depth;
        Rgba32[] expected = new Rgba32[320 * pixelsPerWord * 216];
        for (int y = 0; y < 216; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int part = x / 256;
                ushort word = frame.LastTextureSnapshots!.Value[sample.PixelSnapshots[part]].ReadWord(576 + x, 256 + y);
                for (int subpixel = 0; subpixel < pixelsPerWord; subpixel++)
                {
                    ushort color = depth == 2 ? word : sample.Palette[word >> (subpixel * (16 / pixelsPerWord)) & ((1 << (16 / pixelsPerWord)) - 1)];
                    uint argb = depth == 2 ? NativeBattlePcDirectTexture.DecodeArgb(color, format) : command.DrawMode.BlendMode == 2 ? NativeBattlePcPalette.Decode467470(color, frame.LastColorState!.AlternatePaletteShift!.Value) : NativeBattlePcPalette.Decode467370(color);
                    expected[y * 320 * pixelsPerWord + x * pixelsPerWord + subpixel] = new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
                }
            }
        }
        return expected;
    }

    private static BattleRenderer Renderer(Capture capture) => new(320, 216, new PixelBounds(0, 0, 320, 216)) { TriangleRenderer = capture };

    private static PixelImage Render(BattleRenderer renderer, Capture capture, NativeBattlePreparedPrimitiveFrame frame, NativeBattlePrimitiveCapabilities? capabilities = null)
    {
        renderer.RenderNativePrimitiveFrame(frame, NativeBattleEffectTextureState.FromColdProcess(), capabilities: capabilities ?? NativeBattlePrimitiveCapabilities.PrimaryRgba8());
        return Assert.Single(capture.Images.Distinct());
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<PixelImage> Images { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Images.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (material.Texture is { } image)
            {
                Images.Add(image);
            }
        }

        public void End()
        {
        }
    }
}
