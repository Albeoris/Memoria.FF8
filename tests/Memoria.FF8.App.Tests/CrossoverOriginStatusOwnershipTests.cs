using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverOriginStatusOwnershipTests
{
    [Theory]
    [InlineData("Aura", Ff7GuestBattleStatuses.OriginAura, 1)]
    [InlineData("Aura", Ff7GuestBattleStatuses.OriginAura, 4)]
    [InlineData("Float", Ff7GuestBattleStatuses.OriginFloat, 1)]
    [InlineData("Float", Ff7GuestBattleStatuses.OriginFloat, 4)]
    [InlineData("Double", 0x20000u, 1)]
    [InlineData("Double", 0x20000u, 4)]
    [InlineData("Triple", 0x40000u, 1)]
    [InlineData("Triple", 0x40000u, 4)]
    public void Original_status_contact_on_native_party_and_enemy_keeps_a_distinct_restorable_source_owner(string spell, uint status, int slot)
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(guestCount: 1, additionalSpells: [spell]);
        fixture.Cast(0, spell, checked((ushort)(1 << slot)));
        Assert.Equal(status, fixture.Host.GetOriginBattleStatuses(slot) & status);
        Ff7BattleSnapshot before = fixture.Host.Capture();
        Ff7BattleGuestOriginStatus owner = Assert.Single(before.GuestOriginStatuses!, row => row.HostSlot == slot);
        Assert.Equal(status, owner.BattleStatuses & status);
        if ((status & (Ff7GuestBattleStatuses.OriginAura | Ff7GuestBattleStatuses.OriginFloat)) != 0)
        {
            Assert.NotNull(owner.Timers);
            int bit = System.Numerics.BitOperations.TrailingZeroCount(status);
            Assert.True(owner.Timers[bit] > 0);
        }
        fixture.RestoreQueuedBoundary();
        Assert.Equal(status, fixture.Host.GetOriginBattleStatuses(slot) & status);
        Assert.Equal(JsonSerializer.Serialize(before.GuestOriginStatuses), JsonSerializer.Serialize(fixture.Host.Capture().GuestOriginStatuses));
        Assert.Equal(before.Actors.Single(actor => actor.Actor.Slot == slot).Actor.Statuses, fixture.Host.Actors.Single(actor => actor.Actor.Slot == slot).Actor.Statuses);
    }

    [Fact]
    public void Every_original_Rapture_and_Degenerator_crisis_record_keeps_its_removal_operation_unavailable()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVIII installation.");
        using BattleAssets assets = new(directory!);
        foreach (string name in new[] { "Rapture", "Degenerator" })
        {
            BattleNativeAction[] originals = assets.NativeActions.Where(action => action.Name == name).ToArray();
            Assert.NotEmpty(originals);
            Assert.All(originals, action =>
            {
                Assert.NotEqual(0u, action.Statuses1 & 0x10000u);
                Assert.NotNull(CrossoverBattleAbilityRules.UnsupportedReason(action));
            });
        }
    }

}
