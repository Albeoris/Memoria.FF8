namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDevour62ActorFadeTests
{
    [Theory]
    [InlineData(2048, 0xAB042132u)]
    [InlineData(-2048, 0xAB0A6196u)]
    public void Nonzero_fade_truncates_signed_products_and_preserves_code_and_alpha(short value, uint expected)
    {
        NativeBattleGraphicsModel model = new(3, 0x102) { Color40 = 0xAB998877, Alpha7 = 41 };
        NativeBattleGraphicsModel actual = NativeBattleDevour62ActorFade.Apply(model, value, 0xC0074164);
        Assert.Equal(expected, actual.Color40);
        Assert.Equal((byte)41, actual.Alpha7);
        Assert.Equal((ushort)0x902, actual.Flags0);
    }

    [Fact]
    public void Zero_fade_publishes_process_rgb_and_explicitly_clears_code_and_alpha()
    {
        NativeBattleGraphicsModel actual = NativeBattleDevour62ActorFade.Apply(new(3, 2), 0, 0xAB765432);
        Assert.Equal(0x765432u, actual.Color40);
        Assert.Equal((byte)0, actual.Alpha7);
        Assert.Equal((ushort)0x802, actual.Flags0);
    }

    [Fact]
    public void Disabled_actor_is_not_read_or_modified()
    {
        NativeBattleGraphicsModel model = new(6, 0x801);
        Assert.Same(model, NativeBattleDevour62ActorFade.Apply(model, 4096, 0x808080));
        Assert.Throws<NotSupportedException>(() => NativeBattleDevour62ActorFade.Apply(model with { Flags0 = 2 }, 4096, 0x808080));
    }

    [Theory]
    [InlineData(0xFFFF, 0, 0xF7FB)]
    [InlineData(0, 0xFFFF, 0x804)]
    [InlineData(0x1234, 0x5678, 0x1230)]
    public void Flag_restore_changes_only_the_two_native_mask_bits(ushort current, ushort saved, ushort expected)
    {
        Assert.Equal(expected, NativeBattleDevour62ActorFade.RestoreFlags(current, saved));
    }
}
