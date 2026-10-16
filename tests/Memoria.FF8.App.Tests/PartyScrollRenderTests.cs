using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PartyScrollRenderTests
{
    [Fact]
    public void AuthenticWithdrawalPrefixRestoresSavedPartyCosineStatePixelsAndNextWait()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.PartyScrollRenderTests.1");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Authentic withdrawal snapshot and installed field corpus are required.");
        GameState current = GameSession.Load(source);
        GameSession session = new(current);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(322, world.Field!.Id);
        BackgroundScrollPosition start = world.Events.Scene.BackgroundScrollPositions[0];
        Assert.Equal(((short)0, (short)-220), (start.X, start.Y));
        Assert.True(BackgroundScrollCertificate.CanSetPartyPosition(world.Field, world.Events.Scene, world.Events.Actors, 0, 0, 10));
        int[]? operands = null;
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == "CSCROLLP")
            {
                operands = command.Arguments.ToArray();
            }
        };
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.NotNull(operands);
        Assert.Equal([0, 10], operands);
        NativeActorBackgroundScroll curve = Assert.IsType<NativeActorBackgroundScroll>(world.Events.Scene.ActorBackgroundScroll);
        Assert.Equal((byte)2, curve.Mode);
        Assert.Equal(0, curve.PartySlot);
        Assert.Equal(world.Events.Scene.PartyBindingMemory!.First, curve.NativeActorId);
        Assert.Equal(curve.ObjectId, world.Events.Scene.NativeActorObjectIds[curve.NativeActorId]);
        Assert.Equal((short)10, curve.Duration);
        Assert.Equal((short)1, curve.Counter);
        Assert.Equal((byte)1, curve.Status);
        Assert.Equal((start.X, start.Y), (curve.X, curve.Y));
        Assert.Equal((start.X, start.Y), (curve.PreviousX, curve.PreviousY));
        Assert.NotEqual(((short)0, (short)0), (curve.ProjectedTargetX!.Value, curve.ProjectedTargetY!.Value));
        Assert.Equal((curve.ProjectedTargetX.Value, curve.ProjectedTargetY.Value), ActorBackgroundScrollCertificate.Resolve(world.Field, world.Events.Scene, world.Events.Actors, curve.NativeActorId)!.Value);
        Assert.Equal(new Vector2(0, 220), ActorBackgroundScrollCertificate.Prepare(world.Field, world.Events.Scene)!.ActorOffset);

        JsonElement entry = world.Events.Capture();
        foreach ((string name, int value) in new[] { ("PartySlot", 3), ("ProjectedTargetY", curve.ProjectedTargetY.Value + 1), ("Counter", 2) })
        {
            JsonNode corrupt = JsonNode.Parse(entry.GetRawText())!;
            corrupt["Scene"]!["ActorBackgroundScroll"]![name] = value;
            Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(corrupt)));
            Assert.True(JsonElement.DeepEquals(entry, world.Events.Capture()));
        }
        GameState saved = session.State with { EventState = entry };
        GameSession resumedSession = new(saved);
        using GameWorld restored = new(resumedSession, directory);
        restored.EnsureField();
        AssertStateAndPixels();
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Null(world.Events.LastFault);
        AssertStateAndPixels();
        NativeActorBackgroundScroll next = world.Events.Scene.ActorBackgroundScroll!;
        Assert.True(NativeActorScrollPhase.IsValid(next));
        Assert.Equal((next.ProjectedTargetX!.Value, next.ProjectedTargetY!.Value), ActorBackgroundScrollCertificate.Resolve(world.Field, world.Events.Scene, world.Events.Actors, next.NativeActorId)!.Value);

        void AssertStateAndPixels()
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State with { EventState = world.Events.Capture() }), JsonSerializer.SerializeToElement(resumedSession.State with { EventState = restored.Events.Capture() })));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
    }
}
