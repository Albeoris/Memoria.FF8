using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class GameplaySaveGuardianForceTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(104)]
    public void PendingRecoveryKeepsFullHistoryThroughNativeProgressAndRepeatedColdProjection(int ability)
    {
        NativeCharacterVitals source = PendingRecovery(ability);
        string before = JsonSerializer.Serialize(source);
        string history = JsonSerializer.Serialize(source.GuardianForceRecoveryProgression);
        NativeCharacterVitals selected = Profile(Base(), Templates()) with { GuardianForceProfile = null };
        string selectedBefore = JsonSerializer.Serialize(selected);
        GameplaySaveComponentDocument document = RoundTrip(Document(source));
        NativeCharacterVitals restored = document.Characters.Restore(selected);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Equal(selectedBefore, JsonSerializer.Serialize(selected));
        Assert.Equal(history, JsonSerializer.Serialize(restored.GuardianForceRecoveryProgression));
        Assert.Null(restored.GuardianForceProfile);
        Assert.True(restored.GuardianForceRequiresMemoriaFormat);
        Assert.False(restored.GuardianForces[0].HasLearnedAbility(ability));
        Assert.Equal(source.GuardianForceAbilities(0), restored.GuardianForceAbilities(0));
        Assert.Equal(9, Entry(restored, 100).EarnedPoints);
        Assert.True(Entry(restored, 101).Forgotten);
        Assert.True(Entry(restored, 103).ItemTaught);
        Assert.True(Entry(restored, 105).Learned);
        Assert.False(Entry(restored, 105).Active);
        NativeCharacterVitals progressed = restored.SelectGuardianForceLearning(0, 100).AwardGuardianForceAbilityPoints(0, 5);
        Assert.Equal(14, Entry(progressed, 100).EarnedPoints);
        int slot = Array.FindIndex(progressed.MenuDefinitions.ResolveLearning(0).ToArray(), rule => rule.Ability == 100);
        progressed.GuardianForces[0] = progressed.GuardianForces[0] with { AbilityProgress = progressed.GuardianForces[0].AbilityProgress.SetItem(slot, 16) };
        NativeCharacterVitals normalized = progressed.NormalizeGuardianForceCompatibility();
        Assert.Equal(16, Entry(normalized, 100).EarnedPoints);
        Assert.Equal(history, JsonSerializer.Serialize(normalized.GuardianForceRecoveryProgression));
        Assert.False(normalized.GuardianForces[0].HasLearnedAbility(ability));
        GameplaySaveComponentDocument current = RoundTrip(Document(normalized));
        string canonical = JsonSerializer.Serialize(current);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            NativeCharacterVitals cold = current.Characters.Restore(selected).NormalizeGuardianForceCompatibility();
            Assert.Equal(history, JsonSerializer.Serialize(cold.GuardianForceRecoveryProgression));
            Assert.Equal(16, Entry(cold, 100).EarnedPoints);
            Assert.False(cold.GuardianForces[0].HasLearnedAbility(ability));
            Assert.Equal(normalized.GuardianForceAbilities(0), cold.GuardianForceAbilities(0));
            current = RoundTrip(Document(cold));
            Assert.Equal(canonical, JsonSerializer.Serialize(current));
        }
        string json = JsonSerializer.Serialize(current.Characters.PendingGuardianForceRecovery);
        foreach (string excluded in new[] { "Profile", "Template", "Fingerprint", "Sha256", "LastProjection", "LearningRules", "RequiredPoints" })
        {
            Assert.DoesNotContain(excluded, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AcceptedExactSnapshotWithPendingRecoveryCanBeProjectedWithoutChangingItsFile()
    {
        NativeCharacterVitals source = PendingRecovery(104);
        GameState state = GameSession.CreateColdProcess().State with { CharacterVitals = source };
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-pending-gf-" + Guid.NewGuid().ToString("N"));
        try
        {
            new GameSession(state).Save(path, compact: true);
            byte[] original = File.ReadAllBytes(path);
            GameState loaded = GameSession.Load(path);
            GameplaySaveComponentDocument document = RoundTrip(Document(loaded.CharacterVitals!));
            NativeCharacterVitals restored = document.Characters.Restore(Profile(Base(), Templates()));
            Assert.Equal(JsonSerializer.Serialize(source.GuardianForceRecoveryProgression), JsonSerializer.Serialize(restored.GuardianForceRecoveryProgression));
            Assert.False(restored.GuardianForces[0].HasLearnedAbility(104));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingOptionalRecoveryOwnerRetainsThePreviousNoPendingHistoryMeaning()
    {
        GameplaySaveComponentDocument saved = RoundTrip(Document(Progressed()));
        Assert.Null(saved.Characters.PendingGuardianForceRecovery);
        Assert.DoesNotContain("PendingGuardianForceRecovery", JsonSerializer.Serialize(saved), StringComparison.Ordinal);
        NativeCharacterVitals restored = saved.Characters.Restore(Profile(Base(), Templates()));
        Assert.Empty(restored.GuardianForceRecoveryProgression);
        Assert.Null(GameplaySaveCharacters.Capture(restored).PendingGuardianForceRecovery);
    }

    [Theory]
    [InlineData("rows")]
    [InlineData("duplicate")]
    [InlineData("ability")]
    [InlineData("ap")]
    [InlineData("forgotten")]
    public void MalformedPendingRecoveryRefusesWithoutChangingTheCurrentOwner(string malformed)
    {
        GameplaySaveCharacters saved = GameplaySaveCharacters.Capture(PendingRecovery(104));
        GameplaySaveGuardianForceRecovery history = saved.PendingGuardianForceRecovery!;
        GameplaySaveGuardianForceRecoveryRow?[] rows = history.GuardianForces.ToArray();
        GameplaySaveGuardianForceRecoveryRow row = rows[0]!;
        rows[0] = malformed switch
        {
            "duplicate" => row with { Abilities = [row.Abilities[0], row.Abilities[0]] },
            "ability" => row with { Abilities = [row.Abilities[0] with { Ability = 128 }] },
            "ap" => row with { Abilities = [row.Abilities[0] with { EarnedPoints = 256 }] },
            "forgotten" => row with { Abilities = [row.Abilities[0] with { Learned = true, Forgotten = true }] },
            _ => row
        };
        GameplaySaveGuardianForceRecovery invalid = history with { GuardianForces = malformed == "rows" ? rows[..15] : rows };
        NativeCharacterVitals current = Profile(Base(), Templates());
        string before = JsonSerializer.Serialize(current);
        Assert.Throws<InvalidDataException>(() => (saved with { PendingGuardianForceRecovery = invalid }).Restore(current));
        Assert.Equal(before, JsonSerializer.Serialize(current));
    }

    [Fact]
    public void PendingRecoveryRequiresRepresentedFlagsAndPreservesExplicitFalseZeroAndNullRows()
    {
        GameplaySaveGuardianForceRecovery history = GameplaySaveCharacters.Capture(PendingRecovery(104)).PendingGuardianForceRecovery!;
        GameplaySaveGuardianForceRecovery restored = JsonSerializer.Deserialize<GameplaySaveGuardianForceRecovery>(JsonSerializer.Serialize(history))!;
        Assert.False(restored.GuardianForces[15]!.Initialized);
        Assert.False(restored.GuardianForces[15]!.Abilities[0].Learned);
        Assert.False(restored.GuardianForces[15]!.Abilities[0].Active);
        Assert.Equal(0, restored.GuardianForces[15]!.Abilities[0].EarnedPoints);
        Assert.Null(restored.GuardianForces[2]);
        JsonObject missing = JsonSerializer.SerializeToNode(history)!.AsObject();
        missing["GuardianForces"]![0]!["Abilities"]![0]!.AsObject().Remove("ItemTaught");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameplaySaveGuardianForceRecovery>(missing.ToJsonString()));
    }

    private static NativeCharacterVitals PendingRecovery(int ability)
    {
        NativeCharacterVitals source = Progressed();
        ImmutableArray<GuardianForceProgression?> history = Enumerable.Repeat<GuardianForceProgression?>(null, 16).ToImmutableArray()
            .SetItem(0, new(true, [new(ability, true, 12, ItemTaught: true), new(105, true, 7, Active: false)]))
            .SetItem(1, new(true, [new(106, false, 0, Active: false)]))
            .SetItem(15, new(false, [new(107, false, 0, Forgotten: true, ItemTaught: true, Active: false)]));
        return source with { GuardianForceOpaqueBaseline = true, GuardianForceRecoveryProfile = source.GuardianForceProfile, GuardianForceRecoveryProgression = history, GuardianForceRecoveryTemplates = source.ResolveGuardianForceTemplates() };
    }
}
