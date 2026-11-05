using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveGuardianForceTests
{
    [Fact]
    public void InstalledProfileRoundTripPreservesProgressAndRebuildsSelectedRuntimeProjection()
    {
        NativeCharacterVitals source = Progressed();
        source.GuardianForces[0] = source.GuardianForces[0] with { HitPoints = 9999, Experience = 1234, KillCount = 9, DeathCount = 2, NativeOpaqueBytes = new(1, 2, 3, 4) };
        string original = JsonSerializer.Serialize(source);
        GameplaySaveComponentDocument saved = RoundTrip(Document(source));
        NativeCharacterVitals selected = Profile(Base() with { InitOutSha256 = new string('a', 64), KernelSha256 = new string('b', 64) }, [new(79, true, 10), new(100, false, 30), new(101, false, 40), new(102, false, 50)]);
        string beforeSelection = JsonSerializer.Serialize(selected);

        NativeCharacterVitals restored = saved.Characters.Restore(selected);

        Assert.Equal(9, Entry(restored, 100).EarnedPoints);
        Assert.False(Entry(restored, 100).Learned);
        Assert.True(Entry(restored, 101).Forgotten);
        Assert.True(Entry(restored, 103).ItemTaught);
        Assert.True(restored.GuardianForces[0].HasLearnedAbility(103));
        Assert.True(restored.GuardianForces[0].MoveFindDisabled);
        Assert.False(restored.GuardianForcePassiveAbilityEnabled(0, 79));
        Assert.Equal((ushort)9999, restored.GuardianForces[0].HitPoints);
        Assert.Equal(selected.Definitions.GuardianForceMaximum(0, restored.GuardianForces[0]), restored.GuardianForces[0].MaximumHitPoints);
        Assert.Equal((ushort?)9, restored.GuardianForces[0].KillCount);
        Assert.Equal((ushort?)2, restored.GuardianForces[0].DeathCount);
        Assert.Equal(source.GuardianForces[0].NativeOpaqueBytes, restored.GuardianForces[0].NativeOpaqueBytes);
        Assert.NotNull(restored.GuardianForces[0].LastProjection);
        Assert.Same(selected.MenuDefinitions, restored.MenuDefinitions);
        Assert.Same(selected.GuardianForceProfile, restored.GuardianForceProfile);
        Assert.Equal(selected.KernelSha256, restored.KernelSha256);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Equal(beforeSelection, JsonSerializer.Serialize(selected));
        string json = JsonSerializer.Serialize(saved);
        Assert.DoesNotContain("LastProjection", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceFingerprint", json, StringComparison.Ordinal);
        Assert.DoesNotContain("LearningRules", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RecoveryTemplates", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredPoints", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovedAbilitiesRemainDormantAndReturnWithKnownApAndForgottenState()
    {
        NativeCharacterVitals source = Progressed();
        NativeCharacterVitals selected = Profile(Base(), [new(79, true, 10), new(104, false, 30)]);
        NativeCharacterVitals dormant = RoundTrip(Document(source)).Characters.Restore(selected);

        Assert.Equal(9, Entry(dormant, 100).EarnedPoints);
        Assert.False(Entry(dormant, 100).Active);
        Assert.True(Entry(dormant, 101).Forgotten);
        Assert.False(Entry(dormant, 101).Active);
        Assert.True(dormant.GuardianForces[0].HasLearnedAbility(103));
        Assert.True(dormant.GuardianForces[0].MoveFindDisabled);
        NativeCharacterVitals returned = RoundTrip(Document(dormant)).Characters.Restore(Profile(Base(), Templates()));
        Assert.True(Entry(returned, 100).Active);
        Assert.Equal(9, Entry(returned, 100).EarnedPoints);
        Assert.True(Entry(returned, 101).Forgotten);
        Assert.True(returned.GuardianForces[0].MoveFindDisabled);
        NativeCharacterVitals learned = returned.SelectGuardianForceLearning(0, 100).AwardGuardianForceAbilityPoints(0, 11);
        Assert.True(learned.GuardianForces[0].HasLearnedAbility(100));
        Assert.Equal(20, Entry(learned, 100).EarnedPoints);
    }

    [Fact]
    public void NativeSlotBindingsRemapApAndForgottenFlagsByAbilityIdentity()
    {
        NativeCharacterVitals source = Base();
        source.GuardianForces[0] = source.GuardianForces[0] with { AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 9).SetItem(1, 7), ForgottenAbilities = 2, Learning = 100 };
        NativeCharacterVitals installed = Base();
        installed = installed with { MenuDefinitions = installed.MenuDefinitions with { Learning = installed.MenuDefinitions.Learning.SetItem(0, Rules(101, 100, 102)) } };

        NativeCharacterVitals restored = RoundTrip(Document(source)).Characters.Restore(installed);

        Assert.Equal((byte)7, restored.GuardianForces[0].AbilityProgress[0]);
        Assert.Equal((byte)9, restored.GuardianForces[0].AbilityProgress[1]);
        Assert.Equal(1u, restored.GuardianForces[0].ForgottenAbilities & 3);
        Assert.Equal(9, Entry(restored, 100).EarnedPoints);
        Assert.True(Entry(restored, 101).Forgotten);
        Assert.Equal((byte)100, restored.GuardianForces[0].Learning);
    }

    [Fact]
    public void DuplicateNativeBindingsPreserveEachSavedOccurrence()
    {
        NativeCharacterVitals source = Base();
        source = source with { MenuDefinitions = source.MenuDefinitions with { Learning = source.MenuDefinitions.Learning.SetItem(0, Rules(100, 101, 100)) } };
        source.GuardianForces[0] = source.GuardianForces[0] with { AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 9).SetItem(2, 17), ForgottenAbilities = 1 };
        NativeCharacterVitals installed = Base();
        installed = installed with { MenuDefinitions = installed.MenuDefinitions with { Learning = installed.MenuDefinitions.Learning.SetItem(0, Rules(101, 100, 102, 100)) } };

        NativeCharacterVitals restored = RoundTrip(Document(source)).Characters.Restore(installed);

        Assert.Equal((byte)9, restored.GuardianForces[0].AbilityProgress[1]);
        Assert.Equal((byte)17, restored.GuardianForces[0].AbilityProgress[3]);
        Assert.NotEqual(0u, restored.GuardianForces[0].ForgottenAbilities & 2);
        Assert.Equal(0u, restored.GuardianForces[0].ForgottenAbilities & 8);
    }

    [Fact]
    public void CompatibilityProjectionIsConsumedBeforeCapturingExternalProgress()
    {
        NativeCharacterVitals source = Progressed();
        source.GuardianForces[0] = source.GuardianForces[0] with { AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 13) };
        string original = JsonSerializer.Serialize(source);

        GameplaySaveCharacters captured = GameplaySaveCharacters.Capture(source);

        Assert.Equal(13, captured.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == 100).EarnedPoints);
        Assert.Equal(9, Entry(source, 100).EarnedPoints);
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void ResolvedRecoveryRetainsUniqueKnownHistoryWithoutRecoveryResources()
    {
        NativeCharacterVitals source = Progressed();
        ImmutableArray<GuardianForceProgression?> history = Enumerable.Repeat<GuardianForceProgression?>(null, 16).ToImmutableArray().SetItem(0, new(true, [new(104, true, 12)]));
        source = source with { GuardianForceOpaqueBaseline = true, GuardianForceRecoveryProfile = source.GuardianForceProfile, GuardianForceRecoveryProgression = history, GuardianForceRecoveryTemplates = source.ResolveGuardianForceTemplates() };

        NativeCharacterVitals restored = RoundTrip(Document(source)).Characters.Restore(Profile(Base(), Templates()));

        Assert.True(Entry(restored, 104).Learned);
        Assert.Equal(12, Entry(restored, 104).EarnedPoints);
        Assert.False(Entry(restored, 104).Active);
        Assert.False(Entry(restored, 104).ItemTaught);
        Assert.Null(restored.GuardianForceRecoveryProfile);
        Assert.Empty(restored.GuardianForceRecoveryProgression);
        Assert.Empty(restored.GuardianForceRecoveryTemplates);
        Assert.False(restored.GuardianForceOpaqueBaseline);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(104)]
    public void UnresolvedItemTaughtRecoveryRefusesWithoutLosingOrReactivatingProvenance(int ability)
    {
        NativeCharacterVitals source = Progressed();
        ImmutableArray<GuardianForceProgression?> history = Enumerable.Repeat<GuardianForceProgression?>(null, 16).ToImmutableArray().SetItem(0, new(true, [new(ability, true, 12, ItemTaught: true)]));
        source = source with { GuardianForceOpaqueBaseline = true, GuardianForceRecoveryProfile = source.GuardianForceProfile, GuardianForceRecoveryProgression = history };
        string original = JsonSerializer.Serialize(source);

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => GameplaySaveCharacters.Capture(source));

        Assert.Contains("item-taught recovery provenance", error.Message, StringComparison.Ordinal);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.True(source.GuardianForceRecoveryProgression[0]!.Abilities.Single().ItemTaught);
        Assert.False(source.GuardianForces[0].HasLearnedAbility(ability));
    }

    [Fact]
    public void AlreadyRepresentedItemTaughtRecoveryKeepsTheCurrentFact()
    {
        NativeCharacterVitals source = Progressed();
        ImmutableArray<GuardianForceProgression?> history = Enumerable.Repeat<GuardianForceProgression?>(null, 16).ToImmutableArray().SetItem(0, new(true, [new(103, true, 12, ItemTaught: true)]));
        source = source with { GuardianForceOpaqueBaseline = true, GuardianForceRecoveryProfile = source.GuardianForceProfile, GuardianForceRecoveryProgression = history };

        NativeCharacterVitals restored = GameplaySaveCharacters.Capture(source).Restore(Profile(Base(), Templates()));

        Assert.True(Entry(restored, 103).ItemTaught);
        Assert.True(restored.GuardianForces[0].HasLearnedAbility(103));
        Assert.Equal(Entry(source, 103).EarnedPoints, Entry(restored, 103).EarnedPoints);
    }

    [Fact]
    public void UnresolvedRecoveryAndSaveLocalIntentRefuseWithoutChangingSources()
    {
        NativeCharacterVitals source = Progressed();
        GuardianForceAbilityPatchDocument local = new() { DocumentId = "local", Operations = [new() { Id = "add", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.EnsureAbility, Ability = 104 }] };
        NativeCharacterVitals pending = source with { GuardianForceProfile = null, GuardianForceOpaqueBaseline = true, GuardianForceRecoveryProfile = source.GuardianForceProfile };
        Assert.Throws<NotSupportedException>(() => GameplaySaveCharacters.Capture(pending));
        foreach (GuardianForceAbilityProfile profile in new[]
        {
            source.GuardianForceProfile! with { LocalDocuments = [local] },
            source.GuardianForceProfile! with { ExcludedDocuments = ["package-choice"] },
            source.GuardianForceProfile! with { LocalOwnershipAliases = ImmutableDictionary<string, string>.Empty.Add("exported", "local") }
        })
        {
            NativeCharacterVitals authored = source with { GuardianForceProfile = profile };
            string original = JsonSerializer.Serialize(authored);
            Assert.Throws<NotSupportedException>(() => GameplaySaveCharacters.Capture(authored));
            Assert.Equal(original, JsonSerializer.Serialize(authored));
        }
        GameplaySaveCharacters saved = GameplaySaveCharacters.Capture(source);
        Assert.Throws<NotSupportedException>(() => saved.Restore(pending));
    }

    [Fact]
    public void LegacyComponentsWithoutSemanticBindingsStillReadAndRestore()
    {
        NativeCharacterVitals source = Base();
        source.GuardianForces[0] = source.GuardianForces[0] with { AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 9) };
        GameplaySaveCharacters saved = GameplaySaveCharacters.Capture(source);
        saved.GuardianForces[0] = saved.GuardianForces[0] with { AbilityBindings = default, Progression = null };
        GameplaySaveComponentDocument legacy = RoundTrip(Document(source) with { Characters = saved });

        NativeCharacterVitals restored = legacy.Characters.Restore(Base());

        Assert.Equal((byte)9, restored.GuardianForces[0].AbilityProgress[0]);
        Assert.Equal(9, Entry(restored, 100).EarnedPoints);
    }

    [Fact]
    public void CaptureAndRestoreDoNotNormalizeUnrelatedCharacterOrMagicProgress()
    {
        NativeCharacterVitals source = Progressed();
        source.Records[0] = source.Records[0] with { HitPoints = new(9999, 9999), Junction = source.Records[0].Junction with { MagicJunctions = [42, .. new byte[19]] } };
        NativeCharacterVitals restored = RoundTrip(Document(source)).Characters.Restore(Profile(Base(), Templates()));

        Assert.Equal(source.Records, restored.Records);
        Assert.Equal(JsonSerializer.Serialize(source.MagicSystem), JsonSerializer.Serialize(restored.MagicSystem));
        Assert.Equal(0, restored.GuardianForces[15].Flags & 1);
    }

    [Fact]
    public void UnmappedForgottenBitsSurviveRepeatedSemanticAndCompatibilityNormalization()
    {
        NativeCharacterVitals source = Base();
        source.GuardianForces[0] = source.GuardianForces[0] with { ForgottenAbilities = 0xE00400 };

        NativeCharacterVitals first = RoundTrip(Document(source)).Characters.Restore(Base());
        NativeCharacterVitals normalized = first.NormalizeGuardianForceCompatibility();
        NativeCharacterVitals second = RoundTrip(Document(normalized)).Characters.Restore(Base());

        Assert.Equal(0xE00400u, first.GuardianForces[0].ForgottenAbilities);
        Assert.Equal(first.GuardianForces[0].ForgottenAbilities, first.GuardianForces[0].LastProjection!.ForgottenAbilities);
        Assert.Equal(first.GuardianForces[0].ForgottenAbilities, second.GuardianForces[0].ForgottenAbilities);
    }

    private static NativeCharacterVitals Base()
    {
        NativeCharacterVitals result = MagicSystemFixture.Create().DeepCopy();
        for (int id = 0; id < 16; id++)
        {
            result.GuardianForces[id] = result.GuardianForces[id] with { LearnedBank = new(0, 0), Flags = id == 15 ? (byte)0 : (byte)1 };
        }
        return result with { MenuDefinitions = result.MenuDefinitions with { Learning = result.MenuDefinitions.Learning.SetItem(0, Rules(100, 101, 102)) } };
    }

    private static NativeCharacterVitals Progressed()
    {
        NativeCharacterVitals source = Profile(Base(), Templates()).SelectGuardianForceLearning(0, 100).AwardGuardianForceAbilityPoints(0, 9).ForgetGuardianForceAbility(0, 101).TeachGuardianForceAbility(0, 103);
        return source.SetGuardianForcePassiveAbilityEnabled(0, 79, false);
    }

    private static NativeCharacterVitals Profile(NativeCharacterVitals source, ImmutableArray<GuardianForceAbilityTemplate> row)
    {
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = source.ResolveGuardianForceTemplates().SetItem(0, row);
        NativeCharacterVitals result = source.ApplyGuardianForceTemplates(templates);
        return result with { GuardianForceProfile = new(templates, "selected-profile", [new("Selected/Abilities", new() { DocumentId = "selected-abilities" })], [], []) };
    }

    private static ImmutableArray<GuardianForceAbilityTemplate> Templates() => [new(79, true, 10), new(100, false, 20), new(101, false, 30), new(102, false, 40)];
    private static ImmutableArray<NativeGuardianForceLearningRule> Rules(params byte[] abilities) => Enumerable.Range(0, 21).Select(index => new NativeGuardianForceLearningRule(1, 255, index < abilities.Length ? abilities[index] : (byte)0)).ToImmutableArray();
    private static GuardianForceAbilityProgress Entry(NativeCharacterVitals source, int ability) => source.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == ability);

    private static GameplaySaveComponentDocument Document(NativeCharacterVitals characters)
    {
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        NativeLimitProgress limits = new(new string('0', 64), 0, 0, 0x11, 0, ImmutableArray.CreateRange(new byte[8]));
        return GameplaySaveComponentDocument.Capture(new(1, GameplaySaveResumeKind.Field, 106, 105, new(0, 0, 0, 0), null), characters, economy, limits);
    }

    private static GameplaySaveComponentDocument RoundTrip(GameplaySaveComponentDocument document)
    {
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, document);
        stream.Position = 0;
        return GameplaySaveCodec.Read(stream);
    }
}
