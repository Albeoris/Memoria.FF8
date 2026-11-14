using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverRemainingCinematicTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Apocalypse", (byte)0)]
    [InlineData("Fire Breath", (byte)1)]
    [InlineData("Fire Breath", (byte)2)]
    [InlineData("Fire Breath", (byte)3)]
    [InlineData("Fire Breath", (byte)4)]
    [InlineData("Ice Strike", (byte)4)]
    [InlineData("Desperado", (byte)4)]
    [InlineData("Massive Anchor", (byte)4)]
    public void Original_cinematic_reaches_contacts_restores_source_and_releases_before_following_Fire(string name, byte crisis)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")), "Select both original game installations.");
        byte character = OriginalSourceCharacter(directory!, name, crisis);
        using CrossoverCompositeActionTests.Fixture fixture = new(name, character, crisis);
        CrossoverCompositeActionTests.Result result = fixture.Execute(queuedRoundTrip: true);
        int[] targets = result.Contacts.Select(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot).Distinct().Order().ToArray();
        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.InRange(target, 4, 9));
        Assert.True(result.VisualCallbacks > 0);
        fixture.FollowWithFire();
        output.WriteLine($"{name}, original character {character}, crisis {crisis}: {result.Contacts.Length} individually settled contacts, {result.VisualCallbacks} native visual callbacks, queued JSON continuation, source/resource restoration, original process cleanup and following Fire completed.");
    }

    internal static byte OriginalSourceCharacter(string directory, string name, byte crisis)
    {
        using BattleAssets assets = new(directory);
        BattleNativeAction source = Assert.Single(assets.NativeActions, action => action.Name == name && (action.Command == 2 || action.Command == 15 && action.CrisisLevel == crisis || action.CharacterId is >= 6 and <= 10 && NativeBattleLimitRules.GuestActionId(action.CharacterId.Value) == action.ActionId));
        return source.Command == 2 ? (byte)0 : checked((byte)source.CharacterId!.Value);
    }
}
