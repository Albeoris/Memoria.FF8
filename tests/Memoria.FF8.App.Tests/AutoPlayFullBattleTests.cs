using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayFullBattleTests
{
    [Fact]
    public void PendingNativeEncounterUsesOrdinaryCommandsRestoresAndReturnsItsActualResultOnce()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "main.zzz")), "Select Remastered resources for this reached full-combat check.");
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayFullBattleTests.1")));
        Assert.True(session.State.FullBattlePlaythrough);
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        Assert.Equal(3, occurrence.Formation);
        int applied = session.State.AppliedBattleResultIds.Length;
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        AutoPlayController controller = new(session, () => world, _ => throw new InvalidOperationException("This battle does not require an exploration rollback."), TestResources.ArtifactDirectory, deadlineTime: new PausedInspectionClock());
        controller.StartFromCurrentState();
        bool restored = false;
        bool victory = false;
        for (int step = 0; step < 2000; step++)
        {
            controller.Advance(1);
            Assert.Null(controller.Error);
            if (world.Battle is { } battle)
            {
                Assert.NotNull(battle.NativeGraphics);
                Assert.Null(battle.DebugOutcome);
                Assert.True(session.State.FullBattlePlaythrough);
                if (!restored && battle.ActiveAction is not null)
                {
                    string path = TestResources.ArtifactFile("native-active-battle.ff8");
                    session.Save(path, compact: true);
                    GameSession second = new(GameSession.Load(path));
                    using GameWorld replay = new(second, directory, validateWorldPresentation: false);
                    replay.EnsureField();
                    BattleInput input = AutoPlayController.PlanBattleInput(battle);
                    Assert.Equal(input, AutoPlayController.PlanBattleInput(replay.Battle!));
                    Assert.Equal(world.ApplyBattleInput(input), replay.ApplyBattleInput(input));
                    AssertStateEqual(session.PrepareSnapshot(), second.PrepareSnapshot());
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
                string outcome = Assert.Single(Directory.EnumerateFiles(TestResources.ArtifactDirectory, "battle-outcome-*.json", SearchOption.AllDirectories), path => !path.EndsWith(".run.json", StringComparison.Ordinal));
                Assert.Equal(BattleOutcome.Victory, GameSession.Load(outcome).Battle!.Outcome);
                string returned = Assert.Single(Directory.EnumerateFiles(TestResources.ArtifactDirectory, "battle-return-*.json", SearchOption.AllDirectories), path => !path.EndsWith(".run.json", StringComparison.Ordinal));
                Assert.Null(GameSession.Load(returned).PendingBattleEncounter);
                Assert.Throws<InvalidOperationException>(() => world.ApplyBattleInput(new(BattleInputKind.Return)));
                Assert.Equal(applied + 1, session.State.AppliedBattleResultIds.Length);
                return;
            }
        }
        Assert.Fail("The reached native encounter did not return within the bounded ordinary-command continuation.");
    }

    private static void AssertStateEqual(GameState first, GameState second)
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
