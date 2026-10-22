using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class BattleExpandedViewportTests
{
    [Theory]
    [InlineData(480, 240)]
    [InlineData(840, 240)]
    [InlineData(320, 400)]
    public void Live_scene_uses_the_fitted_projection_for_its_classic_stage(int width, int height)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        BattleDefinition definition = new()
        {
            StageId = 0,
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0 }, new() { Id = "enemy", Name = "Opponent", ModelId = 1 }]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, false);
        CaptureBackend actual = new();
        scene.Render(session, width, height, actual);
        CaptureBackend expected = new();
        BattleRenderer renderer = new(width, height, ViewportLayout.Fit(320, 240, width, height)) { TriangleRenderer = expected };
        renderer.Render(assets.LoadStage(0), [], scene.Camera);
        Assert.NotEmpty(expected.Triangles);
        Assert.Equal(expected.Triangles, actual.Triangles.Take(expected.Triangles.Count));
    }

    [Theory]
    [InlineData(480, 240, 80, 0)]
    [InlineData(840, 240, 260, 0)]
    [InlineData(320, 400, 0, 80)]
    public void Native_coordinates_keep_their_scale_and_center_in_the_fitted_projection(int width, int height, int left, int top)
    {
        PixelBounds bounds = ViewportLayout.Fit(320, 240, width, height);
        Assert.Equal(new PixelBounds(left, top, 320, 240), bounds);
        foreach (bool normalViewport in new[] { false, true })
        {
            foreach (ushort flags in new ushort[] { 0, 1 })
            {
                CaptureBackend backend = new();
                BattleRenderer renderer = new(width, height, bounds) { TriangleRenderer = backend };
                renderer.RenderNativePrimitiveFrame(Frame(80, 54, normalViewport, flags), null);
                var triangle = Assert.Single(backend.Triangles);
                Assert.Equal(new Vector2(left + 80, top + 60), triangle.A.Position);
                Assert.Equal(new Vector2(left + 100, top + 60), triangle.B.Position);
                Assert.Equal(new Vector2(left + 80, top + 80), triangle.C.Position);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expanded_raster_reveals_geometry_outside_the_native_horizontal_and_vertical_edges(bool normalViewport)
    {
        foreach (var sample in new[] { (Width: 480, Height: 240, X: 340, Y: 80), (Width: 320, Height: 400, X: 120, Y: -36) })
        {
            NativeBattlePrimitiveFrame frame = Frame(sample.X, sample.Y, normalViewport, 1);
            byte[] native = new BattleRenderer(320, 240).RenderNativePrimitiveFrame(frame, null);
            Assert.DoesNotContain(Enumerable.Range(0, native.Length / 4), index => native[index * 4] > 0);
            PixelBounds bounds = ViewportLayout.Fit(320, 240, sample.Width, sample.Height);
            BattleRenderer renderer = new(sample.Width, sample.Height, bounds);
            byte[] expanded = renderer.RenderNativePrimitiveFrame(frame, null).ToArray();
            int x = bounds.X + sample.X + 3;
            int y = bounds.Y + sample.Y * 240 / 216 + 3;
            int pixel = y * sample.Width + x;
            Assert.True(expanded[pixel * 4] > 0);
            using GpuTestDevice device = new();
            using GpuTriangleRenderer gpu = new(device.Device);
            renderer.TriangleRenderer = gpu;
            renderer.RenderNativePrimitiveFrame(frame, null);
            Microsoft.Xna.Framework.Color actual = GpuTestDevice.Read(gpu.Target)[pixel];
            Assert.Equal(expanded[pixel * 4], actual.R);
            Assert.Equal(expanded[pixel * 4 + 1], actual.G);
            Assert.Equal(expanded[pixel * 4 + 2], actual.B);
        }
    }

    [Fact]
    public void Four_by_three_projection_preserves_raw_native_pixels()
    {
        NativeBattlePrimitiveFrame frame = Frame(80, 54, true, 1);
        Assert.Equal(new BattleRenderer(640, 480).RenderNativePrimitiveFrame(frame, null), new BattleRenderer(640, 480, ViewportLayout.Fit(320, 240, 640, 480)).RenderNativePrimitiveFrame(frame, null));
    }

    [Theory]
    [InlineData(480, 240)]
    [InlineData(840, 240)]
    [InlineData(320, 400)]
    public void Classic_projection_preserves_model_scale_when_the_target_expands(int width, int height)
    {
        BattleCamera camera = new(Vector3.Zero, -Vector3.UnitZ, 1, 0.45f);
        BattleStage stage = new(0, [], []);
        BattleRenderer native = new(320, 240);
        BattleRenderer expanded = new(width, height, ViewportLayout.Fit(320, 240, width, height));
        native.Render(stage, [], camera);
        expanded.Render(stage, [], camera);
        Assert.True(native.TryProject(new(1, 1, -10), out Vector2 original));
        Assert.True(expanded.TryProject(new(1, 1, -10), out Vector2 actual));
        Assert.InRange(Vector2.Distance(original + new Vector2(expanded.ProjectionBounds.X, expanded.ProjectionBounds.Y), actual), 0, 0.0001f);
        Assert.True(native.TryProject(new(-1, -1, -10), out Vector2 originalOpposite));
        Assert.True(expanded.TryProject(new(-1, -1, -10), out Vector2 actualOpposite));
        Assert.InRange(Vector2.Distance(original - originalOpposite, actual - actualOpposite), 0, 0.0001f);
    }

    private static NativeBattlePrimitiveFrame Frame(int x, int y, bool normalViewport, ushort flags)
    {
        int unit = flags == 0 ? 8 : 1;
        uint Point(int px, int py) => unchecked((ushort)(px * unit)) | (uint)unchecked((ushort)(py * unit)) << 16;
        NativeBattleLogicalCommand command = new(new("triangle", 0, 0), 1, 0x20, [0x200000A0, Point(x, y), Point(x + 20, y), Point(x, y + 18)], flags, [], null) { OrderingBucket = 100 };
        NativeBattlePrimitiveDrawMode mode = new(0);
        if (normalViewport)
        {
            mode = mode with { ViewportInputs = new(320, 216, 0, 0), NormalViewport = new(1, 1, 0.125f, 0.125f, 0, -0.5f, -0.5f) };
        }
        return new(NativeBattleLogicalOrdering.Clear(), [new(command, mode, 1)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
    }

    private sealed class CaptureBackend : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C)> Triangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Triangles.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles.Add((a, b, c));
        }

        public void End()
        {
        }
    }
}
