using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverRemainingMagicTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Holy", 1)]
    [InlineData("Holy", 4)]
    [InlineData("Dispel", 1)]
    [InlineData("Dispel", 4)]
    [InlineData("Drain", 1)]
    [InlineData("Drain", 4)]
    [InlineData("Zombie", 1)]
    [InlineData("Zombie", 4)]
    public void Original_magic_completes_on_actual_host_models_and_releases_before_the_next_action(string name, int target)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        var action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.Name == name);
        output.WriteLine($"Original action {name}: type {action.AttackType}, saved statuses {action.Statuses0:X4}, battle statuses {action.Statuses1:X8}.");
        using CrossoverCompositeActionTests.Fixture fixture = new(name, 0, 0);
        CrossoverCompositeActionTests.Result result = fixture.Execute(queuedRoundTrip: true, targetSlot: target);
        Assert.Contains(result.Contacts, contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot == target);
        Assert.True(result.VisualCallbacks > 0);
        fixture.FollowWithFire();
    }
}
