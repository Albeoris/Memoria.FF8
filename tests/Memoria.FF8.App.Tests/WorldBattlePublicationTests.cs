using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldBattlePublicationTests
{
    [Fact]
    public void Completed_world_battle_return_preserves_achievement_transactions()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("world-battle-result-achievement-cursor")));
        BattleEncounterOccurrence encounter = session.State.PendingBattleEncounter!;
        BattleAchievementCursor cursor = Assert.IsType<BattleAchievementCursor>(session.State.AchievementCursor);
        Assert.Equal(encounter.OccurrenceId, cursor.OccurrenceId);
        Assert.NotEmpty(cursor.Allocations);
        EnemyDefeatedEvent[] pending = session.State.PendingAchievementFacts.ToArray();
        Assert.NotEmpty(pending);
        Assert.Empty(session.State.PendingAchievementObservations);
        using GameWorld world = Restore(session);
        Assert.Equal(BattleOutcome.Victory, world.Battle!.Outcome);

        world.ApplyBattleInput(new(BattleInputKind.Return));

        Assert.Null(session.State.AchievementCursor);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Null(session.State.Battle);
        Assert.Contains(encounter.OccurrenceId, session.State.AppliedBattleResultIds);
        Assert.Equal(pending.Select(fact => fact.EventId), session.State.PendingAchievementFacts.Select(fact => fact.EventId));
        Assert.All(session.State.PendingAchievementFacts, fact => Assert.Equal(BattleOutcome.Victory, fact.Facts.AcceptedOutcome));
        BattleKillTotalObservedEvent observation = Assert.Single(session.State.PendingAchievementObservations);
        Assert.Equal("battle-results/" + encounter.OccurrenceId, observation.ContextId.Value);
        Assert.Equal(BattleOutcome.Victory, observation.Outcome);
        Assert.True(observation.NativeKillTotal > 0);
        session.Save(TestResources.ArtifactFile("world-battle-result-achievements-return.ff8"), compact: true);
        GameSession restoredSession = new(GameSession.Load(TestResources.ArtifactFile("world-battle-result-achievements-return.ff8")));
        using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
        restored.EnsureField();
        Assert.NotNull(restored.WorldScene);
        world.Apply(new(PlayerActionKind.Wait));
        restored.Apply(new(PlayerActionKind.Wait));
        Assert.Equal(JsonSerializer.Serialize(world.WorldScene!.State), JsonSerializer.Serialize(restored.WorldScene!.State));
        Assert.Equal(observation, Assert.Single(restoredSession.State.PendingAchievementObservations));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_BATTLE_RETURN_OUTPUT") is { Length: > 0 } output)
        {
            session.Save(output, compact: true);
        }
    }

    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);

    [Fact]
    public void Fresh_world_battle_entry_selects_native_depth_mix_and_restores_the_next_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_BATTLE_ENTRY_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone world battle entry fixture is required.");
        GameSession session = new(GameSession.Load(source!));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Null(session.State.Battle);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        NativeBattlePrimitiveDepthState depth = world.Battle!.NativeEffectMemory!.PrimitiveProcess!.Depth!;
        Assert.Equal(0.8f, depth.BucketMix);
        Assert.Equal((float)(1d - (double)0.8f), depth.RemainingWeight);
        Advance(world);
        Assert.Equal(depth, world.Battle.NativeGraphics!.PrimitiveFrame!.LastDepthState);
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-world-battle-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "battle.json");
            session.Save(path, compact: true);
            GameSession restoredSession = new(GameSession.Load(path));
            using GameWorld restored = Restore(restoredSession);
            Assert.Equal(depth, restored.Battle!.NativeEffectMemory!.PrimitiveProcess!.Depth);
            Advance(world);
            Advance(restored);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle.Capture())));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void World_battle_physical_action_completes_with_initialized_depth_and_restores_its_next_callback()
    {
        GameSession session = LoadWorldBattle();
        using GameWorld world = Restore(session);
        BattleSession battle = world.Battle!;
        for (int callback = 0; callback < 600 && (!battle.NativeEntryReady || !battle.Combatants.Any(actor => actor.IsPlayer && actor.IsReady)); callback++)
        {
            Advance(world);
        }
        Assert.True(battle.NativeEntryReady);
        BattleCombatant actor = battle.Combatants.First(actor => actor.IsPlayer && actor.IsReady);
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable);
        int completed = battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == actor.Id);
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id));
        for (int callback = 0; callback < 600 && battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == actor.Id) == completed; callback++)
        {
            Advance(world);
        }
        Assert.Contains(battle.Events, item => item.Kind == "Windup" && item.ActorId == actor.Id);
        Assert.True(battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == actor.Id) > completed);
        Assert.Equal(0.8f, battle.NativeGraphics!.PrimitiveFrame!.LastDepthState!.BucketMix);
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.PrepareSnapshot()))!;
        using GameWorld restored = Restore(new GameSession(saved));

        Advance(world);
        Advance(restored);

        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle!.Capture())));
    }

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
