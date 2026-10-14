using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePendingDirectScrollTests
{
    [Fact]
    public void AuthenticTrainInteriorIssuesTwoDirectRowsBeforePostVmAndRestoresTheirNextCallback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_PENDING_DIRECT_SOURCE");
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(source is not null && File.Exists(source) && Directory.Exists(directory), "Select the genuinely recreated pre-DSCROLL field265/frame2 save.");
        GameState initial = GameSession.Load(source!);
        Assert.Equal(265, initial.FieldId);
        Assert.Equal(2, initial.EventState!.Value.GetProperty("Frame").GetInt32());
        GameSession session = new(initial);
        using GameWorld world = new(session, directory, persistReplayJournal: false);
        using GameWorld independent = new(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(initial))!), directory, persistReplayJournal: false);
        world.EnsureField();
        independent.EnsureField();
        world.SetRenderSize(640, 448);
        independent.SetRenderSize(640, 448);
        int observed = 0;
        world.Events.CommandExecuted += command =>
        {
            if (command.Name != "DSCROLL2")
            {
                return;
            }
            observed++;
            BackgroundScrollPosition pending = world.Events.Scene.BackgroundScrollPositions[1];
            Assert.Equal((byte)0, pending.Status);
            Assert.Equal((short)-250, pending.TargetX);
            Assert.Equal((short)24, pending.TargetY);
            Assert.NotEqual(pending.X, pending.TargetX);
            Assert.True(NativeNonselectedBackgroundScrollCertificate.CanSetDirect(world.Field!, world.Events.Scene, 2, -180, 24));
            SceneEventState savedPhase = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(world.Events.Scene))!;
            Assert.True(NativeNonselectedBackgroundScrollCertificate.SupportsRetainedPosition(world.Field!, savedPhase, 1, savedPhase.BackgroundScrollPositions[1]));
        };
        PlayerAction callback = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(callback), independent.Apply(callback));
        Assert.Equal(2, observed);
        foreach ((int layer, short targetX) in new (int, short)[] { (1, -250), (2, -180) })
        {
            BackgroundScrollPosition completed = world.Events.Scene.BackgroundScrollPositions[layer];
            Assert.Equal((byte)2, completed.Status);
            Assert.Equal(targetX, completed.X);
            Assert.Equal((short)24, completed.Y);
            Assert.Equal((short)0, completed.Counter);
        }
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), independent.Render(TimeSpan.Zero));
        GameState current = session.State with { EventState = world.Events.Capture() };
        using GameWorld resumed = new(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(current))!), directory, persistReplayJournal: false);
        resumed.EnsureField();
        resumed.SetRenderSize(640, 448);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Apply(callback), resumed.Apply(callback));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
    }

    [Fact]
    public void PendingDirectAllowsUnknownUnusedHistoryButNeverDrawsInventedCurrentWords()
    {
        BackgroundScrollPosition pending = new(null, null, -250, 24, null, null, null, 3, 0);
        Assert.True(NativeNonselectedBackgroundScrollCertificate.ValidPosition(pending));
        Assert.False(BackgroundParallaxPlacement.TryOffset(0, 0, new BackgroundScrollSettings(0, 0, 256, 256), pending, out _));
        Assert.False(NativeNonselectedBackgroundScrollCertificate.ValidPosition(pending with { Status = 1 }));
        Assert.False(NativeNonselectedBackgroundScrollCertificate.ValidPosition(pending with { TargetX = short.MaxValue }));
        Assert.False(NativeNonselectedBackgroundScrollCertificate.ValidPosition(pending with { X = 0 }));
        Assert.False(NativeNonselectedBackgroundScrollCertificate.ValidPosition(pending with { Status = 2 }));
        BackgroundScrollPosition known = pending with { X = 7, Y = -9, PreviousX = 300, PreviousY = -200, Counter = 50 };
        Assert.True(NativeNonselectedBackgroundScrollCertificate.ValidPosition(known));
        Assert.True(BackgroundParallaxPlacement.TryOffset(0, 0, new BackgroundScrollSettings(0, 0, 256, 256), known, out Vector2 offset));
        Assert.Equal(new Vector2(-7, 9), offset);
    }
}
