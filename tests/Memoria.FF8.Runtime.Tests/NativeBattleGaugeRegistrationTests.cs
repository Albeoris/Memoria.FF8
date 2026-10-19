using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGaugeRegistrationTests
{
    [Theory]
    [InlineData(1u, 0, 0u, NativeBattleGaugeRegistrationKind.Menu, 9u)]
    [InlineData(0x11u, 0, 0u, NativeBattleGaugeRegistrationKind.DeferredScript, 0x219u)]
    [InlineData(1u, 0x20, 0u, NativeBattleGaugeRegistrationKind.ForcedCommand, 5u)]
    [InlineData(0x11u, 0x20, 0u, NativeBattleGaugeRegistrationKind.ForcedCommand, 0x15u)]
    [InlineData(0x11u, 0, 0x4000u, NativeBattleGaugeRegistrationKind.ForcedCommand, 0x15u)]
    [InlineData(0x11u, 0, 0x2000000u, NativeBattleGaugeRegistrationKind.ForcedCommand, 0x15u)]
    [InlineData(0x11u, 0x20, 0x2004000u, NativeBattleGaugeRegistrationKind.ForcedCommand, 0x15u)]
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

    [Fact]
    public void Ready_callback_uses_bank_one_zero_BYTE2_and_retains_unknown_BYTE3_and_zero_targets()
    {
        NativeBattleCommandBanks banks = NativeBattleCommandBanks.Initialize();
        (banks, NativeBattleCommandLocation location) = banks.EnqueueCallback(1, 3, 1);
        Assert.Equal((byte)1, location.Bank);
        Assert.Equal((byte)255, banks.Banks[0].Tail);
        NativeBattleCommandPacket packet = banks.Banks[1].Packets[2 * location.Slot];
        Assert.Equal((byte)3, packet.ReadByte(0));
        Assert.Equal((byte)255, packet.ReadByte(1));
        Assert.Equal((byte)0, packet.ReadByte(2));
        Assert.Throws<NotSupportedException>(() => packet.ReadByte(3));
        Assert.Equal((ushort)1, packet.ReadWord(4));
        Assert.Equal((ushort)0, packet.ReadWord(6));
        Assert.Equal((ushort)0, packet.ReadWord(8));
        Assert.Equal((ushort)0, packet.ReadWord(10));
        Assert.Equal(1, banks.CountActor(3));
        NativeBattleCommandBanks restored = JsonSerializer.Deserialize<NativeBattleCommandBanks>(JsonSerializer.Serialize(banks))!;
        Assert.Equal(JsonSerializer.Serialize(banks), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Reused_callback_packet_preserves_the_actual_retained_BYTE3_instead_of_clearing_it()
    {
        NativeBattleCommandBanks banks = NativeBattleCommandBanks.Initialize();
        (banks, NativeBattleCommandLocation location) = banks.Allocate(1);
        banks = banks.WriteCommand(location, 0, 3, 2, 0xA5, 0xB6, 7, [8]);
        banks = banks.Release(location);
        (banks, location) = banks.EnqueueCallback(1, 4, 1);
        NativeBattleCommandPacket packet = banks.Banks[1].Packets[2 * location.Slot];
        Assert.Equal((byte)0xB6, packet.ReadByte(3));
        Assert.Equal((byte)0, packet.ReadByte(2));
        Assert.Equal((byte)4, packet.ReadByte(0));
    }
}
