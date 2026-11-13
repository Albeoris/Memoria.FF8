using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGfSceneHostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void Original_Shockwave_Pulsar_uses_host_scene_geometry_and_releases_before_following_Fire(byte crisis)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Shockwave Pulsar", 3, crisis);
        CrossoverCompositeActionTests.Result result = fixture.Execute(queuedRoundTrip: true);
        int[] targets = result.Contacts.Select(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot).Distinct().Order().ToArray();
        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.InRange(target, 4, 9));
        Assert.Equal(fixture.NativeHitCount * targets.Length, result.Contacts.Length);
        fixture.FollowWithFire();
        output.WriteLine($"Shockwave Pulsar Crisis {crisis}: {result.Contacts.Length} native contacts over {targets.Length} host targets; {result.VisualCallbacks} visual callbacks; queued JSON continuation, process release and following Fire completed.");
    }

    [Theory]
    [InlineData("Carbuncle", false)]
    [InlineData("Quezacotl", false)]
    [InlineData("Cerberus", true)]
    public void Original_guardian_uses_host_scene_contacts_releases_native_process_and_allows_following_Fire(string name, bool soloGuestParty)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new(name, 0, 0, soloGuestParty: soloGuestParty);
        CrossoverCompositeActionTests.Result result = fixture.Execute(queuedRoundTrip: true);
        int[] targets = result.Contacts.Select(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot).Distinct().Order().ToArray();
        if (name == "Carbuncle")
        {
            Assert.Equal(new[] { 0, 1 }, targets);
        }
        else if (name == "Cerberus")
        {
            Assert.Equal(new[] { 0 }, targets);
        }
        else
        {
            Assert.NotEmpty(targets);
            Assert.All(targets, target => Assert.InRange(target, 4, 9));
        }
        Assert.Equal(fixture.NativeHitCount * targets.Length, result.Contacts.Length);
        Assert.All(result.Contacts, contact => Assert.IsType<Ff7BattleGuestPreparedContact>(contact.HitDescription.PreparedContact));
        fixture.FollowWithFire();
        output.WriteLine($"{name}: {result.Contacts.Length} native contacts over {targets.Length} actual host targets; {result.VisualCallbacks} visual callbacks; queued JSON continuation, process release and following Fire completed.");
    }
}
