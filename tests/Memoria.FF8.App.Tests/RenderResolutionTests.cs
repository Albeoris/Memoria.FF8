using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RenderResolutionTests
{
    [Fact]
    public void Native_viewport_raster_preserves_projection_and_does_not_use_SETGETA_as_elevation()
    {
        Assert.Equal(new PixelBounds(0, 0, 960, 720), ViewportLayout.WorldRenderTarget(1280, 720));
        Assert.Equal(new PixelBounds(0, 0, 2048, 1536), ViewportLayout.WorldRenderTarget(3840, 2160));
        Assert.Equal(new PixelBounds(0, 0, 2880, 2160), ViewportLayout.WorldRenderTarget(3840, 2160, 4096));
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? root = new(TestResources.FixtureDirectory);
        var session = new GameSession(GameSession.Load(Path.Combine(root!.FullName, "../fixtures/external-fixture.json")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        ActorState quistis = Assert.Single(world.Events.Actors, actor => actor.ObjectId == 11);
        Assert.Equal(242, quistis.GetaParameter);
        string original = world.Events.Capture().GetRawText();
        world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Background, Visible: false));
        foreach (ActorState actor in world.Events.Actors.Where(actor => actor.ObjectId != quistis.ObjectId))
        {
            world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Actor, ActorId: actor.ObjectId, Visible: false));
        }
        var native = new ActorVisual(quistis.ObjectId, quistis.ModelId, quistis.X + quistis.DisplayOffset.X, quistis.Y + quistis.DisplayOffset.Y, quistis.Z + quistis.DisplayOffset.Z, quistis.Direction, quistis.AnimationId, quistis.AnimationFrame, quistis.Active && quistis.Visible, FirstControlRotation: quistis.FirstControlRotation, PolygonColor: quistis.PolygonColor);
        var reference = new FieldRenderer(world.Field!);
        byte[] low = world.Render(TimeSpan.Zero).ToArray();
        Assert.Equal(reference.Render(world.Events.BackgroundStates, [native], TimeSpan.Zero, cameraIndex: 1, backgroundVisible: false), low);
        Assert.True(world.SetRenderSize(960, 720));
        byte[] high = world.Render(TimeSpan.Zero).ToArray();
        Assert.Equal(960 * 720 * 4, high.Length);
        Assert.False(world.SetRenderSize(960, 720));
        Assert.Same(world.Render(TimeSpan.Zero), world.Render(TimeSpan.Zero));
        PixelBounds lowBounds = VisibleBounds(low, 320, 240);
        PixelBounds highBounds = VisibleBounds(high, 960, 720);
        Assert.InRange(Math.Abs(highBounds.X / 3f - lowBounds.X), 0, 1);
        Assert.InRange(Math.Abs(highBounds.Y / 3f - lowBounds.Y), 0, 1);
        Assert.InRange(Math.Abs(highBounds.Width / 3f - lowBounds.Width), 0, 2);
        Assert.InRange(Math.Abs(highBounds.Height / 3f - lowBounds.Height), 0, 2);
        Assert.True(HasSubpixelDetail(high, 960, 720), "The model must be rasterized at the selected size, not expanded as3x3 source pixels.");
        Assert.True(world.SetRenderSize(320, 240));
        Assert.Equal(low, world.Render(TimeSpan.Zero));
        Assert.Equal(original, world.Events.Capture().GetRawText());
    }

    private static PixelBounds VisibleBounds(byte[] pixels, int width, int height)
    {
        List<(int X, int Y)> visible = [];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = (y * width + x) * 4;
                if (pixels[offset] != 0 || pixels[offset + 1] != 0 || pixels[offset + 2] != 0)
                {
                    visible.Add((x, y));
                }
            }
        }
        Assert.NotEmpty(visible);
        int left = visible.Min(point => point.X);
        int top = visible.Min(point => point.Y);
        return new PixelBounds(left, top, visible.Max(point => point.X) - left + 1, visible.Max(point => point.Y) - top + 1);
    }

    private static bool HasSubpixelDetail(byte[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y += 3)
        {
            for (int x = 0; x < width; x += 3)
            {
                int first = (y * width + x) * 4;
                for (int dy = 0; dy < 3; dy++)
                {
                    for (int dx = 0; dx < 3; dx++)
                    {
                        int next = ((y + dy) * width + x + dx) * 4;
                        if (!pixels.AsSpan(first, 3).SequenceEqual(pixels.AsSpan(next, 3)))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }
}
