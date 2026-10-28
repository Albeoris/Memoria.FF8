using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeBattlePrefixGpuTests
{
    [Fact]
    public void A_full_query_effect_replacement_still_overrides_the_model_material_on_GPU()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        (NativeBattlePreparedPrimitiveFrame frame, NativeBattleEffectTextureState textures) = NativeBattlePrefixFixture.Create(63, 63);
        frame = NativeBattlePrefixFixture.WithProvenance(frame);
        using InMemoryResourceSource resources = new(new("fixture", GameDataEdition.Classic, ["classic-ffnx"]));
        ResourceService service = new(resources);
        service.Prepare(new NativeBattlePrefixFixture.RedResolver()).Commit();
        using NativeBattleEffectTextureSource effects = new(service);
        BattleRenderer renderer = new(64, 64) { TriangleRenderer = gpu, PilotDiagnosticsEnabled = true };
        renderer.RenderNativePrimitiveFrame(frame, textures, replacements: [NativeBattlePrefixFixture.WhiteReplacement(frame, textures)], effectTextureSource: effects);
        var visible = GpuTestDevice.Read(gpu.Target).Where(pixel => pixel.R > 0 || pixel.G > 0 || pixel.B > 0).ToArray();
        Assert.NotEmpty(visible);
        Assert.All(visible, pixel =>
        {
            Assert.True(pixel.R > 0);
            Assert.Equal(0, pixel.G);
            Assert.Equal(0, pixel.B);
        });
        Assert.Equal(0, renderer.TextureDecoding!.Value.PrefixEligibleCommands);
    }

    [Fact]
    public void The_no_bleed_fixture_exposes_legacy_pixel_center_texture_extrapolation()
    {
        using GpuTestDevice device = new(8);
        using GpuTriangleRenderer gpu = new(device.Device);
        (NativeBattlePreparedPrimitiveFrame frame, NativeBattleEffectTextureState textures) = NativeBattlePrefixFixture.CreateMsaaEdge(0, 0, noBleed: true);
        BattleRenderer renderer = new(64, 64) { TriangleRenderer = new PixelCenterTextureRenderer(gpu) };
        renderer.RenderNativePrimitiveFrame(frame, textures, effectTextureSource: new NativeBattlePrefixFixture.FullPathSource());
        Assert.True(gpu.Target.MultiSampleCount > 0);
        Assert.True(GpuTestDevice.Read(gpu.Target).Any(pixel => pixel.G != 0 || pixel.B != 0), "The independent no-bleed fixture must detect the legacy center-sampled texture behavior.");
    }

    private sealed class PixelCenterTextureRenderer(GpuTriangleRenderer renderer) : ITriangleRenderer
    {
        public void Begin(int width, int height, System.Numerics.Vector4 clearColor, float clearDepth = 1)
        {
            renderer.Begin(width, height, clearColor, clearDepth);
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            renderer.Draw(a, b, c, material with { CentroidTextureSampling = false });
        }

        public void End()
        {
            renderer.End();
        }
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(0, 0.125f)]
    [InlineData(0, 0.375f)]
    [InlineData(1, 0f)]
    [InlineData(1, 0.125f)]
    [InlineData(1, 0.375f)]
    [InlineData(2, 0f)]
    [InlineData(2, 0.125f)]
    [InlineData(2, 0.375f)]
    [InlineData(3, 0f)]
    [InlineData(3, 0.125f)]
    [InlineData(3, 0.375f)]
    public void Full_page_native_polygons_do_not_bleed_unreached_texture_colors_at_MSAA_edges(int shape, float phase)
    {
        using GpuTestDevice device = new(8);
        using GpuTriangleRenderer gpu = new(device.Device);
        (NativeBattlePreparedPrimitiveFrame frame, NativeBattleEffectTextureState textures) = NativeBattlePrefixFixture.CreateMsaaEdge(shape, phase, noBleed: true);
        BattleRenderer renderer = new(64, 64) { TriangleRenderer = gpu, PilotDiagnosticsEnabled = true };

        // Force the complete 256x256 image: this golden does not compare two
        // implementations that could share the same erroneous crop sampling.
        renderer.RenderNativePrimitiveFrame(frame, textures, effectTextureSource: new NativeBattlePrefixFixture.FullPathSource());

        Assert.True(gpu.Target.MultiSampleCount > 0, "The no-bleed golden must actually use an MSAA render target.");
        Assert.Equal(0, renderer.TextureDecoding!.Value.PrefixDecodedImages);
        var pixels = GpuTestDevice.Read(gpu.Target);
        Assert.True(pixels.Any(pixel => pixel.R > 0), "The fixture must cover actual MSAA samples of the red UV domain.");
        int contaminated = pixels.Count(pixel => pixel.G != 0 || pixel.B != 0);
        Assert.True(contaminated == 0, $"Shape {shape}, phase {phase}, MSAA {gpu.Target.MultiSampleCount}: {contaminated} pixels sampled green/blue outside the authored red 0..63 UV domain.");
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(0, 0.125f)]
    [InlineData(0, 0.375f)]
    [InlineData(1, 0f)]
    [InlineData(1, 0.125f)]
    [InlineData(1, 0.375f)]
    [InlineData(2, 0f)]
    [InlineData(2, 0.125f)]
    [InlineData(2, 0.375f)]
    [InlineData(3, 0f)]
    [InlineData(3, 0.125f)]
    [InlineData(3, 0.375f)]
    public void Affine_prefix_preserves_full_page_samples_on_MSAA_thin_and_subpixel_edges(int shape, float phase)
    {
        using GpuTestDevice device = new(8);
        using GpuTriangleRenderer fullGpu = new(device.Device);
        using GpuTriangleRenderer prefixGpu = new(device.Device);
        (NativeBattlePreparedPrimitiveFrame frame, NativeBattleEffectTextureState textures) = NativeBattlePrefixFixture.CreateMsaaEdge(shape, phase);
        BattleRenderer full = new(64, 64) { TriangleRenderer = fullGpu };
        BattleRenderer prefix = new(64, 64) { TriangleRenderer = prefixGpu, PilotDiagnosticsEnabled = true };

        full.RenderNativePrimitiveFrame(frame, textures, effectTextureSource: new NativeBattlePrefixFixture.FullPathSource());
        prefix.RenderNativePrimitiveFrame(frame, textures);

        Assert.True(fullGpu.Target.MultiSampleCount > 0, "The full-page reference must actually use an MSAA render target.");
        Assert.True(prefixGpu.Target.MultiSampleCount > 0, "The prefix render must actually use an MSAA render target.");
        Assert.Equal(fullGpu.Target.MultiSampleCount, prefixGpu.Target.MultiSampleCount);
        var expected = GpuTestDevice.Read(fullGpu.Target);
        var actual = GpuTestDevice.Read(prefixGpu.Target);
        Assert.True(expected.Any(pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0), "The edge fixture must cover actual MSAA samples.");
        Assert.Equal(1, prefix.TextureDecoding!.Value.PrefixDecodedImages);
        Assert.Equal(64 * 64, prefix.TextureDecoding.Value.PrefixCroppedPixels);
        int mismatches = 0;
        int first = -1;
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
            {
                if (first < 0)
                {
                    first = index;
                }
                mismatches++;
            }
        }
        Assert.True(mismatches == 0, $"Shape {shape}, phase {phase}, MSAA {fullGpu.Target.MultiSampleCount}: {mismatches} differing resolved pixels; first ({first % 64}, {first / 64}) full={expected[Math.Max(0, first)]}, prefix={actual[Math.Max(0, first)]}.");
    }

    [Theory]
    [InlineData(63, 31)]
    [InlineData(64, 64)]
    [InlineData(127, 128)]
    [InlineData(255, 63)]
    [InlineData(255, 255)]
    public void Affine_prefix_and_full_page_have_identical_GPU_point_samples_at_power_of_two_edges(int maximumU, int maximumV)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer fullGpu = new(device.Device);
        using GpuTriangleRenderer prefixGpu = new(device.Device);
        (NativeBattlePreparedPrimitiveFrame frame, NativeBattleEffectTextureState textures) = NativeBattlePrefixFixture.Create(maximumU, maximumV);
        BattleRenderer full = new(64, 64) { TriangleRenderer = fullGpu };
        BattleRenderer prefix = new(64, 64) { TriangleRenderer = prefixGpu, PilotDiagnosticsEnabled = true };
        full.RenderNativePrimitiveFrame(frame, textures, effectTextureSource: new NativeBattlePrefixFixture.FullPathSource());
        prefix.RenderNativePrimitiveFrame(frame, textures);
        var expected = GpuTestDevice.Read(fullGpu.Target);
        var actual = GpuTestDevice.Read(prefixGpu.Target);
        Assert.Equal(expected, actual);
        Assert.True(expected.Select(pixel => (pixel.R, pixel.G, pixel.B)).Distinct().Count() > 4);
        Assert.Equal(maximumU == 255 && maximumV == 255 ? 0 : 1, prefix.TextureDecoding!.Value.PrefixDecodedImages);
    }
}

