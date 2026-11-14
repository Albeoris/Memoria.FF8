using System.Text.Json;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuardianPartyStatusTests
{
    [Fact]
    public void Cerberus_group_publishes_the_same_source_multicast_owner_on_guest_and_native_host_ally()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(guestCount: 1, guardianName: "Cerberus");
        string stockBefore = JsonSerializer.Serialize(fixture.Providers[0].WorkingStock);
        fixture.CastGuardian("Cerberus");
        Assert.Equal(stockBefore, JsonSerializer.Serialize(fixture.Providers[0].WorkingStock));
        Assert.Equal(Ff7GuestBattleStatuses.OriginMulticast, fixture.Host.GetOriginBattleStatuses(0) & Ff7GuestBattleStatuses.OriginMulticast);
        Assert.Equal(Ff7GuestBattleStatuses.OriginMulticast, fixture.Host.GetOriginBattleStatuses(1) & Ff7GuestBattleStatuses.OriginMulticast);
        fixture.RestoreQueuedBoundary();
        Assert.Equal(Ff7GuestBattleStatuses.OriginMulticast, fixture.Host.GetOriginBattleStatuses(1) & Ff7GuestBattleStatuses.OriginMulticast);
        Assert.Null(fixture.Providers[0].Invocation);
        Assert.Null(fixture.Host.Action);
    }

    [Fact]
    public void Original_Cerberus_applies_group_multicast_to_both_guests_and_each_can_finish_selected_ordinary_magic()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(additionalSpells: ["Fire"], guardianName: "Cerberus");
        ushort guardian = fixture.Guardian("Cerberus");
        Assert.NotNull(fixture.Host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, guardian, 4));
        ushort cure = fixture.Spell("Cure");
        ushort fire = fixture.Spell("Fire");
        int cureBefore = fixture.Stock(cure);
        int fireBefore = fixture.Stock(fire);
        string sourceBefore = JsonSerializer.Serialize(fixture.Providers[0].WorkingStock);
        fixture.CastGuardian("Cerberus");
        Assert.Equal(sourceBefore, JsonSerializer.Serialize(fixture.Providers[0].WorkingStock));
        for (int slot = 0; slot < 2; slot++)
        {
            Assert.Equal(0x60000u, fixture.Host.GetGuestOriginBattleStatuses(slot) & Ff7GuestBattleStatuses.OriginMulticast);
            Assert.Equal(new[] { 3 }, fixture.Providers[slot].MagicCastCounts(cure));
            Assert.Equal(new[] { 3 }, fixture.Providers[slot].MagicCastCounts(fire));
        }
        fixture.Ready(1);
        Assert.True(fixture.Host.SubmitGuestMagicSequence(1, cure, [1, 2, 1]));
        Assert.Equal(3, fixture.Complete(1, 3, inspectPrepared: hit => fixture.AssertPreparedDamage(1, hit, 75, healing: true)));
        Assert.Equal(cureBefore - 3, fixture.Stock(cure));
        Assert.Equal(fireBefore, fixture.Stock(fire));
        fixture.Ready(0);
        Assert.True(fixture.Host.SubmitGuestMagicSequence(0, fire, [2, 1, 2]));
        Assert.Equal(3, fixture.Complete(0, 3, inspectPrepared: hit => fixture.AssertPreparedDamage(0, hit, 75, healing: false)));
        Assert.Equal(fireBefore - 3, fixture.Stock(fire));
        Assert.Empty(fixture.Host.Capture().GuestMagicSequences);
        Assert.All(fixture.Providers, provider => Assert.Null(provider.Invocation));
        Assert.Null(fixture.Host.Action);
        Assert.Equal(0x60000u, fixture.Host.GetGuestOriginBattleStatuses(0) & Ff7GuestBattleStatuses.OriginMulticast);
        Assert.Equal(0x60000u, fixture.Host.GetGuestOriginBattleStatuses(1) & Ff7GuestBattleStatuses.OriginMulticast);
    }
}
