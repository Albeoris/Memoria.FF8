using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterTemplateSourceBindingsTests
{
    [Fact]
    public void A_batch_binds_distinct_siblings_within_their_declared_and_inherited_scope()
    {
        BattleSourceGroupId donor = new(11);
        BattleSourceGroupId differentDonor = new(12);
        BattleSourceGroupId priorCopy = BattleSourceGroupId.ForEncounterCopy(2);
        BattleSourcePlacementBinding[] bindings = Admit(
            (0, "twin", null), (1, "twin", null), (2, "other", null),
            (0, "twin", donor), (1, "twin", donor), (1, "twin", differentDonor),
            (0, "twin", priorCopy), (1, "twin", priorCopy));

        Assert.Equal(bindings[0].Group, bindings[1].Group);
        Assert.NotEqual(bindings[0].Group, bindings[2].Group);
        Assert.Equal(bindings[3].Group, bindings[4].Group);
        Assert.NotEqual(bindings[3].Group, bindings[5].Group);
        Assert.Equal(bindings[6].Group, bindings[7].Group);
        Assert.Equal(5, bindings.Select(binding => binding.Group).Distinct().Count());
        Assert.Equal(20, bindings[0].Group.Slot);
    }

    [Fact]
    public void Duplicate_self_copies_keep_separate_scopes_and_cannot_own_an_ambiguous_sibling()
    {
        BattleSourcePlacementBinding[] bindings = Admit((0, "mirrors", null), (0, "mirrors", null), (1, "mirrors", null), (2, "mirrors", null));

        Assert.NotEqual(bindings[0].Group, bindings[1].Group);
        Assert.NotEqual(bindings[0].Group, bindings[2].Group);
        Assert.NotEqual(bindings[1].Group, bindings[2].Group);
        Assert.Equal(bindings[2].Group, bindings[3].Group);
        Assert.Equal(22, bindings[2].Group.Slot);
    }

    private static BattleSourcePlacementBinding[] Admit(params (int Ordinal, string Group, BattleSourceGroupId? Namespace)[] sources)
    {
        var requests = ImmutableArray.CreateBuilder<BattleRosterAllocationRequest>(sources.Length);
        for (int index = 0; index < sources.Length; index++)
        {
            int generation = 20 + index;
            NativeBattleEnemyAllocation allocation = new($"native-allocation-{generation}", index, generation)
            {
                TemplateSource = new(BattleSourceGroupId.ForEncounterCopy(generation), index, sources[index].Ordinal)
            };
            requests.Add(new(allocation, BattleRosterSlot.Enemy(index), null, false));
        }
        BattleSession.ApplyEncounterTemplateSourceGroups(requests, sources.Select(source => ((string?)source.Group, source.Namespace)).ToArray());
        return requests.Select(request => request.Allocation.TemplateSource!).ToArray();
    }
}
