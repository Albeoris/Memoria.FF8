using Memoria.FF8.Assets;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayBattlePolicyTests
{
    [Fact]
    public void AutomaticVictoryOverridesAnOlderPendingInteractiveBattleWithoutChangingItsSourceSave()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "main.zzz")), "Select Remastered resources for this policy check.");
        string source = TestResources.FixturePath("App.Tests.AutoPlayFullBattleTests.1");
        GameSession session = new(GameSession.Load(source));
        int applied = session.State.AppliedBattleResultIds.Length;
        string output = TestResources.ArtifactFile("automatic-victory");
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        AutoPlayController controller = new(session, () => world, _ => throw new InvalidOperationException("No exploration rollback is needed."), output, fullBattlePlaythroughOverride: false);
        controller.StartFromCurrentState();
        controller.Advance(1);
        Assert.Null(controller.Error);
        Assert.Null(session.State.FullBattlePlaythrough);
        Assert.False(controller.State.FullBattlePlaythroughOverride);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Equal(applied + 1, session.State.AppliedBattleResultIds.Length);
        Assert.True(GameSession.Load(source).FullBattlePlaythrough);
        string returned = Assert.Single(Directory.EnumerateFiles(output, "latest.run.json", SearchOption.AllDirectories));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(returned));
        Assert.False(manifest.RootElement.GetProperty("FullBattlePlaythroughOverride").GetBoolean());
    }
}

