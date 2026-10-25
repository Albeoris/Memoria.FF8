using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldRenderInterpolationTests
{
    [Theory]
    [InlineData(320, 240)]
    [InlineData(641, 480)]
    [InlineData(960, 480)]
    public void WrappedParallaxAndCameraBlendMatchFractionalPresentation(int width, int height)
    {
        FieldAssets field = CreateField(-193);
        FieldBackgroundSample previous = Sample(field, 0, 0);
        FieldBackgroundSample current = Sample(field, 1, 2);
        FieldRenderer renderer = new(field, width, height);
        byte[] midpoint = Render(renderer, current, new(previous, current, 0.5f)).ToArray();
        FieldAssets relocated = field with { Background = [field.Background[0] with { X = 158 }, field.Background[0] with { X = -194 }] };
        byte[] expected = new FieldRenderer(relocated, width, height).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [1] = new(-0.5f, 0) });
        Assert.Equal(expected, midpoint);
        Assert.NotEqual(Render(renderer, previous), midpoint);
        Assert.NotEqual(Render(renderer, current), midpoint);
        Assert.Equal(Render(new FieldRenderer(field, width, height), previous), Render(renderer, current, new(previous, current, 0)));
        Assert.Equal(Render(new FieldRenderer(field, width, height), current), Render(renderer, current, new(previous, current, 1)));
        Assert.Equal(Render(new FieldRenderer(field, width, height), current), Render(renderer, current));
    }

    [Fact]
    public void WrappedCurrentPhaseBoundaryDoesNotSweepAcrossTheImage()
    {
        FieldAssets field = CreateField(100);
        FieldBackgroundSample previous = Sample(field, 0, 351);
        FieldBackgroundSample current = Sample(field, 0, 0);
        FieldRenderer renderer = new(field, 640, 480);
        byte[] actual = Render(renderer, current, new(previous, current, 0.5f));
        FieldAssets reference = field with { Background = [field.Background[0] with { X = 100 }] };
        byte[] expected = new FieldRenderer(reference, 640, 480).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [1] = new(0.5f, 0) });
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(960, 480)]
    public void FractionalCameraAndShakeMoveAllUnwrappedLayersTogether(int width, int height)
    {
        FieldAssets field = CreateField(0) with { Background = [CreateTile(0, 0), CreateTile(20, 1)] };
        FieldBackgroundSample previous = new(new Dictionary<int, Vector2> { [0] = Vector2.Zero, [1] = new(2, 0) }, Vector2.Zero, null);
        FieldBackgroundSample current = new(new Dictionary<int, Vector2> { [0] = new(-1, 0), [1] = new(0, 0) }, new(-1, 0), null);
        FieldRenderer renderer = new(field, width, height);
        byte[] actual = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: current.LayerOffsets, actorScrollOffset: current.ActorOffset, drawingJitterX: 1, backgroundBlend: new(previous, current, 0.5f));
        byte[] expected = new FieldRenderer(field, width, height).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [0] = new(0.5f, 0), [1] = new(2, 0) });
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BothNativeEndpointsAreValidatedBeforePresentation()
    {
        FieldAssets field = CreateField(0);
        FieldBackgroundSample previous = Sample(field, 0, 0);
        FieldBackgroundSample current = Sample(field, 1, 2);
        FieldBackgroundSample invalid = previous with { NativeWrap = previous.NativeWrap! with { Left = -193 } };
        Assert.Throws<NotSupportedException>(() => Render(new FieldRenderer(field), current, new(invalid, current, 0.5f)));
        Assert.Throws<NotSupportedException>(() => Render(new FieldRenderer(field), current, new(current, invalid, 0.5f)));
    }

    [Theory]
    [InlineData(0.25f, 160, 171)]
    [InlineData(0.5f, 160, 172)]
    [InlineData(0.75f, 161, 172)]
    public void FractionalShadowCenterUsesNativeRoundingBeforeCameraMovement(float offset, float centerX, float diagonalX)
    {
        FieldModel model = new("shadow", [new(-1, 0)], [], [], [], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = CreateField(0) with { Background = [], Models = [model] };
        CaptureRenderer capture = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = capture };
        ActorVisual actor = new(0, 0, 0, 0, 100, 0, 0, 0, true, GroundShadowRadius601To608: 2, GroundShadowLevel609: 128, GroundShadowCentre: new(0, 0, 100));
        actor = actor with { GroundShadowCentre = new(offset, 0, 100), AnimationBlend = new(0, 0, 1) };
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
        Assert.Equal(24, capture.Vertices.Count);
        // Native projection rounds camera-space rows before applying the fractional DRAWENV pan.
        Assert.Equal(centerX, capture.Vertices[0].Position.X);
        Assert.Equal(diagonalX, capture.Vertices[2].Position.X);
        TriangleVertex[] expected = capture.Vertices.Select(vertex => vertex with { Position = vertex.Position - new Vector2(offset, 0) }).ToArray();
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, actorScrollOffset: new(-offset, 0));
        Assert.Equal(expected, capture.Vertices);
    }

    [Fact]
    public void CardMarkerTracksTheSmoothSkeletalHead()
    {
        FieldModel model = new("marker", [new(-1, 0)], [new(new Vector3(0, -20, 0), 0)], [], [], [new([new(Vector3.Zero, [Vector3.Zero]), new(new Vector3(0, 10, 0), [Vector3.Zero])])]);
        FieldAssets field = CreateField(0) with { Background = [], Models = [model] };
        FieldModel referenceModel = model with { Animations = [new([new(new Vector3(0, 5, 0), [Vector3.Zero])])] };
        CaptureRenderer capture = new();
        ActorVisual actor = new(0, 0, 0, 0, 100, 192, 0, 0, true);
        PixelImage markerImage = new(10, 14);
        CardMarkerVisual[] marker = [new(0, markerImage, markerImage)];
        new FieldRenderer(field with { Models = [referenceModel] }) { TriangleRenderer = capture }.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, cardMarkers: marker);
        TriangleVertex[] expected = capture.Vertices.ToArray();
        Assert.Equal(6, expected.Length);
        new FieldRenderer(field) { TriangleRenderer = capture }.Render(new Dictionary<int, int>(), [actor with { AnimationBlend = new(0.5f, 0.5f, 1) }], TimeSpan.Zero, cardMarkers: marker);
        Assert.Equal(expected, capture.Vertices);
    }

    private static byte[] Render(FieldRenderer renderer, FieldBackgroundSample sample, FieldBackgroundBlend? blend = null)
    {
        return renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: sample.LayerOffsets, actorScrollOffset: sample.ActorOffset, nativeWrapFrame: sample.NativeWrap, backgroundBlend: blend);
    }

    private static FieldBackgroundSample Sample(FieldAssets field, int cameraX, int phase)
    {
        ImmutableArray<FieldNativeBackgroundLayerCurrent> currents = ImmutableArray.Create(new FieldNativeBackgroundLayerCurrent(1, (short)phase, 0, 0, 0, 256, 256));
        FieldNativeBackgroundWrapFrame frame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new(cameraX, 0), currents)!;
        Assert.NotNull(frame);
        Vector2 scroll = new(-cameraX, 0);
        return new(new Dictionary<int, Vector2> { [1] = scroll }, scroll, frame);
    }

    private static FieldAssets CreateField(int tileX)
    {
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100);
        FieldBounds[] bounds = Enumerable.Repeat(new FieldBounds(-512, 512, 512, -512), 8).ToArray();
        bounds[1] = new(-128, 128, 176, -176);
        return new(600, "presentation", [], [], [CreateTile(tileX, 1)], camera, new FieldWalkmesh([]), [])
        {
            NativeExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash,
            Information = new FieldInformation(0, 0, bounds, [new(0, 224, 320, 0)], [], []) { BackgroundWrapMask = 2 }
        };
    }

    private static BackgroundTile CreateTile(int x, int layer)
    {
        PixelImage image = new(16, 16);
        Array.Fill(image.Pixels, new Rgba32(100, 150, 200));
        return new(x, 0, 100, 255, 0, layer, MapBlendMode.None, false, true, image);
    }

    private sealed class CaptureRenderer : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Vertices.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }
    }
}
