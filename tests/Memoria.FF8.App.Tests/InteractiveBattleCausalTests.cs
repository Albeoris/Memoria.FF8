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
    public void NewGameOrdinaryInputsReachIfritFightRestoreAndColdRecreateWithoutDebugVictory()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed game corpus is required for the authentic interactive battle journey.");
        string root = RepositoryRoot();
        string evidence = Path.Combine(root, "../fixtures");
        string recipePath = Path.Combine(evidence, "replay-timber-normal-menu-return-checkpoint.json");
        byte[] originalRecipeBytes = File.ReadAllBytes(recipePath);
        GameState recipe = GameSession.LoadReplaySource(recipePath);
        string scratch = Path.Combine(Path.GetTempPath(), "Memoria-interactive-battle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        Stopwatch routeWatch = Stopwatch.StartNew();
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        MovieClock clock = new(session, game);
        using GameWorld world = new(session, game, clock, persistReplayJournal: false);
        DriveOrdinaryRecipeUntilBattle(session, world, clock, recipe.ReplayJournal!, game);
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
                session.Save(midPath, registerJournalReference: false);
                GameSession restoredSession = new(GameSession.Load(midPath));
                using GameWorld restored = new(restoredSession, game, persistReplayJournal: false);
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
        Assert.True(session.ReplayJournal!.IsComplete, session.ReplayJournal.UnavailableReason);
        session.SetEventState(world.Events.Capture());
        string finalPath = Path.Combine(scratch, "after-ifrit.json");
        session.Save(finalPath, registerJournalReference: false);

        Stopwatch recreateWatch = Stopwatch.StartNew();
        GameState recreatedState = GameWorld.RecreateSave(GameSession.LoadReplaySource(finalPath), game);
        recreateWatch.Stop();
        EqualState(session.State, recreatedState);
        GameSession recreatedSession = new(recreatedState);
        using GameWorld recreated = new(recreatedSession, game, persistReplayJournal: false);
        recreated.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), recreated.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), recreated.Apply(new PlayerAction(PlayerActionKind.Wait)));
        EqualState(session.State, recreatedSession.State);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), recreated.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), recreated.Render(TimeSpan.Zero));
        Assert.Equal(originalRecipeBytes, File.ReadAllBytes(recipePath));

        session.SetEventState(world.Events.Capture());
        bool publishEvidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_PUBLISH_BATTLE_EVIDENCE") == "1";
        string proofDirectory = publishEvidence ? evidence : scratch;
        string published = Path.Combine(proofDirectory, "battle-fidelity-ifrit-checkpoint.json");
        session.Save(published, registerJournalReference: false);
        var proof = new
        {
            Baseline = "Game-owned NewGame input recipe replay-timber-normal-menu-return-checkpoint.json, first interactive BATTLE 94 only",
            Implementations = new[] { "Current Memoria live input host with full combat", "Current Memoria cold-process causal reconstruction" },
            OriginalFF8Parity = "Unproved; primitive Attack, ATB, statuses, enemy stats and AI use documented TD-009 scaffold tuning.",
            RecipeSha256 = Convert.ToHexString(SHA256.HashData(originalRecipeBytes)),
            EarlierDriverDiagnostic = "Older recipe driver stopped at FieldLoad232 while current allocation was229 before the driver reproduced current autonomous allocation barriers; no original-parity or engine-defect claim.",
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
            JournalRecords = session.ReplayJournal.EntryCount,
            MidCombatOwnedSnapshotAndNextTick = true,
            WholeStateExceptJournal = true,
            NextWaitStateVmCpuPixels = true
        };
        File.WriteAllText(Path.Combine(proofDirectory, "battle-fidelity-ifrit-proof.json"), JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(proof));
        string resolvedScratch = Path.GetFullPath(scratch);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        Assert.StartsWith(temporaryRoot, resolvedScratch, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(resolvedScratch, recursive: true);
    }

    private static void DriveOrdinaryRecipeUntilBattle(GameSession session, GameWorld world, MovieClock clock, ReplayJournal recipe, string game)
    {
        using GameAssets assets = new(game);
        Queue<(int FieldId, string ScriptHash, string? ExecutableHash)> autonomousAllocations = new();
        bool configured = false;
        ReplayJournalEntry? movieSkip = null;
        world.Events.MovieSkipRequested -= clock.Skip;
        world.Events.MovieSkipRequested += () =>
        {
            MoviePlaybackState terminal = movieSkip?.MoviePlayback ?? throw new NotSupportedException("Recipe movie skip lacks its genuinely recorded terminal external clock.");
            string hash = MovieClock.GetSourceHash(game, session.State.MovieIndex);
            Assert.Equal(movieSkip!.MovieSourceHash, hash);
            session.SetReplayMovieSourceHash(hash);
            session.SetMoviePlayback(terminal with { IsPlaying = true, Completed = false, Skipped = false });
            session.CompleteMovie(true);
        };
        foreach (ReplayJournalEntry input in recipe.Entries)
        {
            for (int repeat = 0; repeat < input.Repeat; repeat++)
            {
                switch (input.Kind)
                {
                    case ReplayEntryKind.FieldLoad:
                        if (autonomousAllocations.TryDequeue(out var allocation))
                        {
                            Assert.Equal(input.FieldId, allocation.FieldId);
                            Assert.Equal(input.NativeSourceHash, allocation.ScriptHash);
                            Assert.Equal(input.Text, allocation.ExecutableHash);
                        }
                        else
                        {
                            Assert.True(world.EnsureField(), "A recipe allocation barrier did not perform its actual field load.");
                            Assert.Equal(input.FieldId, world.Field?.Id);
                            Assert.Equal(input.NativeSourceHash, Convert.ToHexString(SHA256.HashData(world.Field!.Jsm)));
                            Assert.Equal(input.Text, assets.NativeExecutableHash);
                        }
                        if (!configured)
                        {
                            Assert.Equal(74, world.Field!.Id);
                            Assert.Equal(0, world.Events.Frame);
                            world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigurePlaythrough, FullCombat: true));
                            configured = true;
                        }
                        break;
                    case ReplayEntryKind.Step:
                        world.Events.Step();
                        PublishDeparture(session, world);
                        break;
                    case ReplayEntryKind.Update:
                        world.Events.Update(TimeSpan.FromTicks(input.ElapsedTicks));
                        PublishDeparture(session, world);
                        break;
                    case ReplayEntryKind.Action:
                        if (world.EnsureField())
                        {
                            autonomousAllocations.Enqueue((world.Field!.Id, Convert.ToHexString(SHA256.HashData(world.Field.Jsm)), assets.NativeExecutableHash));
                        }
                        GameWorld.AwaitRecordedDialogueSite(world.Events, input, () =>
                        {
                            world.Events.Step();
                            PublishDeparture(session, world);
                        });
                        movieSkip = input.Code == (int)PlayerActionKind.SkipMovie ? input : null;
                        world.Apply(new PlayerAction((PlayerActionKind)input.Code, input.Value, input.Text, input.Horizontal, input.Vertical, input.Frames));
                        break;
                    case ReplayEntryKind.WorldAction:
                        world.Apply(new PlayerAction((PlayerActionKind)input.Code, input.Value, input.Text, input.Horizontal, input.Vertical, input.Frames));
                        break;
                    case ReplayEntryKind.Dialogue:
                        GameWorld.AwaitRecordedDialogueSite(world.Events, input, () =>
                        {
                            world.Events.Step();
                            PublishDeparture(session, world);
                        });
                        world.Events.ConfirmDialogue(input.Code == 1 ? input.Value : null, input.Code == 2 ? input.Text : null);
                        break;
                    case ReplayEntryKind.MovieSample:
                        Assert.Equal(input.MovieSourceHash, MovieClock.GetSourceHash(game, session.State.MovieIndex));
                        session.SetReplayMovieSourceHash(input.MovieSourceHash!);
                        if (input.MoviePlayback is MoviePlaybackState playback)
                        {
                            session.SetMoviePlayback(playback);
                        }
                        session.SetMoviePosition(input.MovieSeconds);
                        break;
                    case ReplayEntryKind.MovieComplete:
                        Assert.Equal(input.MovieSourceHash, MovieClock.GetSourceHash(game, session.State.MovieIndex));
                        session.SetReplayMovieSourceHash(input.MovieSourceHash!);
                        session.CompleteMovie(input.Skipped);
                        break;
                    default:
                        throw new NotSupportedException($"Ordinary recipe input {input.Kind} is unsupported by this bounded driver.");
                }
                Assert.True(session.ReplayJournal!.IsComplete, session.ReplayJournal.UnavailableReason);
                if (world.Events.PendingInteractiveBattle is not null)
                {
                    return;
                }
                // Preserve the recipe's causal readiness barrier through ordinary autonomous callbacks.
                // The recorded destination is checked only as an outcome; it is never injected.
                if (input.ResultScene.GetValueOrDefault(input.Scene) != input.Scene || input.ResultFieldId.GetValueOrDefault(input.FieldId) != input.FieldId)
                {
                    for (int callback = 0; session.State.Scene == input.Scene && session.State.FieldId == input.FieldId && callback < 30_000; callback++)
                    {
                        if (world.Events.CurrentDialogue is not null || world.Events.LastFault is not null || world.NextTrigger.Kind is PlayerTriggerKind.Movie or PlayerTriggerKind.WorldMap or PlayerTriggerKind.FieldTransition)
                        {
                            break;
                        }
                        world.Events.Step();
                        PublishDeparture(session, world);
                        if (world.Events.PendingInteractiveBattle is not null)
                        {
                            return;
                        }
                    }
                }
            }
        }
        throw new InvalidDataException("The ordinary recipe ended before the first interactive battle.");
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
        left.Remove("ReplayJournal");
        right.Remove("ReplayJournal");
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