public sealed class AutoPlayFullBattleTests
{
    [Fact]
    public void PendingNativeEncounterUsesOrdinaryCommandsForksAndReturnsItsActualResultOnce()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "main.zzz")), "Select Remastered resources for this reached full-combat check.");
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayFullBattleTests.1")));
        Assert.True(session.State.FullBattlePlaythrough);
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        Assert.Equal(3, occurrence.Formation);
        int applied = session.State.AppliedBattleResultIds.Length;
        string output = TestResources.ArtifactFile("full-combat");
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        AutoPlayController controller = new(session, () => world, _ => throw new InvalidOperationException("This battle does not require an exploration rollback."), output, deadlineTime: new PausedInspectionClock());
        controller.StartFromCurrentState();
        bool restored = false;
        bool victory = false;
        for (int step = 0; step < 2000; step++)
        {
            controller.Advance(1);
            Assert.Null(controller.Error);
            if (world.Battle is { } battle)
            {
                Assert.NotNull(battle.Presentation?.Graphics);
                Assert.Null(battle.DebugOutcome);
                Assert.True(session.State.FullBattlePlaythrough);
                if (!restored && battle.ActiveAction is not null)
                {
                    BattleSession fork = battle.ForkForContinuation();
                    BattleInput input = AutoPlayController.PlanBattleInput(battle);
                    Assert.Equal(input, AutoPlayController.PlanBattleInput(fork));
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                    fork.Tick();
                    AssertStateEqual(battle.Capture(), fork.Capture());
                    Assert.Equal(battle.Presentation!.Graphics.Callback, fork.Presentation!.Graphics.Callback);
                    restored = true;
                }
                if (battle.CanReturnToField)
                {
                    Assert.Equal(BattleOutcome.Victory, battle.Outcome);
                    victory = true;
                }
                continue;
            }
            if (session.State.PendingBattleEncounter is null)
            {
                Assert.True(restored);
                Assert.True(victory);
                Assert.Equal(applied + 1, session.State.AppliedBattleResultIds.Length);
                Assert.Equal(1, session.State.AppliedBattleResultIds.Count(id => id == occurrence.OccurrenceId));
                Assert.Equal(SceneKind.Field, session.State.Scene);
                string returnedManifest = Assert.Single(Directory.EnumerateFiles(output, "latest.run.json", SearchOption.AllDirectories));
                AutoPlayRunState returned = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(returnedManifest), AutoPlayController.JsonOptions)!;
                GameState returnedState = GameSession.Load(Path.Combine(Path.GetDirectoryName(returnedManifest)!, returned.Snapshot!));
                Assert.Null(returnedState.PendingBattleEncounter);
                Assert.Equal(applied + 1, returnedState.AppliedBattleResultIds.Length);
                Assert.Equal(1, returnedState.AppliedBattleResultIds.Count(id => id == occurrence.OccurrenceId));
                Assert.Throws<InvalidOperationException>(() => world.ApplyBattleInput(new(BattleInputKind.Return)));
                Assert.Equal(applied + 1, session.State.AppliedBattleResultIds.Length);
                return;
            }
        }
        Assert.Fail("The reached native encounter did not return within the bounded ordinary-command continuation.");
    }

    [Fact]
    public void Host_elapsed_is_used_once_and_explicit_callbacks_resume_after_autoplay()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, allies: 3, additionalEnemies: 0);
        string output = TestResources.ArtifactFile("autoplay-battle-elapsed");
        AutoPlayController controller = new(session, () => world, _ => throw new InvalidOperationException("The live entry must keep its source world."), output, deadlineTime: new PausedInspectionClock(), fullBattlePlaythroughOverride: true);
        controller.StartFromCurrentState();
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattlePresentationProcess presentation = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.False(presentation.EntryReady);
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromMilliseconds(10));
        Assert.Equal(0L, presentation.Graphics.Callback);

        GameplayClock clock = new();
        clock.Configure(3, 200);
        int submissions = 0;
        controller.Advance(256, active =>
        {
            Assert.Same(battle, active);
            submissions++;
            active.AdvanceNativeGraphicsPresentation(clock.Advance(TimeSpan.FromMilliseconds(30), session.State.Scene, battle: true));
            world.SynchronizeBattlePresentation();
        });

        Assert.Null(controller.Error);
        Assert.Equal(1, submissions);
        Assert.Equal(TimeSpan.FromMilliseconds(60), clock.Total);
        Assert.Equal(1L, presentation.Graphics.Callback);
        BattleSession fork = battle.ForkForContinuation();
        controller.Stop();
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        fork.Tick();
        Assert.Equal(2L, presentation.Graphics.Callback);
        Assert.Equal(presentation.Graphics.Callback, fork.Presentation!.Graphics.Callback);
        Assert.Equal(presentation.Graphics.PresentationSubticks, fork.Presentation.Graphics.PresentationSubticks);
        Assert.Equal(presentation.Graphics.PresentationHalfSubtick, fork.Presentation.Graphics.PresentationHalfSubtick);
    }

    private static void AssertStateEqual(BattleSnapshot first, BattleSnapshot second)
    {
        JsonElement expected = JsonSerializer.SerializeToElement(first);
        JsonElement actual = JsonSerializer.SerializeToElement(second);
        List<string> differences = [];
        Compare(expected, actual, "$", differences);
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static void Compare(JsonElement first, JsonElement second, string path, List<string> differences)
    {
        if (differences.Count >= 8 || JsonElement.DeepEquals(first, second))
        {
            return;
        }
        if (first.ValueKind == second.ValueKind && first.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in first.EnumerateObject())
            {
                if (second.TryGetProperty(property.Name, out JsonElement value))
                {
                    Compare(property.Value, value, path + "." + property.Name, differences);
                }
                else
                {
                    differences.Add(path + "." + property.Name + " missing");
                }
            }
            foreach (JsonProperty property in second.EnumerateObject().Where(property => !first.TryGetProperty(property.Name, out _)))
            {
                differences.Add(path + "." + property.Name + " added");
            }
            return;
        }
        if (first.ValueKind == second.ValueKind && first.ValueKind == JsonValueKind.Array && first.GetArrayLength() == second.GetArrayLength())
        {
            for (int index = 0; index < first.GetArrayLength() && differences.Count < 8; index++)
            {
                Compare(first[index], second[index], path + "[" + index + "]", differences);
            }
            return;
        }
        differences.Add(path + ": " + first.GetRawText()[..Math.Min(first.GetRawText().Length, 80)] + " != " + second.GetRawText()[..Math.Min(second.GetRawText().Length, 80)]);
    }

    private sealed class PausedInspectionClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
