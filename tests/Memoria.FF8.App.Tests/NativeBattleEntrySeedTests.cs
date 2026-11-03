using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEntrySeedTests
{
    [Theory]
    [InlineData(1u, 2745024u, 41)]
    [InlineData(2745024u, 3357800067u, 35)]
    public void Default_seed_uses_low_byte_of_one_retained_CRT_draw(uint before, uint after, byte expected)
    {
        NativeCrtRandomState original = new(before);
        (byte seed, NativeCrtRandomState? random) = GameWorld.PrepareNativeBattleSeed(null, original);

        Assert.Equal(expected, seed);
        Assert.Equal(new NativeCrtRandomState(after), random);
        Assert.Equal(new NativeCrtRandomState(before), original);
    }

    [Fact]
    public void Independent_JSON_restore_selects_the_same_next_seed_without_replaying_entry()
    {
        NativeCrtRandomState initial = NativeCrtRandomState.FromColdProcess();
        var (_, after) = GameWorld.PrepareNativeBattleSeed(null, initial);
        NativeCrtRandomState restored = JsonSerializer.Deserialize<NativeCrtRandomState>(JsonSerializer.Serialize(after))!;

        Assert.Equal(GameWorld.PrepareNativeBattleSeed(null, after), GameWorld.PrepareNativeBattleSeed(null, restored));
        Assert.Equal((byte)35, GameWorld.PrepareNativeBattleSeed(null, restored).Seed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void Explicit_diagnostic_seed_preserves_retained_or_unknown_CRT_owner(byte supplied)
    {
        NativeCrtRandomState original = new(12345);
        Assert.Equal((supplied, original), GameWorld.PrepareNativeBattleSeed(supplied, original));
        Assert.Equal((supplied, (NativeCrtRandomState?)null), GameWorld.PrepareNativeBattleSeed(supplied, null));
    }

    [Fact]
    public void Default_seed_rejects_unknown_CRT_owner()
    {
        Assert.Throws<NotSupportedException>(() => GameWorld.PrepareNativeBattleSeed(null, null));
    }
}
