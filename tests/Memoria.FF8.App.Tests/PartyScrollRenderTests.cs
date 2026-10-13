using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PartyScrollRenderTests
{
    [Fact]
    public void ReachedPartyCameraUsesOriginalActorOrderAndCertifiedCosinePhases()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        BackgroundScrollPosition? pending = null;
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == "CSCROLLP")
            {
                pending = world.Events.Scene.BackgroundScrollPositions[0];
            }
        };
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(8, world.Events.Frame);
        Assert.NotNull(pending);
        Assert.Equal((byte)2, pending.Mode);
        Assert.Equal((short)15, pending.Duration);
        Assert.Equal((byte)0, pending.NativeActorId);
        BackgroundScrollPosition final = world.Events.Scene.BackgroundScrollPositions[0];
        Assert.Equal((byte)3, final.Mode);
        Assert.Equal((byte)2, final.Status);
        Assert.Equal((short)224, final.Y);
        Assert.Equal((short)15, final.Duration);
        Assert.Equal((byte)0, final.NativeActorId);
        byte[] originalPixels = world.Render(TimeSpan.Zero).ToArray();
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(originalPixels, resumed.Render(TimeSpan.Zero));
        Assert.False(BackgroundScrollCertificate.CanSetPartyPosition(world.Field!, world.Events.Scene, 0, 0, 14));
        Assert.False(BackgroundScrollCertificate.CanSetPartyPosition(world.Field!, world.Events.Scene, 1, 1, 15));
        // Isolated renderer-phase checks, not additional player progression.
        for (short elapsed = 0; elapsed <= 15; elapsed++)
        {
            short y = (short)NativeCosineInterpolation.Interpolate(224, 0, 15, elapsed);
            world.Events.Scene.BackgroundScrollPositions[0] = final with { Mode = 2, Status = elapsed == 15 ? (byte)2 : (byte)1, Counter = elapsed == 15 ? elapsed : (short)(elapsed + 1), PreviousY = 224, Y = y };
            BackgroundScrollFrame frame = BackgroundScrollCertificate.Prepare(world.Field!, world.Events.Scene);
            Assert.Equal(new Vector2(0, -y), frame.ActorOffset);
            Assert.Equal(Vector2.Zero, frame.LayerOffsets.GetValueOrDefault(1));
            _ = world.Render(TimeSpan.Zero);
        }
        world.Events.Scene.BackgroundScrollPositions[0] = final with { Mode = 2, Status = 1, Counter = 4, PreviousY = 224, Y = 999 };
        Assert.False(BackgroundScrollCertificate.Supports(world.Field!, world.Events.Scene));
        world.Events.Scene.BackgroundScrollPositions[0] = final with { Mode = 2, Status = 1, Counter = 4, PreviousY = 224, Y = 123 };
        Assert.False(BackgroundScrollCertificate.Supports(world.Field!, world.Events.Scene));
    }
}