internal static class NativeBattlePrefixFixture
{
    internal static NativeBattlePreparedPrimitiveFrame WithProvenance(NativeBattlePreparedPrimitiveFrame frame)
    {
        NativeBattlePcTextureWindowPixels pixels = frame.LastTextureSnapshots!.Value[0];
        NativeBattleTextureOrigin origin = new("mag012_b.00", 0, NativeBattleTextureRole.Pixels, 4, 64, 256);
        return frame with { LastTextureSnapshots = [pixels with { LiveTextureSources = [new(origin, 0, 0, 64, 256)], TextureRegistrations = [] }] };
    }

    internal static NativeBattleTextureReplacement WhiteReplacement(NativeBattlePreparedPrimitiveFrame frame, NativeBattleEffectTextureState textures)
    {
        byte[] palette = new byte[32];
        textures.ReadVramInto(2 * 256 * 1024, palette);
        return new(0, 0x4000, 0, 0, 256, 256, new(256, 256, Enumerable.Repeat(new Rgba32(255, 255, 255, 127), 65536).ToArray()), 64, frame.LastTextureSnapshots!.Value[0].Data, palette.ToImmutableArray());
    }

    internal sealed class RedResolver : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Assert.Contains("/window/0/0/256/256/", context.Key.Subresource);
            Assert.Equal(256, context.LogicalWidth);
            Assert.Equal(256, context.LogicalHeight);
            return new(new(256, 256, Enumerable.Repeat(new Rgba32(255, 0, 0, 127), 65536).ToArray()), "fixture:red", 256, 256);
        }
    }

    internal static (NativeBattlePreparedPrimitiveFrame Frame, NativeBattleEffectTextureState Textures) Create(int maximumU, int maximumV, bool perspective = false)
    {
        byte[] words = new byte[64 * 256 * 2];
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                int index = 1 + (x + y * 7) % 15;
                words[y * 128 + x / 2] |= (byte)(index << ((x & 1) * 4));
            }
        }
        byte[] palette = new byte[32];
        for (int index = 1; index < 16; index++)
        {
            ushort color = index % 3 == 0 ? (ushort)0x7C00 : index % 3 == 1 ? (ushort)0x001F : (ushort)0x03E0;
            if ((index & 1) != 0)
            {
                color |= 0x4210;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(index * 2), color);
        }
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 64, 256, words).Upload45BD30(0, 256, 16, 1, palette);
        NativeBattlePcTextureWindowPixels pixels = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 256, 256), textures);
        NativeBattleLogicalCommand command = new(new("fixture", 0, 0), 1, 0x24,
            [0x24808080, Point(11, 9), 0x40000000, Point(307, 17), (uint)maximumU, Point(23, 207), (uint)maximumV << 8],
            0, perspective ? [100, 200, 150, 100] : [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveDrawMode mode = new(0)
        {
            TextureWindowBits = 0,
            TexturePixelSnapshot = 0,
            TextureSelection = new(0, 0, 0, 0, 256, 256, 1f / 256, 1f / 256, false, false) { UsesRetainedPixelWords = true }
        };
        return (new([new(command, mode, 1)], [])
        {
            LastTextureSnapshots = [pixels],
            LastDescriptorPerspective = perspective,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        }, textures);
    }

    internal static (NativeBattlePreparedPrimitiveFrame Frame, NativeBattleEffectTextureState Textures) CreateMsaaEdge(int shape, float phase, bool noBleed = false)
    {
        // These indices are deliberately nonperiodic across the prefix boundary:
        // U=0,63,64,255 have four different colors on every row.
        static int Index(int x, int y) => 1 + (x * 11 + y * 7 + x / 64 * 3 + y / 64 * 4) % 15;
        Assert.Equal(4, new[] { Index(0, 0), Index(63, 0), Index(64, 0), Index(255, 0) }.Distinct().Count());
        Assert.Equal(4, new[] { Index(0, 0), Index(0, 63), Index(0, 64), Index(0, 255) }.Distinct().Count());
        byte[] words = new byte[64 * 256 * 2];
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                int index = noBleed ? x < 64 && y < 64 ? 1 : x >= 64 ? 2 : 3 : Index(x, y);
                words[y * 128 + x / 2] |= (byte)(index << ((x & 1) * 4));
            }
        }
        byte[] palette = new byte[32];
        for (int index = 1; index < 16; index++)
        {
            ushort color = noBleed ? index == 1 ? (ushort)0x001F : index == 2 ? (ushort)0x03E0 : (ushort)0x7C00 : (ushort)((index * 2 + 1) | ((31 - index * 2) << 5) | (((index * 7) % 31) << 10));
            BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(index * 2), color);
        }
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 64, 256, words).Upload45BD30(0, 256, 16, 1, palette);
        NativeBattlePcTextureWindowPixels pixels = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 256, 256), textures);
        (float X, float Y)[] positions = shape switch
        {
            0 => [(8.1f, 9.1f), (55.8f, 47.2f), (55.6f, 47.65f)],
            1 => [(31.1f, 31.1f), (31.9f, 31.25f), (31.2f, 31.9f)],
            2 => [(15.1f, 6.2f), (54.7f, 31.1f), (6.3f, 29.8f), (45.9f, 54.7f)],
            3 => [(8.1f, 9.1f), (55.8f, 47.2f), (7.9f, 9.6f), (55.6f, 47.7f)],
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
        bool quad = positions.Length == 4;
        ImmutableArray<uint>.Builder commandWords = ImmutableArray.CreateBuilder<uint>(quad ? 9 : 7);
        commandWords.Add(quad ? 0x2C808080u : 0x24808080u);
        for (int corner = 0; corner < positions.Length; corner++)
        {
            (float x, float y) = positions[corner];
            int packedX = (int)MathF.Round((x + phase) * 40);
            int packedY = (int)MathF.Round((y + phase) * 27);
            commandWords.Add((uint)(ushort)packedX | (uint)(ushort)packedY << 16);
            uint uv = corner switch
            {
                0 => 0u,
                1 => 63u,
                2 => 63u << 8,
                _ => 63u | 63u << 8
            };
            commandWords.Add(uv | (corner == 0 ? 0x40000000u : 0));
        }
        (NativeBattlePreparedPrimitiveFrame frame, _) = Create(63, 63);
        NativeBattleLogicalResolvedCommand template = frame.LastCommands[0];
        NativeBattleLogicalCommand command = template.Command with { Opcode = quad ? (byte)0x2C : (byte)0x24, Words = commandWords.MoveToImmutable() };
        return (frame with { LastCommands = [template with { Command = command }], LastTextureSnapshots = [pixels] }, textures);
    }

    private static uint Point(int x, int y) => (uint)(ushort)(x * 8) | (uint)(ushort)(y * 8) << 16;

    internal sealed class FullPathSource : INativeBattleEffectTextureSource
    {
        public long Generation => 0;
        internal int Calls { get; private set; }

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            Calls++;
            return original;
        }
    }
}
