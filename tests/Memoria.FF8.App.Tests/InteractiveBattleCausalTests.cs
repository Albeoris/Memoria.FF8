using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InteractiveBattleCausalTests
{
    [Fact]
    public void IfritSnapshotContinuesFightRestoresAndReturnsWithoutDebugVictory()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed game corpus is required for the authentic interactive battle journey.");
        string root = RepositoryRoot();
        string evidence = Path.Combine(root, "../fixtures");
        string scratch = Path.Combine(Path.GetTempPath(), "Memoria-interactive-battle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        Stopwatch routeWatch = Stopwatch.StartNew();
        GameSession session = new(SnapshotFixtures.Require("MEMORIA_FF8_IFRIT_BATTLE_SNAPSHOT", null));
        MovieClock clock = new(session, game);
        using GameWorld world = new(session, game, clock);
        world.EnsureField();
        routeWatch.Stop();
        Assert.Equal((ushort)94, world.Events.PendingInteractiveBattle!.Encounter);
        world.ApplyBattleInput(new BattleInput(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased));
        long frozenFieldFrame = world.Events.Frame;
        long beforeCombatTimerCalls = session.ExamTimer!.ClockCalls;
        long beforeCombatMainLoopCalls = session.State.WorldProcess!.MainLoopCallbacks;

        int commands = 0;
        int attacks = 0;
        bool testedMidCombat = false;
        bool capturedPresentation = false;
        Stopwatch fightWatch = Stopwatch.StartNew();
        while (world.Battle!.Outcome == BattleOutcome.Active)
        {
            Assert.True(commands++ < 2_000, "Primitive-attack battle exceeded the declared command bound.");
            BattleSession active = world.Battle;
            BattleCombatant? player = active.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady);
            if (player is not null)
            {
                BattleCombatant enemy = active.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive);
                world.ApplyBattleInput(new BattleInput(BattleInputKind.Attack, ActorId: player.Id, TargetId: enemy.Id));
                attacks++;
            }
            else if (active.ActiveAction is not null || active.QueuedActions.Count != 0)
            {
                world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick, BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames + 1));
            }
            else
            {
                world.ApplyBattleInput(new BattleInput(BattleInputKind.AdvanceTurn));
            }
            if (!capturedPresentation && Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PRESENTATION_EVIDENCE") is string presentationEvidence && active.Events.Any(item => item.Kind == "Damage"))
            {
                capturedPresentation = true;
                Directory.CreateDirectory(presentationEvidence);
                string snapshot = Path.Combine(presentationEvidence, "battle-fidelity-ifrit-snapshot.json");
                File.WriteAllText(snapshot, JsonSerializer.Serialize(active.Capture(), new JsonSerializerOptions { WriteIndented = true }));
                Assert.Equal(0, BattlePreview.Run(["--snapshot", snapshot, "--output", Path.Combine(presentationEvidence, "battle-fidelity-ifrit.png"), "--game-dir", game]));
            }
            if (!testedMidCombat && attacks > 0 && active.Outcome == BattleOutcome.Active && (active.ActiveAction is not null || active.QueuedActions.Count != 0))
            {
                testedMidCombat = true;
                session.SetEventState(world.Events.Capture());
                string midPath = Path.Combine(scratch, "mid-combat.json");
                session.Save(midPath);
                GameSession restoredSession = new(GameSession.Load(midPath));
                using GameWorld restored = new(restoredSession, game);
                restored.EnsureField();
                EqualState(session.State, restoredSession.State);
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(active.Capture()), JsonSerializer.SerializeToElement(restored.Battle!.Capture())));
                world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
                restored.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
                EqualState(session.State, restoredSession.State);
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(active.Capture()), JsonSerializer.SerializeToElement(restored.Battle.Capture())));
            }
        }
        fightWatch.Stop();
        Assert.True(testedMidCombat);
        Assert.Equal(BattleOutcome.Victory, world.Battle.Outcome);
        Assert.DoesNotContain(world.Battle.Events, item => item.Kind == "debug-victory");
        long battleFrames = world.Battle.Frame;
        Assert.Equal(frozenFieldFrame, world.Events.Frame);
        Assert.Equal(beforeCombatTimerCalls + battleFrames * 4, session.ExamTimer.ClockCalls);
        Assert.Equal(beforeCombatMainLoopCalls + battleFrames, session.State.WorldProcess.MainLoopCallbacks);
        Assert.Equal(battleFrames, world.Events.Scene.FieldCallbackClock!.BattleCallbacks);
        int[] partyHp = world.Battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.CurrentHp).ToArray();
        world.ApplyBattleInput(new BattleInput(BattleInputKind.Return));
        Assert.Null(world.Battle);
        Assert.Null(session.State.Battle);
        world.Apply(new PlayerAction(PlayerActionKind.Wait));
        session.SetEventState(world.Events.Capture());
        string finalPath = Path.Combine(scratch, "after-ifrit.json");
        session.Save(finalPath);

        Stopwatch recreateWatch = Stopwatch.StartNew();
        GameState recreatedState = GameSession.Load(finalPath);
        recreateWatch.Stop();
        EqualState(session.State, recreatedState);
        GameSession recreatedSession = new(recreatedState);
        using GameWorld recreated = new(recreatedSession, game);
        recreated.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), recreated.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), recreated.Apply(new PlayerAction(PlayerActionKind.Wait)));
        EqualState(session.State, recreatedSession.State);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), recreated.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), recreated.Render(TimeSpan.Zero));

        session.SetEventState(world.Events.Capture());
        bool publishEvidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_PUBLISH_BATTLE_EVIDENCE") == "1";
        string proofDirectory = publishEvidence ? evidence : scratch;
        string published = Path.Combine(proofDirectory, "battle-fidelity-ifrit-checkpoint.json");
        session.Save(published);
        var proof = new
        {
            Baseline = "Explicit pending interactive BATTLE 94 snapshot",
            Implementations = new[] { "Current Memoria live input host with full combat", "Current Memoria independent snapshot continuation" },
            OriginalFF8Parity = "Unproved; primitive Attack, ATB, statuses, enemy stats and AI use documented TD-009 scaffold tuning.",
            CheckpointSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(published))),
            Encounter = 94,
            Outcome = "Victory",
            DebugVictoryUsed = false,
            Attacks = attacks,
            BattleCommands = commands,
            BattleFrames = battleFrames,
            NativeTimerCallsDuringCombat = battleFrames * 4,
            NativeMainLoopCallbacksDuringCombat = battleFrames,
            FieldVmFrozenDuringCombat = true,
            PartyHp = partyHp,
            RouteSeconds = routeWatch.Elapsed.TotalSeconds,
            FightSeconds = fightWatch.Elapsed.TotalSeconds,
            RecreateSeconds = recreateWatch.Elapsed.TotalSeconds,
            MidCombatOwnedSnapshotAndNextTick = true,
            WholeState = true,
            NextWaitStateVmCpuPixels = true
        };
        File.WriteAllText(Path.Combine(proofDirectory, "battle-fidelity-ifrit-proof.json"), JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(proof));
        string resolvedScratch = Path.GetFullPath(scratch);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        Assert.StartsWith(temporaryRoot, resolvedScratch, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(resolvedScratch, recursive: true);
    }

    private static void PublishDeparture(GameSession session, GameWorld world)
    {
        if (world.Events.FieldId != session.State.FieldId || world.Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition || session.State.Scene == SceneKind.WorldMap)
        {
            session.SetDepartingFieldState(world.Events.Capture());
        }
    }

    private static void EqualState(GameState first, GameState second)
    {
        JsonObject left = JsonNode.Parse(JsonSerializer.Serialize(first))!.AsObject();
        JsonObject right = JsonNode.Parse(JsonSerializer.Serialize(second))!.AsObject();
        Assert.True(JsonNode.DeepEquals(left, right), "Current live input host and independent current reconstruction diverged.");
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Research repository root was not found.");
    }
}
