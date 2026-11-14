using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverApprovedStatusGroupsTests
{
    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void Original_compound_protection_applies_every_requested_operation_to_the_mixed_party(byte crisis)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        uint expected = assets.NativeActions.Single(action => action.Name == "Mighty Guard" && action.CrisisLevel == crisis).Statuses1 & Ff7GuestBattleStatuses.SupportedOwnedOriginBattle;
        using CrossoverCompositeActionTests.Fixture fixture = new("Mighty Guard", 3, crisis);

        CrossoverCompositeActionTests.Result result = fixture.Execute(queuedRoundTrip: true);

        Assert.Equal(new[] { 0, 1 }, result.Contacts.Select(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot).Order());
        Assert.All(result.Contacts, contact => Assert.Equal(expected, contact.HitDescription.PreparedContact!.Receipt.AddedOriginBattleStatuses));
        fixture.FollowWithFire();
    }
}
