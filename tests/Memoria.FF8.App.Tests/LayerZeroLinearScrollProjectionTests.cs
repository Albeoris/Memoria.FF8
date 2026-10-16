using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LayerZeroLinearScrollProjectionTests
{
    [Fact]
    public void AuthenticTowerRoadPrefixUsesItsActualDirectStartAndRestoresTheLinearCamera()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.LayerZeroLinearScrollProjectionTests.1");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Authentic tower-road snapshot and installed original resources are required.");
        GameState current = GameSession.Load(source);
        using GameWorld world = new(new GameSession(current), directory);
        world.EnsureField();
        Assert.Equal(332, world.Field!.Id);
        BackgroundScrollPosition start = world.Events.Scene.BackgroundScrollPositions[0];
        Assert.Equal(((short)0, (short)-112), (start.X, start.Y));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        BackgroundScrollPosition phase = world.Events.Scene.BackgroundScrollPositions[0];
        Assert.Equal((byte)4, phase.Mode);
        Assert.Equal(1, phase.NativeLinearVersion);
        Assert.Equal((short)210, phase.Duration);
        Assert.Equal((short)1, phase.Counter);
        Assert.Equal((start.X, start.Y), (phase.PreviousX, phase.PreviousY));
        Assert.Equal((start.X, start.Y), (phase.X, phase.Y));
        Assert.Equal(((short)0, (short)126), (phase.TargetX, phase.TargetY));
        Assert.True(DirectBackgroundScrollCertificate.SupportsLinearPhase(world.Field, world.Events.Scene));
        Assert.Equal(new Vector2(0, 112), DirectBackgroundScrollCertificate.Prepare(world.Field, world.Events.Scene)!.ActorOffset);
        JsonElement entry = world.Events.Capture();
        foreach ((string name, int value) in new[] { ("Y", -111), ("NativeLinearVersion", 0), ("Counter", 0), ("Duration", -1) })
        {
            JsonNode corrupt = JsonNode.Parse(entry.GetRawText())!;
            corrupt["Scene"]!["BackgroundScrollPositions"]!["0"]![name] = value;
            Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(corrupt)));
            Assert.True(JsonElement.DeepEquals(entry, world.Events.Capture()));
        }
        using GameWorld restored = new(new GameSession(current with { EventState = entry }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        for (short callback = 1; callback <= 3; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            Assert.Equal(NativeLinearScrollPhase.Interpolate(-112, 126, 210, callback), world.Events.Scene.BackgroundScrollPositions[0].Y);
        }
    }
}
