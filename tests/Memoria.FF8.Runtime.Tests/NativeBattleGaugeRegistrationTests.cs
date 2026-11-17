namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGaugeRegistrationTests
{
    [Theory]
    [InlineData(1u, 0, 0u, NativeBattleGaugeRegistrationKind.Menu, 9u)]
    [InlineData(0x11u, 0, 0u, NativeBattleGaugeRegistrationKind.DeferredScript, 0x219u)]
    [InlineData(1u, 0x20, 0u, NativeBattleGaugeRegistrationKind.ForcedCommand, 5u)]
    [InlineData(0x11u, 0x20, 0u, NativeBattleGaugeRegistrationKind.DeferredScript, 0x215u)]
    [InlineData(0x11u, 0, 0x4000u, NativeBattleGaugeRegistrationKind.DeferredScript, 0x215u)]
    [InlineData(0x11u, 0, 0x2000000u, NativeBattleGaugeRegistrationKind.DeferredScript, 0x215u)]
    [InlineData(0x11u, 0x20, 0x2004000u, NativeBattleGaugeRegistrationKind.DeferredScript, 0x215u)]
    [InlineData(0x11u, 0x10, 0x2000u, NativeBattleGaugeRegistrationKind.DeferredScript, 0x219u)]
    [InlineData(0x11u, 0x40, 0x4000000u, NativeBattleGaugeRegistrationKind.DeferredScript, 0x219u)]
    [InlineData(1u, 0, 0x4000u, NativeBattleGaugeRegistrationKind.ForcedCommand, 5u)]
    public void Full_gauge_distinguishes_menu_script_and_exact_compelled_status_branches(uint flags, ushort saved, uint battle, NativeBattleGaugeRegistrationKind kind, uint expected)
    {
        NativeBattleAtbState gauge = new(12000, 12000);
        NativeBattleGaugeRegistration result = NativeBattleGaugeRegistrations.Evaluate4842B0(gauge, flags, saved, battle);
        Assert.Equal(kind, result.Kind);
        Assert.Equal(expected, result.ActorFlags);
        Assert.Equal(12000, gauge.Current);
        Assert.Equal(NativeBattleGaugeRegistrationKind.None, NativeBattleGaugeRegistrations.Evaluate4842B0(gauge, result.ActorFlags, saved, battle).Kind);
    }

    [Theory]
    [InlineData(5u)]
    [InlineData(9u)]
    [InlineData(13u)]
    [InlineData(0x15u)]
    [InlineData(0x19u)]
    public void Flags_C_do_not_freeze_gauge_growth_but_do_reject_repeat_registration(uint flags)
    {
        NativeBattleAtbState before = new(12000, 11999);
        NativeBattleAtbState full = before.Advance(20, 10, flags, 0, 0);
        Assert.Equal(12000, full.Current);
        Assert.Equal(new NativeBattleGaugeRegistration(NativeBattleGaugeRegistrationKind.None, flags), NativeBattleGaugeRegistrations.Evaluate4842B0(full, flags, 0, 0));
    }

    [Theory]
    [InlineData(0u, 0, 0u)]
    [InlineData(0x81u, 0, 0u)]
    [InlineData(1u, 1, 0u)]
    [InlineData(1u, 4, 0u)]
    [InlineData(1u, 0, 1u)]
    [InlineData(1u, 0, 8u)]
    public void Existing_allocated_status_and_pause_guards_do_not_register_a_full_gauge(uint flags, ushort saved, uint battle)
    {
        Assert.Equal(new NativeBattleGaugeRegistration(NativeBattleGaugeRegistrationKind.None, flags), NativeBattleGaugeRegistrations.Evaluate4842B0(new(12000, 12000), flags, saved, battle));
    }

    [Fact]
    public void Below_maximum_does_not_register_and_completed_flags_allow_only_a_new_refill()
    {
        NativeBattleAtbState below = new(12000, 11999);
        Assert.Equal(NativeBattleGaugeRegistrationKind.None, NativeBattleGaugeRegistrations.Evaluate4842B0(below, 0x11, 0, 0).Kind);
        NativeBattleAtbState completed = below with { Current = 0 };
        Assert.Equal(NativeBattleGaugeRegistrationKind.None, NativeBattleGaugeRegistrations.Evaluate4842B0(completed, 0x11, 0, 0).Kind);
        Assert.Equal(0x219u, NativeBattleGaugeRegistrations.Evaluate4842B0(completed with { Current = 12000 }, 0x11, 0, 0).ActorFlags);
    }
}
