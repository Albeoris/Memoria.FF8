using System.Text.Json;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuardianPartyStatusTests
{
    [Fact]
    public void Cerberus_group_with_a_native_host_ally_is_rejected_without_publishing_an_unapproved_multicast_owner()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(guestCount: 1, guardianName: "Cerberus");
        fixture.Ready(0);
        ushort guardian = fixture.Guardian("Cerberus");
        string hostBefore = JsonSerializer.Serialize(fixture.Host.Capture());
        string stockBefore = JsonSerializer.Serialize(fixture.Providers[0].WorkingStock);
        string randomBefore = JsonSerializer.Serialize(fixture.Providers[0].CurrentCrtRandom);
        uint originBefore = fixture.Host.GetGuestOriginBattleStatuses(0);
        Ff7BattleCommandOption option = Assert.Single(fixture.Host.Commands(0), choice => choice.Command == Ff7BattleCommand.Magic && choice.Argument == guardian);
        Assert.NotNull(option.UnsupportedReason);
        Assert.NotNull(fixture.Host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, guardian, 1));
        Assert.False(fixture.Host.Submit(0, Ff7BattleCommand.Magic, guardian, 3));
        Assert.Equal(hostBefore, JsonSerializer.Serialize(fixture.Host.Capture()));
        Assert.Equal(stockBefore, JsonSerializer.Serialize(fixture.Providers[0].WorkingStock));
        Assert.Equal(randomBefore, JsonSerializer.Serialize(fixture.Providers[0].CurrentCrtRandom));
        Assert.Equal(originBefore, fixture.Host.GetGuestOriginBattleStatuses(0));
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
            Assert.Equal(new[] { 1, 2, 3 }, fixture.Providers[slot].MagicCastCounts(cure));
            Assert.Equal(new[] { 1, 2, 3 }, fixture.Providers[slot].MagicCastCounts(fire));
        }
        fixture.Ready(1);
        Assert.True(fixture.Host.SubmitGuestMagicSequence(1, cure, [1, 2, 1]));
        Assert.Equal(3, fixture.Complete(1, 3));
        Assert.Equal(cureBefore - 3, fixture.Stock(cure));
        Assert.Equal(fireBefore, fixture.Stock(fire));
        fixture.Ready(0);
        Assert.True(fixture.Host.SubmitGuestMagicSequence(0, fire, [1 << 4, 1 << 5]));
        Assert.Equal(2, fixture.Complete(0, 2));
        Assert.Equal(fireBefore - 2, fixture.Stock(fire));
        Assert.Empty(fixture.Host.Capture().GuestMagicSequences);
        Assert.All(fixture.Providers, provider => Assert.Null(provider.Invocation));
        Assert.Null(fixture.Host.Action);
        Assert.Equal(0x60000u, fixture.Host.GetGuestOriginBattleStatuses(0) & Ff7GuestBattleStatuses.OriginMulticast);
        Assert.Equal(0x60000u, fixture.Host.GetGuestOriginBattleStatuses(1) & Ff7GuestBattleStatuses.OriginMulticast);
    }
}
