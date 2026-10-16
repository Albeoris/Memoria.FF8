using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class CardMarkerRenderingTests
{
    private static readonly Rgba32 FrontLeft = new(240, 20, 20);
    private static readonly Rgba32 FrontRight = new(20, 240, 20);
    private static readonly Rgba32 BackLeft = new(20, 20, 240);
    private static readonly Rgba32 BackRight = new(240, 240, 20);
    private static readonly ActorVisual Actor = new(7, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128));
    private static readonly CardMarkerVisual Marker = new(7, Split(FrontLeft, FrontRight), Split(BackLeft, BackRight));

    [Theory]
    [InlineData(0, false)]
    [InlineData(4, true)]
    public void RotationShowsReadableFrontAndBackWithoutMirroring(double seconds, bool back)
    {
        FieldRenderer renderer = new(Field(), modelTextureSampling: ModelTextureSampling.Nearest);
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.Zero).ToArray();
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.FromSeconds(seconds), cardMarkers: [Marker]).ToArray();
        var bounds = ChangedBounds(baseline, pixels, renderer.OutputWidth);
        int middleY = (bounds.Top + bounds.Bottom) / 2;
        Assert.Equal(back ? BackLeft : FrontLeft, Sample(pixels, renderer.OutputWidth, bounds.Left + 1, middleY));
        Assert.Equal(back ? BackRight : FrontRight, Sample(pixels, renderer.OutputWidth, bounds.Right - 1, middleY));
        Assert.Equal(pixels, renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.FromSeconds(seconds + 8), cardMarkers: [Marker]));
    }

    [Fact]
    public void EdgeOnCardDisappearsAtAQuarterTurn()
    {
        FieldRenderer renderer = new(Field());
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.Zero).ToArray();
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.FromSeconds(2), cardMarkers: [Marker]));
    }

    [Theory]
    [InlineData(320, 240, 1)]
    [InlineData(960, 720, 3)]
    public void MarkerStaysSmallAndPreservesCardAspectAboveTheActor(int width, int height, int scale)
    {
        FieldRenderer renderer = new(Field(), width, height);
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.Zero).ToArray();
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.Zero, cardMarkers: [Marker]);
        var bounds = ChangedBounds(baseline, pixels, width);
        int markerWidth = bounds.Right - bounds.Left + 1;
        int markerHeight = bounds.Bottom - bounds.Top + 1;
        Assert.Equal(14 * scale, markerHeight);
        Assert.Equal(10 * scale, markerWidth);
        Assert.Equal(Marker.Front.Width / (double)Marker.Front.Height, markerWidth / (double)markerHeight, 6);
        Assert.Equal(width / 2.0, (bounds.Left + bounds.Right + 1) / 2.0);
        int actorTop = Enumerable.Range(0, baseline.Length / 4).Where(index => baseline[index * 4 + 2] != 0).Min(index => index / width);
        Assert.InRange(actorTop - bounds.Bottom - 1, 3 * scale, 5 * scale);
    }

    [Theory]
    [InlineData(false, 7, 0)]
    [InlineData(true, 8, 0)]
    [InlineData(true, 7, 1)]
    public void HiddenMissingOrUnrenderableActorsDoNotReceiveMarkers(bool visible, int objectId, int modelId)
    {
        FieldRenderer renderer = new(Field());
        ActorVisual actor = Actor with { Visible = visible, ObjectId = objectId, ModelId = modelId };
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, cardMarkers: [Marker]));
        Assert.Equal(renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray(), renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, cardMarkers: [Marker]));
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(40, false)]
    public void ForegroundOccludesTheMarkerWhileBackgroundRemainsBehindIt(int tileDepth, bool occluded)
    {
        PixelImage texture = Solid(new Rgba32(90, 90, 90), 16, 16);
        BackgroundTile[] tiles = new[] { -16, 0 }.SelectMany(x => new[] { -32, -16 }.Select(y => new BackgroundTile(x, y, tileDepth, 255, 0, 0, MapBlendMode.None, false, true, texture))).ToArray();
        FieldRenderer renderer = new(Field() with { Background = tiles });
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.Zero).ToArray();
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.Zero, cardMarkers: [Marker]);
        Assert.Equal(occluded, baseline.SequenceEqual(pixels));
    }

    [Fact]
    public void LeaningActorsAnchorTheMarkerToTheProjectedHeadPositionAndDepth()
    {
        FieldAssets field = Field();
        FieldModel model = field.Models[0] with { Vertices = [new(new Vector3(20, -20, 20), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 10, 10), 0)] };
        PixelImage texture = Solid(new Rgba32(90, 90, 90), 16, 16);
        BackgroundTile[] tiles = new[] { -48, -32 }.Select(y => new BackgroundTile(16, y, 22, 255, 0, 0, MapBlendMode.None, false, true, texture)).ToArray();
        FieldRenderer renderer = new(field with { Models = [model], Background = tiles });
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.Zero).ToArray();
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [Actor], TimeSpan.Zero, cardMarkers: [Marker]);
        var bounds = ChangedBounds(baseline, pixels, renderer.OutputWidth);
        Assert.Equal(185, (bounds.Left + bounds.Right + 1) / 2.0);
        Assert.Equal(14, bounds.Bottom - bounds.Top + 1);
        Assert.Equal(10, bounds.Right - bounds.Left + 1);
        Assert.InRange(120 - 25 * 240 / 224.0 - bounds.Bottom - 1, 3, 5);
    }

    private static FieldAssets Field()
    {
        PixelImage texture = Solid(new Rgba32(0, 0, 200), 16, 16);
        FieldModel model = new("triangle", [new(-1, 0)], [new(new Vector3(0, -10, 0), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 10, 10), 0)], [new([0, 2, 1], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        return new FieldAssets(0, "card-markers", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [model]);
    }

    private static PixelImage Split(Rgba32 left, Rgba32 right)
    {
        PixelImage image = Solid(left, 40, 56);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = image.Width / 2; x < image.Width; x++)
            {
                image[x, y] = right;
            }
        }
        return image;
    }

    private static PixelImage Solid(Rgba32 color, int width, int height)
    {
        return new PixelImage(width, height, Enumerable.Repeat(color, width * height).ToArray());
    }

    private static (int Left, int Top, int Right, int Bottom) ChangedBounds(byte[] baseline, byte[] pixels, int width)
    {
        int[] changed = Enumerable.Range(0, pixels.Length / 4).Where(index => !pixels.AsSpan(index * 4, 4).SequenceEqual(baseline.AsSpan(index * 4, 4))).ToArray();
        Assert.NotEmpty(changed);
        return (changed.Min(index => index % width), changed.Min(index => index / width), changed.Max(index => index % width), changed.Max(index => index / width));
    }

    private static Rgba32 Sample(byte[] pixels, int width, int x, int y)
    {
        int offset = (y * width + x) * 4;
        return new Rgba32(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
    }
}
