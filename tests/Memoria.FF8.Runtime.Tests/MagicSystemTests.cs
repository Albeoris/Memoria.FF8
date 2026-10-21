using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

internal static class MagicSystemFixture
{
    internal static NativeCharacterVitals Create()
    {
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        return new NativeCharacterVitals
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new string('0', 64),
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(10, 10), 0, new NativeCharacterJunction((ushort)(1 << id), 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 1, 2],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 10, new NativeGuardianForceLearnedBank(0x3FFFF, 0), 10, ImmutableArray.CreateRange(new byte[21]), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), ImmutableArray.CreateRange(Enumerable.Repeat((byte)1, 57)), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5])),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
    }

    internal static NativeCharacterVitals Stock(NativeCharacterVitals vitals, int row, int spell, int count) => vitals.ChangeMagicStock(row, spell, count, MagicOperationReason.ScriptReward, out _);
    internal static NativeCharacterVitals Modes(NativeCharacterVitals vitals, bool shared = false, bool infinite = false) => MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SharedMagicStock = shared, InfiniteMagic = infinite }).Vitals;
}

public sealed class MagicSystemTests
{
    [Fact]
    public void Shared_migration_preserves_overflow_and_uses_stable_record_zero()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        foreach (int row in new[] { 0, 1, 2 })
        {
            vitals = MagicSystemFixture.Stock(vitals, row, 1, 50);
        }
        vitals = vitals with { CombatParty = [2, 1, 255] };
        vitals.Records[0] = vitals.Records[0] with { CharacterId = 8 };
        MagicModePreview preview = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SharedMagicStock = true });
        Assert.Equal(50, vitals.Records[0].MagicInventory[0].Count);
        Assert.Equal(100, preview.Vitals.GetMagicCount(2, 1));
        Assert.Equal(0, preview.Vitals.Records[1].MagicInventory.Sum(stock => stock.Count));
        Assert.Equal(50, preview.Vitals.Records[2].MagicInventory.Sum(stock => stock.Count));
        NativeCharacterVitals full = preview.Vitals.ChangeMagicStock(2, 1, 10, MagicOperationReason.DrawStock, out MagicStockMutation result);
        Assert.Equal(0, result.Accepted);
        Assert.Same(preview.Vitals, full);
    }

    [Fact]
    public void More_than_thirty_two_types_remain_accessible_after_disabling_shared()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        for (int spell = 1; spell <= 40; spell++)
        {
            vitals = MagicSystemFixture.Stock(vitals, spell <= 32 ? 0 : 1, spell, 10);
        }
        vitals = MagicSystemFixture.Modes(vitals, shared: true);
        Assert.Equal(40, vitals.GetMagicInventory(7).Count);
        Assert.Equal(8, vitals.MagicSystem.RecordZeroExtension.Length);
        vitals = MagicSystemFixture.Modes(vitals);
        Assert.Equal(10, vitals.GetMagicCount(0, 40));
        Assert.Equal(2, vitals.GetMagicCapacity(0, 41));
        Assert.Equal(0, vitals.GetMagicCapacity(0, 40));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(33, 1, 1)]
    [InlineData(34, 2, 1)]
    [InlineData(50, 2, 2)]
    [InlineData(67, 3, 2)]
    [InlineData(100, 4, 3)]
    public void Capacities_follow_actor_profile(int stock, int female, int male)
    {
        Assert.Equal(female, MagicCastingService.Capacity(stock, true, 4));
        Assert.Equal(male, MagicCastingService.Capacity(stock, true, 0));
        Assert.Equal(stock == 0 ? 0 : 1, MagicCastingService.Capacity(stock, false, 4));
    }

    [Fact]
    public void Field_tickets_survive_rebinding_and_victory_receipts_are_idempotent()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 4, 1, 100);
        vitals = MagicJunctionPolicy.Assign(vitals, 4, 0, 1);
        vitals = MagicSystemFixture.Modes(vitals, infinite: true);
        for (int cast = 0; cast < 4; cast++)
        {
            string action = "field-" + cast;
            vitals = MagicCastingService.Commit(MagicCastingService.Reserve(vitals, 4, 1, MagicCastContext.Field, action), action);
        }
        Assert.Equal(100, vitals.GetMagicCount(4, 1));
        Assert.False(MagicCastingService.Quote(vitals, 4, 1, MagicCastContext.Field).Allowed);
        vitals = MagicJunctionPolicy.Assign(vitals, 4, 0, 0);
        vitals = MagicCastingService.RecoverVictory(vitals, "victory");
        Assert.Equal(1, MagicCastingService.Quote(vitals, 4, 1, MagicCastContext.Field).Available);
        NativeCharacterVitals same = MagicCastingService.RecoverVictory(vitals, "victory");
        Assert.Same(vitals, same);
        vitals = MagicCastingService.Commit(MagicCastingService.Reserve(vitals, 4, 1, MagicCastContext.Field, "last"), "last");
        vitals = MagicJunctionPolicy.Assign(vitals, 4, 0, 1);
        Assert.Equal(0, MagicCastingService.Quote(vitals, 4, 1, MagicCastContext.Field).Available);
        Assert.Equal(15, vitals.MagicSystem.SharedFieldMasks[1]);
    }

    [Fact]
    public void Last_ticket_reservation_does_not_reject_itself_and_cancellation_costs_nothing()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Modes(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 100), shared: true, infinite: true);
        vitals = MagicCastingService.BeginEncounter(vitals, "battle");
        vitals = MagicCastingService.Reserve(vitals, 1, 1, MagicCastContext.Battle, "one");
        Assert.False(MagicCastingService.Quote(vitals, 2, 1, MagicCastContext.Battle).Allowed);
        Assert.True(MagicCastingService.Quote(vitals, 1, 1, MagicCastContext.Battle, "one").Allowed);
        vitals = MagicCastingService.Release(vitals, "one");
        Assert.True(MagicCastingService.Quote(vitals, 2, 1, MagicCastContext.Battle).Allowed);
        vitals = MagicCastingService.Commit(MagicCastingService.Reserve(vitals, 2, 1, MagicCastContext.Battle, "two"), "two");
        Assert.False(MagicCastingService.Quote(vitals, 1, 1, MagicCastContext.Battle).Allowed);
        Assert.Equal(0, vitals.MagicSystem.SharedFieldMasks[1]);
        Assert.Same(vitals, MagicCastingService.Commit(vitals, "two"));
        Assert.Same(vitals, MagicCastingService.BeginEncounter(vitals, "battle"));
    }
    [Fact]
    public void Shared_owner_conflict_uses_party_order_and_preserves_twentieth_saved_byte()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = MagicSystemFixture.Stock(MagicSystemFixture.Stock(vitals, 0, 1, 50), 1, 1, 50);
        vitals = MagicJunctionPolicy.Assign(MagicJunctionPolicy.Assign(vitals, 0, 0, 1), 1, 0, 1);
        vitals.Records[0].Junction.MagicJunctions[19] = 55;
        vitals = vitals with { CombatParty = [1, 0, 255] };
        MagicModePreview preview = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SharedMagicStock = true });
        Assert.Equal(1, MagicJunctionPolicy.GetOwner(preview.Vitals, 1));
        Assert.Contains(preview.RemovedJunctions, change => change.RecordId == 0 && change.Slot == 0);
        Assert.Equal(55, preview.Vitals.Records[0].Junction.MagicJunctions[19]);
        Assert.False(MagicJunctionPolicy.CanAccess(preview.Vitals, 0, 1));
        Assert.Equal(100, MagicJunctionPolicy.GetEffectiveCount(preview.Vitals, 1, 1));
        Assert.Throws<InvalidOperationException>(() => preview.Vitals.TransferMagicStock(1, 2, 1, 1, out _));
    }

    [Fact]
    public void Personal_transfers_carry_history_without_erasing_the_source()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Modes(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 100), infinite: true);
        vitals = MagicCastingService.Commit(MagicCastingService.Reserve(vitals, 0, 1, MagicCastContext.Field, "cast"), "cast");
        vitals = vitals.TransferMagicStock(0, 1, 1, 50, out MagicStockMutation moved);
        Assert.Equal(50, moved.Accepted);
        Assert.Equal(1, vitals.MagicSystem.PersonalFieldMasks[0][1]);
        Assert.Equal(1, vitals.MagicSystem.PersonalFieldMasks[1][1]);
        Assert.False(MagicCastingService.Quote(vitals, 1, 1, MagicCastContext.Field).Allowed);
        vitals = MagicSystemFixture.Modes(vitals, shared: true, infinite: true);
        Assert.False(MagicCastingService.Quote(vitals, 2, 1, MagicCastContext.Field).Allowed);
    }

    [Fact]
    public void Finite_casts_preserve_and_propagate_initialized_histories()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Modes(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 100), infinite: true);
        vitals = MagicSystemFixture.Modes(vitals);
        vitals = MagicCastingService.Commit(MagicCastingService.Reserve(vitals, 0, 1, MagicCastContext.Field, "finite"), "finite");
        Assert.Equal(99, vitals.GetMagicCount(0, 1));
        Assert.Equal(1, vitals.MagicSystem.PersonalFieldMasks[0][1]);
        Assert.Equal(1, vitals.MagicSystem.SharedFieldMasks[1]);
        vitals = MagicCastingService.RecoverVictory(vitals, "victory");
        Assert.Equal(0, vitals.MagicSystem.PersonalFieldMasks[0][1]);
        Assert.Equal(0, vitals.MagicSystem.SharedFieldMasks[1]);
    }

    [Fact]
    public void Pending_reservations_and_extension_round_trip_without_fresh_charges()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        for (int spell = 1; spell <= 40; spell++)
        {
            vitals = MagicSystemFixture.Stock(vitals, spell <= 32 ? 0 : 1, spell, 10);
        }
        vitals = MagicSystemFixture.Modes(vitals, shared: true, infinite: true);
        vitals = MagicCastingService.Reserve(vitals, 1, 40, MagicCastContext.Battle, "queued");
        string json = System.Text.Json.JsonSerializer.Serialize(vitals);
        NativeCharacterVitals restored = System.Text.Json.JsonSerializer.Deserialize<NativeCharacterVitals>(json)!;
        restored.ValidateMagicSystemConsistency();
        Assert.Equal(10, restored.GetMagicCount(2, 40));
        Assert.False(MagicCastingService.Quote(restored, 2, 40, MagicCastContext.Battle).Allowed);
        Assert.True(MagicCastingService.Quote(restored, 1, 40, MagicCastContext.Battle, "queued").Allowed);
        NativeCharacterVitals copy = restored.DeepCopy();
        copy.MagicSystem.SharedFieldMasks[40] = 15;
        Assert.Equal(0, restored.MagicSystem.SharedFieldMasks[40]);
    }

    [Fact]
    public void Ambiguous_positive_duplicates_reject_migration_without_changing_original()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 50);
        vitals.Records[0].MagicInventory[1] = new NativeMagicStock(1, 20);
        Assert.Throws<InvalidDataException>(() => MagicSystemFixture.Modes(vitals, shared: true));
        Assert.Equal(50, vitals.Records[0].MagicInventory[0].Count);
        Assert.Equal(20, vitals.Records[0].MagicInventory[1].Count);
    }
}
