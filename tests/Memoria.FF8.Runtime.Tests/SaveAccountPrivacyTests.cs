using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SaveAccountPrivacyTests
{
    [Fact]
    public void Vm_diagnostics_drop_host_details_and_legacy_movie_paths_without_changing_names_or_fault_boundary()
    {
        JsonElement vm = JsonSerializer.SerializeToElement(new
        {
            LastFault = new { FieldId = 165, ObjectId = 3, ScriptId = 2, Operation = 7, Opcode = "MOVIEREADY", Message = @"Cannot open ../fixtures/external-fixture.avi" },
            FaultContext = new { Diagnostic = "private-account" },
            MovieSource = new { DiscIndex = 1, MovieIndex = 7, Path = @"../fixtures/external-fixture.avi" },
            Scene = new { Names = new Dictionary<int, string> { [0] = @"C:\Hero", [1] = "x@y.example" } }
        });
        GameState live = new() { EventState = vm, DepartingFieldState = vm, WorldEventState = vm };
        GameState portable = SaveAccountPrivacy.Detach(live);
        foreach (JsonElement saved in new[] { portable.EventState!.Value, portable.DepartingFieldState!.Value, portable.WorldEventState!.Value })
        {
            Assert.DoesNotContain("private-user", saved.GetRawText());
            Assert.DoesNotContain("private-account", saved.GetRawText());
            Assert.Equal(7, saved.GetProperty("LastFault").GetProperty("Operation").GetInt32());
            Assert.Equal("MOVIEREADY", saved.GetProperty("LastFault").GetProperty("Opcode").GetString());
            Assert.Equal(2, saved.GetProperty("MovieSource").EnumerateObject().Count());
            Assert.True(JsonElement.DeepEquals(vm.GetProperty("Scene"), saved.GetProperty("Scene")));
        }
        Assert.Contains("private-user", live.EventState!.Value.GetRawText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Portable_save_detaches_accounts_without_changing_live_facts_or_gameplay_input(bool compact)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ff8save");
        try
        {
            GameState original = State();
            GameSession session = new(original);
            session.Save(path, compact);
            AssertPortable(original, GameSession.Load(path));
            Assert.Equal(original.LastOriginalAchievementObservation, session.State.LastOriginalAchievementObservation);
            if (!compact)
            {
                string payload = File.ReadAllText(path);
                Assert.DoesNotContain("real-account", payload);
                Assert.DoesNotContain("private-profile", payload);
                Assert.DoesNotContain(original.LastOriginalAchievementObservation!.Value.ContextId.Value, payload);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Legacy_load_detaches_account_and_coalesces_identical_unbound_observations()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ff8save");
        try
        {
            GameState original = State();
            OriginalAchievementObservedEvent fact = original.LastOriginalAchievementObservation!.Value;
            GameState legacy = original with { PendingOriginalAchievementObservations = [fact, OriginalAchievementObservedEvent.Create(fact.Progress, fact.Owner! with { User = "second-account" })] };
            File.WriteAllText(path, JsonSerializer.Serialize(legacy));
            GameState loaded = GameSession.Load(path);
            AssertPortable(original, loaded);
            Assert.Single(loaded.PendingOriginalAchievementObservations);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static GameState State()
    {
        AchievementOwner owner = new("provider", "product", "real-account", "private-profile");
        OriginalAchievementObservedEvent observation = OriginalAchievementObservedEvent.Create(new() { MagazineCollection = true }, owner);
        return new()
        {
            Scene = SceneKind.Field,
            FieldId = 165,
            X = 25,
            PendingEntityNames = [new(2, "x@y.example", "item:24") { Draft = @"C:\Hero" }],
            PendingAchievementFacts = [new(new("defeat"), new("battle"), new("defeat", BattleDefeatCredit.Character, 0, null, 0, false, BattleRewardPolicy.NativeCombat)) { Owner = owner }],
            PendingAchievementObservations = [new(new("kills"), new("battle"), 12, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 0, owner)],
            PendingOriginalAchievementObservations = [observation],
            PendingOriginalAchievementActions = [new(new("salary"), new("field"), OriginalAchievementActionKind.FirstSalary, Owner: owner)],
            LastOriginalAchievementObservation = observation
        };
    }

    private static void AssertPortable(GameState original, GameState loaded)
    {
        Assert.Equal(original.FieldId, loaded.FieldId);
        Assert.Equal(original.X, loaded.X);
        Assert.Equal(original.PendingEntityNames, loaded.PendingEntityNames);
        Assert.Equal(original.PendingAchievementFacts[0] with { Owner = null }, Assert.Single(loaded.PendingAchievementFacts));
        Assert.Equal(original.PendingAchievementObservations[0] with { Owner = null }, Assert.Single(loaded.PendingAchievementObservations));
        Assert.Equal(original.PendingOriginalAchievementActions[0] with { Owner = null }, Assert.Single(loaded.PendingOriginalAchievementActions));
        OriginalAchievementObservedEvent expected = OriginalAchievementObservedEvent.Create(original.LastOriginalAchievementObservation!.Value.Progress, null);
        Assert.Equal(expected, loaded.LastOriginalAchievementObservation);
        Assert.Equal(expected, Assert.Single(loaded.PendingOriginalAchievementObservations));
        expected.Validate();
    }
}
