using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldPresentationTests
{
    [Theory]
    [InlineData(30, false, 61)]
    [InlineData(60, false, 61)]
    [InlineData(30, true, 183)]
    [InlineData(60, true, 183)]
    public void Display_rate_does_not_change_native_callback_count_and_F1_scales_only_simulation(int displayFps, bool accelerated, int expectedCallbacks)
    {
        WorldSimulationClock simulation = new();
        GameplayClock gameplay = new();
        if (accelerated)
        {
            gameplay.ToggleSpeed();
        }
        int displays = displayFps * 2;
        int callbacks = 0;
        for (int frame = 0; frame < displays; frame++)
        {
            long start = TimeSpan.TicksPerSecond * 2 * frame / displays;
            long end = TimeSpan.TicksPerSecond * 2 * (frame + 1) / displays;
            simulation.Accumulate(gameplay.Advance(TimeSpan.FromTicks(end - start), SceneKind.WorldMap));
            while (simulation.TryStep())
            {
                callbacks++;
            }
            Assert.InRange(simulation.Fraction, 0, 1);
        }
        Assert.Equal(expectedCallbacks, callbacks);
    }

    [Fact]
    public void Extra_display_samples_produce_intermediate_positions_without_advancing_native_state()
    {
        WorldPresentation presentation = new();
        WorldState before = new() { Frame = 7, X = 100 };
        WorldState after = before with { Frame = 8, X = 120 };
        string saved = JsonSerializer.Serialize(after);
        presentation.Sample(before, 1, Prepare);
        presentation.Commit(before, after, Prepare);
        Assert.Equal(105, presentation.Sample(after, 0.25f, Prepare).Position.X);
        Assert.Equal(115, presentation.Sample(after, 0.75f, Prepare).Position.X);
        Assert.Equal(saved, JsonSerializer.Serialize(after));
        Assert.Equal(120, presentation.Sample(after, 1, Prepare).Position.X);
    }

    [Fact]
    public void Last_two_native_callbacks_are_used_when_acceleration_skips_display_samples()
    {
        WorldPresentation presentation = new();
        WorldState first = new() { Frame = 1, X = 10 };
        presentation.Sample(first, 1, Prepare);
        WorldState penultimate = first with { Frame = 3, X = 30 };
        WorldState last = first with { Frame = 4, X = 40 };
        WorldState second = first with { Frame = 2, X = 20 };
        presentation.Commit(first, second, Prepare);
        presentation.Commit(second, penultimate, Prepare);
        presentation.Commit(penultimate, last, Prepare);
        Assert.Equal(35, presentation.Sample(last, 0.5f, Prepare).Position.X);
    }

    [Fact]
    public void Restore_teleport_and_nonconsecutive_callbacks_start_without_old_visual_history()
    {
        WorldPresentation presentation = new();
        WorldState before = new() { Frame = 4, X = 10 };
        WorldState after = before with { Frame = 5, X = 20 };
        presentation.Sample(before, 1, Prepare);
        presentation.Commit(before, after, Prepare);
        WorldState teleported = after with { X = 5000 };
        Assert.Equal(5000, presentation.Sample(teleported, 0, Prepare).Position.X);
        WorldState jumped = after with { Frame = 10 };
        presentation.Commit(before, jumped, Prepare);
        Assert.Equal(20, presentation.Sample(jumped, 0.5f, Prepare).Position.X);
        WorldSimulationClock simulation = new();
        simulation.Accumulate(TimeSpan.FromMilliseconds(10));
        Assert.True(simulation.Fraction > 0);
        simulation.Reset();
        Assert.Equal(0, simulation.Fraction);
        Assert.False(simulation.TryStep());
    }

    [Fact]
    public void Visual_samples_are_captured_under_their_committed_process_owner_and_fail_only_when_presented()
    {
        WorldPresentation presentation = new();
        WorldState before = new() { Frame = 0, X = 10 };
        WorldState after = before with { Frame = 1, X = 20 };
        long processFrame = before.Frame;
        WorldRenderFrame PrepareOwned(WorldState state)
        {
            Assert.Equal(processFrame, state.Frame);
            return Prepare(state);
        }
        presentation.Sample(before, 1, PrepareOwned);
        processFrame = after.Frame;
        presentation.Commit(before, after, PrepareOwned);
        processFrame++;
        Assert.Equal(15, presentation.Sample(after, 0.5f, PrepareOwned).Position.X);
        WorldState next = after with { Frame = 2, X = 30 };
        presentation.Commit(after, next, _ => throw new NativeWorldPresentationDeferredException("Awaiting a model draw callback."));
        Assert.Throws<NativeWorldPresentationDeferredException>(() => presentation.Sample(next, 1, Prepare));
        Assert.Equal(30, next.X);
    }

    [Fact]
    public void Installed_player_motion_and_events_match_at_both_display_rates_and_acceleration()
    {
        string directory = TestResources.GameDirectory;
        GameSession seed = GameSession.CreateColdProcess();
        seed.StartNewGame();
        using GameWorld initialization = new(seed, directory);
        seed.Restore(seed.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        using WorldMapScene initial = new(seed, directory);
        initial.Apply(new(PlayerActionKind.Wait, Frames: 24));
        GameState snapshot = seed.State;
        foreach (bool accelerated in new[] { false, true })
        {
            string? expected = null;
            foreach (int displayFps in new[] { 30, 60 })
            {
                GameSession session = new(snapshot);
                using WorldMapScene scene = new(session, directory);
                GameplayClock gameplay = new();
                WorldSimulationClock simulation = new();
                if (accelerated)
                {
                    gameplay.ToggleSpeed();
                }
                int displays = displayFps / 5;
                for (int frame = 0; frame < displays; frame++)
                {
                    long start = TimeSpan.TicksPerSecond / 5 * frame / displays;
                    long end = TimeSpan.TicksPerSecond / 5 * (frame + 1) / displays;
                    simulation.Accumulate(gameplay.Advance(TimeSpan.FromTicks(end - start), SceneKind.WorldMap));
                    while (simulation.TryStep())
                    {
                        Assert.Equal(1, scene.Apply(new(PlayerActionKind.Move, Horizontal: 1)).FramesAdvanced);
                    }
                    scene.Render(80, 56, interpolation: simulation.Fraction);
                }
                Assert.Equal(snapshot.World!.Frame + (accelerated ? 18 : 6), scene.State.Frame);
                Assert.NotEqual((snapshot.World.X, snapshot.World.Z), (scene.State.X, scene.State.Z));
                string actual = JsonSerializer.Serialize(session.State);
                expected ??= actual;
                Assert.Equal(expected, actual);
                GameSession reference = new(snapshot);
                using WorldMapScene native = new(reference, directory);
                native.Apply(new(PlayerActionKind.Move, Horizontal: 1, Frames: accelerated ? 18 : 6));
                Assert.Equal(JsonSerializer.Serialize(reference.State), actual);
            }
        }
    }

    [Fact]
    public void Installed_world_intermediate_images_change_without_modifying_a_standalone_save()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        using WorldMapScene scene = new(session, directory);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 24));
        scene.Render(160, 112);
        Assert.Equal(1, scene.Apply(new(PlayerActionKind.Move, Horizontal: 1)).FramesAdvanced);
        Assert.Equal(1, scene.Apply(new(PlayerActionKind.Move, Horizontal: 1)).FramesAdvanced);
        string state = JsonSerializer.Serialize(session.State);
        byte[] first = scene.Render(160, 112, interpolation: 0).ToArray();
        byte[] middle = scene.Render(160, 112, interpolation: 0.5f).ToArray();
        byte[] last = scene.Render(160, 112).ToArray();
        Assert.NotEqual(first, middle);
        Assert.NotEqual(middle, last);
        Assert.Equal(state, JsonSerializer.Serialize(session.State));
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_PRESENTATION_SNAPSHOT") ?? TestResources.ArtifactFile("world-presentation-component-save.json");
        session.Save(path);
        GameSession restored = new(GameSession.Load(path));
        using WorldMapScene other = new(restored, directory);
        Assert.Equal(last, other.Render(160, 112, interpolation: 0));
        PlayerAction next = new(PlayerActionKind.Move, Horizontal: 1);
        Assert.Equal(scene.Apply(next), other.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
    }

    private static WorldRenderFrame Prepare(WorldState state)
    {
        WorldSky sky = new(Vector3.Zero, new Rgba32(0, 0, 0), new Rgba32(0, 0, 0), new Rgba32(0, 0, 0));
        return new WorldRenderFrame(new(state.X, -state.NativeHeight, state.Z), 0, 0, 0, WorldCamera.Foot(0, 2048), sky, false);
    }
}
