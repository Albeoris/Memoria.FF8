using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveSessionCaptureTests
{
    [Fact]
    public void Current_field_capture_retains_logical_continuation_and_drops_historical_menu_locations()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveFieldPartyResumeTests.ThreePartyDocument(assets), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
        GameState before = session.State;
        GameplaySaveComponentDocument document = world.CaptureGameplaySave();
        Assert.Same(before, session.State);
        Assert.NotNull(document.Progression!.FieldContinuation);
        Assert.Null(document.Progression.FieldLocation);
        Assert.Equal(document.Progression.FieldContinuation.GetLeaderPosition(), document.Resume.Field);
        GameState restored = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets);
        GameSession selected = new(restored);
        using GameWorld candidate = new(selected, directory, sharedAssets: assets);
        candidate.EnsureField();
        Assert.Equal(world.Events.Frame, candidate.Events.Frame);
        Assert.Equal(world.Events.CaptureGameplaySaveFieldParty().PartyPositions, candidate.Events.CaptureGameplaySaveFieldParty().PartyPositions);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
        Assert.True(candidate.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
        Assert.Equal(world.Events.Frame, candidate.Events.Frame);
        Assert.Equal(session.State.Economy!.Gil, selected.State.Economy!.Gil);
    }

    [Fact]
    public void Live_world_capture_uses_current_position_and_globals_and_cold_reloads_without_resource_buffers()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, true);
        GameSession session = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        WorldState current = session.State.World!;
        session.SetWorldState(current with { X = current.X + 100, Z = current.Z + 100, BodyYaw = 3072 });
        world.Events.Scene.Names[0] = "Current";
        GameState before = session.State;
        GameplaySaveComponentDocument captured = world.CaptureGameplaySave();
        Assert.Same(before, session.State);
        Assert.Equal(current.X + 100, captured.Resume.World!.X);
        Assert.Equal(current.Z + 100, captured.Resume.World.Z);
        Assert.Equal((ushort)3072, captured.Resume.World.BodyYaw);
        Assert.Equal("Current", captured.Progression!.SceneFacts!.Names[0]);
        Assert.Equal(initial.Progression!.Clocks.Playtime, captured.Progression.Clocks.Playtime);
        string serialized = JsonSerializer.Serialize(captured);
        Assert.DoesNotContain("NativeBattleMemory", serialized);
        Assert.DoesNotContain("NativeFootRuntime", serialized);
        Assert.DoesNotContain("NativeFieldRegistration", serialized);
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-live-save-" + Guid.NewGuid().ToString("N"));
        GameState restored;
        try
        {
            restored = world.WriteGameplaySave(path);
            Assert.True(File.Exists(path));
            Assert.Same(before, session.State);
            GameState selected = PlayerStateLoader.Load(path, directory, sharedAssets: assets);
            Assert.Equal(restored.World!.X, selected.World!.X);
            Assert.Equal(restored.World.Z, selected.World.Z);
        }
        finally
        {
            File.Delete(path);
        }
        Assert.Equal(captured.Resume.World.X, restored.World!.X);
        Assert.Equal(captured.Resume.World.Z, restored.World.Z);
        Assert.Equal(3072, restored.World.BodyYaw);
        Assert.Equal(captured.Economy.Gil, restored.Economy!.Gil);
        Assert.Equal("Current", restored.WorldEventState!.Value.GetProperty("Scene").GetProperty("Names").GetProperty("0").GetString());
    }

    [Fact]
    public void Scripted_world_boundary_fails_without_advancing_the_live_session()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, true), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        session.SetWorldState(session.State.World! with { EventUi = NativeWorldEventUi.Create() with { ScriptMode = 1 } });
        GameState before = session.State;
        Assert.Throws<NotSupportedException>(() => world.CaptureGameplaySave());
        Assert.Same(before, session.State);
    }

    [Fact]
    public void Rejected_host_continuation_keeps_the_existing_save_and_live_state()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, true), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        GameState before = session.State;
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-live-save-" + Guid.NewGuid().ToString("N"));
        byte[] original = [1, 2, 3, 4];
        try
        {
            File.WriteAllBytes(path, original);
            int calls = 0;
            Assert.Throws<InvalidDataException>(() => world.WriteGameplaySave(path, candidate =>
            {
                calls++;
                Assert.NotNull(candidate.World!.NativeFootRuntime);
                throw new InvalidDataException("Rejected continuation.");
            }));
            Assert.Equal(1, calls);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Same(before, session.State);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
