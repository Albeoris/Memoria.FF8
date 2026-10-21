using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldBattlePublicationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);

    [Fact]
    public void World_battle_tick_publishes_live_owners_without_serializing_the_dormant_field()
    {
        GameSession session = LoadWorldBattle();
        using GameWorld world = Restore(session);
        BattleSession battle = world.Battle!;
        JsonElement dormant = session.State.EventState!.Value;
        long fieldFrame = world.Events.Frame;
        long callbacks = battle.NativeHostCallbacks!.Value;
        long timer = session.ExamTimer!.ClockCalls;
        long mainLoop = session.State.WorldProcess!.MainLoopCallbacks;

        Advance(world);

        long executed = battle.NativeHostCallbacks!.Value - callbacks;
        Assert.True(executed > 0);
        // JsonElement equality preserves the document identity, so an equivalent reserialization also fails this assertion.
        Assert.Equal(dormant, session.State.EventState!.Value);
        Assert.Equal(fieldFrame, world.Events.Frame);
        Assert.Equal(mainLoop + executed, session.State.WorldProcess!.MainLoopCallbacks);
        Assert.Equal(timer + executed * 4, session.ExamTimer!.ClockCalls);
        Assert.Equal(battle.NativeHostCallbacks, session.State.Battle!.NativeHostCallbacks);
        Assert.Equal(battle.NativeMusicProcess, session.State.Battle.NativeMusicProcess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void World_battle_save_refreshes_the_dormant_field_and_restores_the_next_callback(bool compact)
    {
        GameSession session = LoadWorldBattle();
        using GameWorld world = Restore(session);
        Advance(world);
        JsonElement dormant = session.State.EventState!.Value;
        JsonElement current = world.Events.Capture();
        Assert.False(JsonElement.DeepEquals(dormant, current));
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-world-battle-publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "battle.json");
            session.Save(path, compact);
            GameSession restoredSession = new(GameSession.Load(path));
            Assert.True(JsonElement.DeepEquals(current, restoredSession.State.EventState!.Value));
            using GameWorld restored = Restore(restoredSession);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle!.Capture()), JsonSerializer.SerializeToElement(restored.Battle!.Capture())));

            Advance(world);
            Advance(restored);

            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle.Capture())));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.PrepareSnapshot()), JsonSerializer.SerializeToElement(restoredSession.PrepareSnapshot())));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static GameSession LoadWorldBattle()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_BATTLE_SNAPSHOT");
        Assert.SkipUnless(path is not null && File.Exists(path), "An optional standalone world battle fixture is required.");
        GameSession session = new(GameSession.Load(path!));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(SceneKind.WorldMap, session.State.RandomEncounters?.Pending?.Scene);
        Assert.NotNull(session.State.Battle);
        return session;
    }

    private static GameWorld Restore(GameSession session)
    {
        GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.NotNull(world.Field);
        Assert.NotNull(world.WorldScene);
        Assert.NotNull(world.Battle);
        return world;
    }

    private static void Advance(GameWorld world)
    {
        world.Battle!.AdvanceNativeGraphicsPresentation(Callback);
        world.SynchronizeBattlePresentation();
        world.ApplyBattleInput(new(BattleInputKind.Tick));
    }
}
