using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class JunctionLearnedAbilityPerformanceTests
{
    [Fact]
    public void Native_banks_union_assigned_guardians_and_match_the_full_menu()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[0] = vitals.Records[0] with { Junction = vitals.Records[0].Junction with { GuardianForces = 3 } };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new((1UL << 1) | (1UL << 2) | (1UL << 18), 1UL << (100 - 64)) };
        vitals.GuardianForces[1] = vitals.GuardianForces[1] with { LearnedBank = new(1UL << 17, 0) };

        Assert.Equal(new[] { 1, 2, 17, 18, 100 }, vitals.JunctionLearnedAbilities(0).Order());
        AssertEquivalent(vitals);
        Assert.Equal(3, vitals.JunctionAbilitySlots(0));
        Assert.True(vitals.JunctionMagicSlotAvailable(0, 18));
    }

    [Fact]
    public void Managed_ledger_filters_forgotten_and_removed_entries_but_keeps_item_taught_abilities()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Templates = vitals.ResolveGuardianForceTemplates().SetItem(0, [new(1, false, 10), new(2, false, 10), new(3, false, 10), new(6, false, 10), new(100, false, 10)]) } };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with
        {
            LearnedBank = new(1UL << 5, 0),
            Progression = new(true,
            [
                new(1, true),
                new(2, true, Active: false),
                new(3, false, Forgotten: true),
                new(4, true, ItemTaught: true),
                new(5, true),
                new(6, false),
                new(100, true)
            ])
        };

        Assert.Equal(new[] { 1, 2, 4, 100 }, vitals.JunctionLearnedAbilities(0).Order());
        AssertEquivalent(vitals);
        Assert.False(vitals.JunctionMagicSlotAvailable(0, 2));
        Assert.True(vitals.JunctionMagicSlotAvailable(0, 3));
    }

    [Fact]
    public void Managed_ledger_without_explicit_templates_uses_native_learning_and_bank_identities()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Learning = vitals.MenuDefinitions.Learning.SetItem(0, vitals.MenuDefinitions.Learning[0].SetItem(0, new(1, 255, 2))) } };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with
        {
            LearnedBank = new(1UL << 1, 1UL << (100 - 64)),
            Progression = new(true, [new(1, true), new(2, true), new(3, true), new(4, true, ItemTaught: true), new(100, true)])
        };

        Assert.Equal(new[] { 1, 2, 4, 100 }, vitals.JunctionLearnedAbilities(0).Order());
        AssertEquivalent(vitals);
    }

    [Fact]
    public void Learned_reads_preserve_validation_for_invalid_rows_and_ledgers()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => vitals.JunctionLearnedAbilities(8));
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Progression = new(true, [new(1, true, Forgotten: true)]) };
        Assert.Throws<InvalidDataException>(() => vitals.JunctionLearnedAbilities(0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Learned_reads_allocate_less_than_projecting_the_full_learning_menu(bool managed)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        ImmutableArray<GuardianForceAbilityTemplate> templates = Enumerable.Range(1, 115).Select(ability => new GuardianForceAbilityTemplate(ability, false, 10)).ToImmutableArray();
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Templates = vitals.ResolveGuardianForceTemplates().SetItem(0, templates) } };
        if (managed)
        {
            vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Progression = new(true, templates.Select(entry => new GuardianForceAbilityProgress(entry.Ability, entry.Ability < 19)).ToImmutableArray()) };
        }
        AssertEquivalent(vitals);
        ReadLearned();
        ReadMenu();
        long learnedBytes = Allocate(ReadLearned);
        long menuBytes = Allocate(ReadMenu);
        Console.WriteLine($"Managed={managed}, 20 learned reads: {learnedBytes:N0} bytes; full menu projection: {menuBytes:N0} bytes.");
        Assert.True(learnedBytes < menuBytes, $"Learned reads allocated {learnedBytes:N0} bytes versus {menuBytes:N0} for full menu projection.");

        void ReadLearned()
        {
            vitals.JunctionLearnedAbilities(0);
        }

        void ReadMenu()
        {
            vitals.GuardianForceAbilities(0).Where(entry => entry.Learned).Select(entry => entry.Ability).ToHashSet();
        }
    }

    private static long Allocate(Action read)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 20; index++)
        {
            read();
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void AssertEquivalent(NativeCharacterVitals vitals)
    {
        int[] expected = vitals.Records[0].Junction.GuardianForceAssignments.SelectMany(id => vitals.GuardianForceAbilities(id).Where(entry => entry.Learned).Select(entry => entry.Ability)).Distinct().Order().ToArray();
        Assert.Equal(expected, vitals.JunctionLearnedAbilities(0).Order());
    }
}
