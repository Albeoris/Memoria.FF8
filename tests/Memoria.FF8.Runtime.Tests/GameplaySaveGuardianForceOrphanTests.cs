using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class GameplaySaveGuardianForceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedOrphanBindingsImportCurrentNativeValuesBeforeCurrentDefinitionProjection(bool acquired)
    {
        NativeCharacterVitals source = OrphanedBindings();
        source.GuardianForces[0] = source.GuardianForces[0] with { Flags = acquired ? (byte)1 : (byte)0, HitPoints = 9999 };
        string before = JsonSerializer.Serialize(source);
        string history = JsonSerializer.Serialize(source.GuardianForceRecoveryProgression);
        Assert.Equal((byte)9, source.GuardianForces[0].LastProjection!.AbilityProgress[0]);
        Assert.Equal((byte)17, source.GuardianForces[0].AbilityProgress[0]);
        Assert.False(source.GuardianForces[0].LastProjection!.LearnedBank.Has(102));
        Assert.True(source.GuardianForces[0].LearnedBank.Has(102));
        GameplaySaveComponentDocument saved = RoundTrip(Document(source));
        NativeCharacterVitals installed = SelectedOrphanDefinitions();
        string definitionsBefore = JsonSerializer.Serialize(installed);
        NativeCharacterVitals restored = saved.Characters.Restore(installed);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Equal(definitionsBefore, JsonSerializer.Serialize(installed));
        Assert.Equal(source.GuardianForces[0].Flags, restored.GuardianForces[0].Flags);
        Assert.Equal((ushort)9999, restored.GuardianForces[0].HitPoints);
        Assert.Equal(17, Entry(restored, 100).EarnedPoints);
        Assert.False(Entry(restored, 100).Learned);
        Assert.False(restored.GuardianForces[0].HasLearnedAbility(100));
        Assert.Equal(12, Entry(restored, 101).EarnedPoints);
        Assert.True(Entry(restored, 101).Forgotten);
        Assert.Equal((byte)12, restored.GuardianForces[0].AbilityProgress[0]);
        Assert.Equal((byte)17, restored.GuardianForces[0].AbilityProgress[1]);
        Assert.Equal(1u, restored.GuardianForces[0].ForgottenAbilities & 3);
        Assert.True(restored.GuardianForces[0].HasLearnedAbility(102));
        Assert.True(restored.GuardianForces[0].HasLearnedAbility(103));
        Assert.False(restored.GuardianForces[0].HasLearnedAbility(104));
        Assert.Equal(history, JsonSerializer.Serialize(restored.GuardianForceRecoveryProgression));
        Assert.Equal(new byte[] { 101, 100, 104 }, saved.Characters.GuardianForces[0].AbilityBindings.Take(3));
        string canonical = JsonSerializer.Serialize(saved);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            restored = RoundTrip(Document(restored.NormalizeGuardianForceCompatibility())).Characters.Restore(installed);
            Assert.Equal(history, JsonSerializer.Serialize(restored.GuardianForceRecoveryProgression));
            Assert.False(restored.GuardianForces[0].HasLearnedAbility(100));
            Assert.False(restored.GuardianForces[0].HasLearnedAbility(104));
            Assert.Equal(17, Entry(restored, 100).EarnedPoints);
            Assert.Equal(12, Entry(restored, 101).EarnedPoints);
            Assert.Equal(canonical, JsonSerializer.Serialize(RoundTrip(Document(restored))));
        }
        string json = JsonSerializer.Serialize(saved);
        Assert.DoesNotContain("LastProjection", json, StringComparison.Ordinal);
        Assert.DoesNotContain("LearningRules", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredPoints", json, StringComparison.Ordinal);
    }

    [Fact]
    public void OrphanRecoveryUsesCurrentForgottenAndLearnedBitsInsteadOfCachedProjectionValues()
    {
        NativeCharacterVitals source = OrphanedBindings();
        NativeGuardianForceVital gf = source.GuardianForces[0];
        Assert.Equal(2u, gf.LastProjection!.ForgottenAbilities & 3);
        Assert.True(gf.LastProjection.LearnedBank.Has(103));
        source.GuardianForces[0] = gf with { ForgottenAbilities = (gf.ForgottenAbilities & ~3u) | 1, LearnedBank = gf.LearnedBank with { High = gf.LearnedBank.High & ~(1UL << (103 - 64)) } };
        string before = JsonSerializer.Serialize(source);
        NativeCharacterVitals restored = RoundTrip(Document(source)).Characters.Restore(SelectedOrphanDefinitions());
        Assert.True(Entry(restored, 100).Forgotten);
        Assert.False(Entry(restored, 101).Forgotten);
        Assert.Equal(2u, restored.GuardianForces[0].ForgottenAbilities & 3);
        Assert.False(restored.GuardianForces[0].HasLearnedAbility(103));
        Assert.True(restored.GuardianForces[0].HasLearnedAbility(102));
        Assert.Equal(17, Entry(restored, 100).EarnedPoints);
        Assert.Equal(before, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void AcceptedMappedExactSnapshotCanResaveByRetainedBindingsWithoutChangingItsSource()
    {
        NativeCharacterVitals source = OrphanedBindings();
        GameState state = GameSession.CreateColdProcess().State with { CharacterVitals = source };
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-orphan-gf-" + Guid.NewGuid().ToString("N"));
        try
        {
            new GameSession(state).Save(path, compact: true);
            byte[] original = File.ReadAllBytes(path);
            GameState loaded = GameSession.Load(path);
            Assert.Null(loaded.CharacterVitals!.GuardianForces[0].Progression);
            NativeCharacterVitals restored = RoundTrip(Document(loaded.CharacterVitals)).Characters.Restore(SelectedOrphanDefinitions());
            Assert.Equal(17, Entry(restored, 100).EarnedPoints);
            Assert.True(Entry(restored, 101).Forgotten);
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unsupported")]
    public void ChangedOrphanMappingsWithoutUnambiguousAbilityIdentitiesStillRefuse(string kind)
    {
        NativeCharacterVitals source = OrphanedBindings();
        NativeGuardianForceVital gf = source.GuardianForces[0];
        ImmutableArray<NativeGuardianForceLearningRule> rules = kind == "duplicate" ? Rules(100, 101, 100) : Rules(100, 127, 102);
        source.GuardianForces[0] = gf with { LastProjection = gf.LastProjection! with { LearningRules = rules } };
        string before = JsonSerializer.Serialize(source);
        Assert.Throws<NotSupportedException>(() => GameplaySaveCharacters.Capture(source));
        Assert.Equal(before, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void MissingOrphanMapIsRejectedButNoProjectionKeepsTheExistingCurrentMapInterpretation()
    {
        NativeCharacterVitals source = OrphanedBindings();
        NativeGuardianForceVital gf = source.GuardianForces[0];
        source.GuardianForces[0] = gf with { LastProjection = gf.LastProjection! with { LearningRules = default } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveCharacters.Capture(source));
        source.GuardianForces[0] = gf with { LastProjection = null };
        NativeCharacterVitals restored = RoundTrip(Document(source)).Characters.Restore(SelectedOrphanDefinitions());
        Assert.Equal(17, Entry(restored, 101).EarnedPoints);
        Assert.Equal(12, Entry(restored, 100).EarnedPoints);
        Assert.True(Entry(restored, 100).Forgotten);
    }

    private static NativeCharacterVitals OrphanedBindings()
    {
        NativeCharacterVitals source = PendingRecovery(104);
        NativeGuardianForceVital gf = source.GuardianForces[0];
        source.GuardianForces[0] = gf with { Progression = null, AbilityProgress = gf.AbilityProgress.SetItem(0, 17).SetItem(1, 12), LearnedBank = gf.LearnedBank with { High = gf.LearnedBank.High | (1UL << (102 - 64)) } };
        NativeCharacterVitals selected = SelectedOrphanDefinitions();
        return source with { MenuDefinitions = selected.MenuDefinitions, GuardianForceProfile = selected.GuardianForceProfile };
    }

    private static NativeCharacterVitals SelectedOrphanDefinitions()
    {
        NativeCharacterVitals source = Base();
        source = source with { MenuDefinitions = source.MenuDefinitions with { Learning = source.MenuDefinitions.Learning.SetItem(0, Rules(101, 100, 104)) } };
        return Profile(source, [new(79, true, 10), new(100, true, 30), new(101, false, 40), new(104, false, 50)]);
    }
}
