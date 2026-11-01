using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldFieldReturnRenderSizeTests
{
    [Fact]
    public void ReturningToCachedFieldUsesTheRenderSizeSelectedOnTheWorldMap()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_STATE");
        Assert.SkipUnless(File.Exists(source), "An explicitly selected standalone field state is required.");
        GameState original = GameSession.Load(source!);
        Assert.Equal(SceneKind.Field, original.Scene);
        GameSession session = new(original);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        GameState field = session.State;
        //Exercise the retained renderer across a world-size selection without advancing gameplay.
        session.RequestWorld(new(0, 0, -1));
        Assert.True(world.SetRenderSize(640, 448));
        session.Restore(field);
        world.EnsureField();
        byte[] returned = world.Render(TimeSpan.Zero);
        Assert.Equal(640 * 448 * 4, returned.Length);
        GameSession freshSession = new(field);
        using GameWorld fresh = new(freshSession, TestResources.GameDirectory);
        fresh.EnsureField();
        fresh.SetRenderSize(640, 448);
        Assert.Equal(fresh.Render(TimeSpan.Zero), returned);
    }
}
