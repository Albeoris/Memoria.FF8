using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGuardianForceLearningIndexTests
{
    [Fact]
    public void NativeLevelPassWinsBeforeEarlierPrerequisiteAndApUsesFirstMatchingSlot()
    {
        NativeCharacterVitals source = Source([new(102, 255, 100), new(1, 255, 101), new(1, 255, 100)], learned: [101]);
        source.GuardianForces[0] = source.GuardianForces[0] with { AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 3).SetItem(2, 17), Learning = 100 };
        NativeGuardianForceAbility available = Assert.Single(source.GuardianForceAbilities(0), entry => entry.Ability == 100);
        Assert.Equal(2, available.LearningIndex);
        Assert.Equal(17, available.Progress);
        NativeCharacterVitals awarded = source.AwardGuardianForceAbilityPoints(0, 2);
        Assert.Equal((byte)5, awarded.GuardianForces[0].AbilityProgress[0]);
        Assert.Equal((byte)17, awarded.GuardianForces[0].AbilityProgress[2]);
        Assert.False(awarded.GuardianForces[0].LearnedBank.Has(100));
        Assert.Equal(17, Assert.Single(awarded.GuardianForceAbilities(0), entry => entry.Ability == 100).Progress);
    }

    [Fact]
    public void DormantSelfPrerequisiteSlotsStayUnavailableWithoutRejectingSource()
    {
        NativeCharacterVitals source = Source([new(102, 255, 100), new(101, 255, 100)]);
        Assert.DoesNotContain(source.GuardianForceAbilities(0), entry => entry.Ability == 100);
        source = source.ApplyGuardianForceTemplates(source.ResolveGuardianForceTemplates());
        Assert.DoesNotContain(source.GuardianForceAbilities(0), entry => entry.Ability == 100);
        Assert.All(source.GuardianForces[0].AbilityProgress, value => Assert.Equal((byte)0, value));
        NativeCharacterVitals taught = source.TeachGuardianForceAbility(0, 100);
        Assert.True(Assert.Single(taught.GuardianForceAbilities(0), entry => entry.Ability == 100).Learned);
    }

    [Fact]
    public void NativePrerequisiteAndExclusionResolveTableSlotsAndRequireLearnedState()
    {
        NativeCharacterVitals source = Source([new(1, 255, 100), new(101, 255, 101), new(101, 1, 102)], learned: [100]);
        Assert.Contains(source.GuardianForceAbilities(0), entry => entry.Ability == 101 && !entry.Learned);
        Assert.Contains(source.GuardianForceAbilities(0), entry => entry.Ability == 102 && !entry.Learned);
        source.GuardianForces[0] = source.GuardianForces[0] with { LearnedBank = new(0, (1UL << (100 - 64)) | (1UL << (101 - 64))) };
        Assert.DoesNotContain(source.GuardianForceAbilities(0), entry => entry.Ability == 102);
    }

    [Fact]
    public void ForgettingOnlySuppressesFirstMatchingNativeSlot()
    {
        NativeCharacterVitals source = Source([new(1, 255, 100), new(1, 255, 100)]);
        NativeCharacterVitals forgotten = source.ForgetGuardianForceAbility(0, 100);
        Assert.Equal(1u, forgotten.GuardianForces[0].ForgottenAbilities);
        NativeGuardianForceAbility remaining = Assert.Single(forgotten.GuardianForceAbilities(0), entry => entry.Ability == 100);
        Assert.Equal(1, remaining.LearningIndex);
        Assert.False(remaining.Learned);
    }

    [Fact]
    public void SnapshotPreservesNativeCandidatesDistinctApAndPartialSuppression()
    {
        NativeCharacterVitals source = Source([new(1, 255, 100), new(1, 255, 100)]);
        source.GuardianForces[0] = source.GuardianForces[0] with { AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 4).SetItem(1, 9), ForgottenAbilities = 1 };
        NativeCharacterVitals projected = source.ApplyGuardianForceTemplates(source.ResolveGuardianForceTemplates());
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(projected))!;
        restored.Validate();
        NativeCharacterVitals normalized = restored.NormalizeGuardianForceCompatibility();
        Assert.Equal(1u, normalized.GuardianForces[0].ForgottenAbilities);
        Assert.Equal((byte)4, normalized.GuardianForces[0].AbilityProgress[0]);
        Assert.Equal((byte)9, normalized.GuardianForces[0].AbilityProgress[1]);
        NativeGuardianForceAbility available = Assert.Single(normalized.GuardianForceAbilities(0), entry => entry.Ability == 100);
        Assert.Equal(1, available.LearningIndex);
        Assert.Equal(9, available.Progress);
        Assert.False(normalized.GuardianForceRequiresMemoriaFormat);
    }

    [Fact]
    public void AuthoredCyclesRemainInvalidAndSourceSlotBindingsCannotDropDuplicates()
    {
        NativeCharacterVitals source = Source([new(1, 255, 100), new(1, 255, 100)]);
        var templates = source.ResolveGuardianForceTemplates();
        Assert.Throws<InvalidDataException>(() => source.ApplyGuardianForceTemplates(templates.SetItem(0, [new(100, false, 20, PrerequisiteAbility: 101), new(101, false, 20, PrerequisiteAbility: 100)])));
        GuardianForceAbilityTemplate entry = templates[0].Single();
        Assert.Equal(3u, entry.NativeLearningSlots);
        Assert.Throws<InvalidDataException>(() => source.ApplyGuardianForceTemplates(templates.SetItem(0, [entry with { NativeLearningSlots = 1 }])));
    }

    [Fact]
    public void SnapshotWithoutSourceSlotMetadataKeepsLegacyNativeConditions()
    {
        NativeCharacterVitals source = Source([new(1, 255, 100), new(101, 255, 101), new(101, 1, 102)], learned: [100]);
        source.GuardianForces[0] = source.GuardianForces[0] with { AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(1, 9) };
        source = source.ApplyGuardianForceTemplates(source.ResolveGuardianForceTemplates());
        JsonObject json = JsonSerializer.SerializeToNode(source)!.AsObject();
        foreach (string property in new[] { "Templates", "SourceTemplates" })
        {
            JsonArray rows = json["MenuDefinitions"]![property]!.AsArray();
            for (int id = 0; id < rows.Count; id++)
            {
                foreach (JsonNode? node in rows[id]!.AsArray())
                {
                    JsonObject entry = node!.AsObject();
                    entry.Remove(nameof(GuardianForceAbilityTemplate.NativeLearningSlots));
                    int ability = entry["Ability"]!.GetValue<int>();
                    int index = Array.FindIndex(source.MenuDefinitions.Learning[id].ToArray(), rule => rule.Ability == ability);
                    if (index < 0)
                    {
                        continue;
                    }
                    NativeGuardianForceLearningRule rule = source.MenuDefinitions.Learning[id][index];
                    entry["RequiredLevel"] = rule.Requirement <= 100 ? Math.Max(1, (int)rule.Requirement) : 1;
                    entry["PrerequisiteAbility"] = rule.Requirement is >= 101 and <= 121 ? JsonValue.Create((int)source.MenuDefinitions.Learning[id][rule.Requirement - 101].Ability) : null;
                    entry["ExcludedAbility"] = rule.Requirement is >= 101 and <= 121 && rule.Exclusion != 255 ? JsonValue.Create((int)source.MenuDefinitions.Learning[id][rule.Exclusion].Ability) : null;
                    entry["SupportedCondition"] = rule.Requirement <= 121 && ability < source.MenuDefinitions.AbilityPoints.Length;
                }
            }
        }
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(json.ToJsonString())!;
        restored.Validate();
        NativeCharacterVitals normalized = restored.NormalizeGuardianForceCompatibility();
        Assert.Same(restored, normalized);
        for (int id = 0; id < 16; id++)
        {
            Assert.Equal(source.GuardianForceAbilities(id).Select(entry => (entry.Ability, entry.Learned, entry.Progress, entry.RequiredPoints)), normalized.GuardianForceAbilities(id).Select(entry => (entry.Ability, entry.Learned, entry.Progress, entry.RequiredPoints)));
        }
        Assert.False(normalized.GuardianForceRequiresMemoriaFormat);
        Assert.Equal(source.MenuDefinitions.Learning.SelectMany(row => row), normalized.MenuDefinitions.Learning.SelectMany(row => row));
    }

    private static NativeCharacterVitals Source(NativeGuardianForceLearningRule[] rules, int[]? learned = null)
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var table = Enumerable.Repeat(new NativeGuardianForceLearningRule(255, 255, 0), 21).ToArray();
        rules.CopyTo(table, 0);
        NativeGuardianForceLearnedBank bank = new(0, 0);
        foreach (int ability in learned ?? [])
        {
            bank = ability < 64 ? bank with { Low = bank.Low | (1UL << ability) } : bank with { High = bank.High | (1UL << (ability - 64)) };
        }
        source.GuardianForces[0] = source.GuardianForces[0] with { LearnedBank = bank };
        source = source with { MenuDefinitions = source.MenuDefinitions with { Learning = source.MenuDefinitions.Learning.SetItem(0, table.ToImmutableArray()), AbilityPoints = source.MenuDefinitions.AbilityPoints.SetItem(100, 20) } };
        var templates = source.ResolveGuardianForceTemplates();
        return source with { MenuDefinitions = source.MenuDefinitions with { Templates = templates, SourceTemplates = templates, InitialStates = source.GuardianForces.Select(gf => new NativeGuardianForceInitialState(gf.LearnedBank, gf.AbilityProgress, gf.Learning, gf.ForgottenAbilities)).ToImmutableArray() } };
    }
}

