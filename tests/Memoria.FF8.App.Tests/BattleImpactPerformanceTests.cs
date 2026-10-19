using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleImpactPerformanceTests
{
    [Fact]
    public void Named_field_save_continues_through_repeated_attacks_and_reports_contact_costs()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_IMPACT_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone field or battle snapshot is required.");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(evidence), "A distinct diagnostic output directory is required.");
        string id = "impact-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = StartWorld(session);
        BattleSession battle = world.Battle!;
        session.SetLiveBattle(battle);
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-before", session.State);
        BattleScenePresentation scene = new(world.BattleResources, battle.Capture().Definition, true);
        List<object> samples = [];
        int completed = 0;
        int soundCount = 0;
        BattleAudioPresentation audio = new(_ => soundCount++, (_, _) => soundCount++, _ => soundCount++, () => { }, _ => { });
        audio.Update(battle, scene.CollectSounds(battle));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        TimeSpan? idleBudget = Environment.GetEnvironmentVariable("MEMORIA_FF8_IMPACT_INTERACTIVE_BUDGET") == "1" ? TimeSpan.FromSeconds(1d / 60) : null;
        for (int frame = 0; frame < 1200 && completed < 4; frame++)
        {
            BattleCombatant? actor = battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady);
            BattleCombatant? target = battle.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable);
            if (actor is not null && target is not null)
            {
                world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id));
            }
            int before = battle.Events.Count;
            long beforeCallback = battle.NativeHostCallbacks!.Value;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            battle.AdvanceNativeGraphicsPresentation(callback, idleBudget);
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            double update = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            start = Stopwatch.GetTimestamp();
            scene.Render(battle, 320, 224);
            double render = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            audio.Update(battle, scene.CollectSounds(battle));
            BattleEvent[] events = battle.Events.Skip(before).ToArray();
            completed += events.Count(item => item.Kind == "ActionComplete");
            samples.Add(new { battle.Frame, Callback = battle.NativeGraphics!.Callback, HostCallbacks = battle.NativeHostCallbacks, DispatchedCallbacks = battle.NativeHostCallbacks - beforeCallback, AllocatedBytes = allocated, Update = update, Render = render, SoundCount = soundCount, Events = events.Select(item => new { item.Kind, item.ActorId, item.NativeModuleSound }).ToArray() });
        }
        session.SetLiveBattle(battle);
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-after", session.State);
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-timings", samples);
        Assert.True(completed >= 4, $"Completed {completed} actions at battle frame {battle.Frame}.");
        foreach (BattleCombatant actor in battle.Combatants.Where(actor => actor.IsPlayer))
        {
            Assert.Contains(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == actor.Id);
        }
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-hashes", new { State = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(session.State))), Pixels = Convert.ToHexString(SHA256.HashData(scene.Render(battle, 320, 224))) });
        using GameWorld restored = new(new GameSession(GameSession.Load(Path.Combine(evidence!, id + "-after.json"))), TestResources.GameDirectory);
        restored.EnsureField();
        battle.AdvanceNativeGraphicsPresentation(callback);
        restored.Battle!.AdvanceNativeGraphicsPresentation(callback);
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        restored.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Battle.Capture()));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    [Fact]
    public void Interactive_idle_budget_does_not_change_logical_readiness_or_presentation_cadence()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_IMPACT_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone field or battle snapshot is required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld full = StartWorld(session);
        session.SetLiveBattle(full.Battle!);
        session.SetEventState(full.Events.Capture());
        using GameWorld budgeted = StartWorld(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!));
        BattleSession immediate = full.Battle!;
        BattleSession zeroBudget = budgeted.Battle!;
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        bool skippedLogicalTime = false;
        for (int frame = 0; frame < 300 && !immediate.Combatants.Any(actor => actor.IsReady); frame++)
        {
            long beforeHostCallbacks = immediate.NativeHostCallbacks!.Value;
            long beforeGraphics = immediate.NativeGraphics!.Callback;
            long beforeBudgetedGraphics = zeroBudget.NativeGraphics!.Callback;
            immediate.AdvanceNativeGraphicsPresentation(callback);
            full.ApplyBattleInput(new(BattleInputKind.Tick));
            zeroBudget.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            budgeted.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Equal(beforeGraphics + 1, immediate.NativeGraphics.Callback);
            Assert.Equal(beforeBudgetedGraphics + 1, zeroBudget.NativeGraphics.Callback);
            Assert.True(immediate.NativeHostCallbacks!.Value >= beforeHostCallbacks + 1);
            skippedLogicalTime |= immediate.NativeHostCallbacks.Value > beforeHostCallbacks + 1;
            Assert.Equal(immediate.Combatants.Any(actor => actor.IsReady), zeroBudget.Combatants.Any(actor => actor.IsReady));
            Assert.Equal(JsonSerializer.Serialize(immediate.Capture()), JsonSerializer.Serialize(zeroBudget.Capture()));
            Assert.True(JsonElement.DeepEquals(full.Events.Capture(), budgeted.Events.Capture()));
        }
        Assert.True(skippedLogicalTime, "The fixture must reach an idle skip containing multiple logical time steps.");
        Assert.Contains(immediate.Combatants, actor => actor.IsReady);
        Assert.Equal(new BattleScenePresentation(full.BattleResources, immediate.Capture().Definition, true).Render(immediate, 320, 224), new BattleScenePresentation(budgeted.BattleResources, zeroBudget.Capture().Definition, true).Render(zeroBudget, 320, 224));
        long callbacks = zeroBudget.NativeHostCallbacks!.Value;
        long graphics = zeroBudget.NativeGraphics!.Callback;
        for (int frame = 0; frame < 3; frame++)
        {
            immediate.AdvanceNativeGraphicsPresentation(callback);
            zeroBudget.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            full.ApplyBattleInput(new(BattleInputKind.Tick));
            budgeted.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Equal(JsonSerializer.Serialize(immediate.Capture()), JsonSerializer.Serialize(zeroBudget.Capture()));
            Assert.True(JsonElement.DeepEquals(full.Events.Capture(), budgeted.Events.Capture()));
        }
        Assert.Equal(graphics + 3, zeroBudget.NativeGraphics.Callback);
        Assert.True(zeroBudget.NativeHostCallbacks!.Value >= callbacks + 3);
        session.SetLiveBattle(zeroBudget);
        session.SetEventState(budgeted.Events.Capture());
        using GameWorld restored = StartWorld(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!));
        Assert.Equal(JsonSerializer.Serialize(zeroBudget.Capture()), JsonSerializer.Serialize(restored.Battle!.Capture()));
        Assert.Throws<ArgumentOutOfRangeException>(() => zeroBudget.AdvanceNativeGraphicsPresentation(callback, TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void Named_standalone_loads_and_continues_the_next_live_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_IMPACT_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        Assert.SkipUnless(source is not null && File.Exists(source) && !string.IsNullOrWhiteSpace(evidence), "An optional standalone snapshot and distinct diagnostic output directory are required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = StartWorld(session);
        session.SetLiveBattle(world.Battle!);
        session.SetEventState(world.Events.Capture());
        using GameWorld independent = StartWorld(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        long before = world.Battle!.NativeHostCallbacks!.Value;
        long beforeGraphics = world.Battle.NativeGraphics!.Callback;
        world.Battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        independent.Battle!.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        independent.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(beforeGraphics + 1, world.Battle.NativeGraphics!.Callback);
        Assert.True(world.Battle.NativeHostCallbacks!.Value >= before + 1);
        Assert.Equal(JsonSerializer.Serialize(world.Battle.Capture()), JsonSerializer.Serialize(independent.Battle.Capture()));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
        session.SetLiveBattle(world.Battle);
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, "impact-resume-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"), session.State);
    }

    private static GameWorld StartWorld(GameSession session)
    {
        GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        for (int tick = 0; tick < 1500 && world.Battle is null && !world.HasPendingBattle; tick++)
        {
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction input = dialogue?.FirstChoice is int choice ? new(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable ? new(PlayerActionKind.Move, Vertical: -1) : new(PlayerActionKind.Wait);
            world.Apply(input);
        }
        if (world.Battle is null)
        {
            Assert.True(world.HasPendingBattle, $"Field {world.Events.FieldId}, frame {world.Events.Frame}, trigger {world.NextTrigger}");
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: 0));
        }
        return world;
    }
}
