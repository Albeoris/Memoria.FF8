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
