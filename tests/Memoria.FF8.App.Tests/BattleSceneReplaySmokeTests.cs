using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSceneReplaySmokeTests
{
    [Fact]
    public void Copied_scene_words_replace_only_the_proved_main_span_after_cold_restore_at_the_configured_resolution()
    {
        string? resolution = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISIBLE_RESOLUTION");
        Assert.SkipUnless(resolution is not null, "Configure the current visible resolution.");
        int[] size = resolution!.Split('x').Select(int.Parse).ToArray();
        Assert.Equal(2, size.Length);
        int width = size[0];
        int height = size[1];
        PixelBounds projection = ViewportLayout.Fit(320, 240, width, height);
        BattleSceneVisibleArea area = BattleSceneVisibleArea.FromViewport(width, height, projection.X, projection.Y, projection.Width, projection.Height);
        NativeBattleLogicalCommand red = new(new("source", 0, 0), 1, 0x28, [0x280000FF, 0, 320, 216u << 16, 320u | 216u << 16], 1, [0, 0, 0, 0], null)
        {
            OrderingBucket = 17,
            ScreenCoverage = NativeBattleScreenCoverage.Full
        };
        NativeBattleLogicalCommand edge = red with
        {
            Packet = new("source-edge", 0, 0),
            Words = [0x2800FF00, 0, 320, 216u << 16, 320u | 216u << 16],
            ScreenCoverage = new(0, 0, 0.1f, 1)
        };
        NativeBattlePreparedPrimitiveFrame sourceFrame = Frame(new(red, new(0), 1));
        sourceFrame = sourceFrame with { LastCommands = sourceFrame.LastCommands.Add(new(edge, new(0), 1)) };
        NativeBattleSceneReplaySource source = new(sourceFrame, NativeBattleEffectTextureState.FromColdProcess(), area) { Callback = 1, Buffer = 0 };
        NativeBattleSceneCapture copy = new(area, NativeBattleSceneCaptureKind.Copy)
        {
            X = 384,
            Y = 256,
            Height = 224,
            PaddingBottom = 8,
            ReplaySourceId = 1
        };
        NativeBattleSceneCopyRegion region = new(copy, 384, 256, 128, 224, 0, 0);
        byte[] blueWords = new byte[256 * 256 * 2];
        for (int index = 1; index < blueWords.Length; index += 2)
        {
            blueWords[index] = 0x7C;
        }
        NativeBattlePcTextureWindowPixels pixels = new(384, 256, 256, 256, blueWords.ToImmutableArray()) { SceneCopies = [region] };
        NativeBattleLogicalCommand consumer = new(new("consumer", 0, 0), 1, 0x2C, [0x2C808080, 0, 0, 320, 0x011600FF, 216u << 16, 0x0000FF00, 320u | 216u << 16, 0x0000FFFF], 1, [0, 0, 0, 0], null) { OrderingBucket = 17 };
        NativeBattlePrimitiveDrawMode mode = new(0x116)
        {
            TextureWindowBits = 0,
            TextureSelection = new(86, 0, 0, 0, 256, 256, 1f / 256, 1f / 256, false, false),
            TexturePixelSnapshot = 0,
            SceneCopyPalette = []
        };
        NativeBattlePreparedPrimitiveFrame frame = Frame(new(consumer, mode, 1)) with
        {
            LastTextureSnapshots = [pixels],
            SceneReplaySources = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, source)
        };
        NativeBattlePreparedPrimitiveFrame frozen = NativeBattleStorageSnapshots.Capture(frame);
        string json = JsonSerializer.Serialize(frozen);
        NativeBattlePreparedPrimitiveFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattlePreparedPrimitiveFrame>(json)!);
        Assert.Equal(1, Assert.Single(restored.SceneReplaySources!).Key);
        Assert.Null(restored.SceneReplaySources![1].Frame.SceneReplaySources);
        Capture backend = new();
        BattleRenderer renderer = new(width, height, projection) { TriangleRenderer = backend };
        renderer.RenderNativePrimitiveFrame(restored, NativeBattleEffectTextureState.FromColdProcess(), capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8());
        PixelImage image = Assert.Single(backend.Textures.Distinct());
        Assert.Equal((byte)0, image[0, 0].R);
        Assert.Equal((byte)255, image[0, 0].G);
        Assert.Equal((byte)0, image[0, 0].B);
        AssertRed(image[127, 215]);
        AssertBlue(image[128, 0]);
        AssertBlue(image[255, 215]);
        AssertBlue(image[0, 216]);
        AssertBlue(image[127, 223]);
        AssertBlue(image[0, 224]);
        Assert.Equal((byte)0, pixels.Data[0]);
        Assert.Equal((byte)0x7C, pixels.Data[1]);
    }

    private static NativeBattlePreparedPrimitiveFrame Frame(NativeBattleLogicalResolvedCommand command)
    {
        return new([command], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
    }

    private static void AssertRed(Rgba32 pixel)
    {
        Assert.Equal((byte)255, pixel.R);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)0, pixel.B);
    }

    private static void AssertBlue(Rgba32 pixel)
    {
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)255, pixel.B);
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<PixelImage> Textures { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Textures.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (material.Texture is { } texture)
            {
                Textures.Add(texture);
            }
        }

        public void End()
        {
        }
    }
}
