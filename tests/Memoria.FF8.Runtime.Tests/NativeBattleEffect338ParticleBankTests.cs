using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect338ParticleBankTests
{
    [Fact]
    public void Reset_only_clears_lifetimes_and_preserves_unknown_and_retained_payloads()
    {
        NativeBattleEffect338ParticleBank bank = NativeBattleEffect338ParticleBank.FromUnknown().WriteDword(0, 0x137130C).WriteWord(4, 32768).Reset6EC0C0();
        Assert.Equal(0, bank.NextOffset);
        Assert.Equal(0x137130Cu, bank.ReadDword(0));
        Assert.Equal((ushort)32768, bank.ReadWord(4));
        Assert.Throws<NotSupportedException>(() => bank.ReadWord(12));
        for (int row = 0; row < 64; row++)
        {
            Assert.Equal((ushort)0, bank.ReadWord(row * 24 + 10));
        }
    }

    [Fact]
    public void Allocation_prefers_retained_cursor_then_scans_from_first_physical_slot()
    {
        NativeBattleEffect338ParticleBank bank = NativeBattleEffect338ParticleBank.FromColdProcess().Reset6EC0C0() with { NextOffset = 5 * 24 };
        var first = bank.Allocate6EC980();
        Assert.Equal(5 * 24, first.Offset);
        Assert.Equal(6 * 24, first.Bank.NextOffset);
        bank = first.Bank.WriteWord(6 * 24 + 10, 12).WriteWord(10, 1);
        var scanned = bank.Allocate6EC980();
        Assert.Equal(24, scanned.Offset);
        Assert.Equal(48, scanned.Bank.NextOffset);
        Assert.Equal((ushort)0, scanned.Bank.ReadWord(24 + 10));
    }

    [Fact]
    public void Last_slot_wraps_and_full_bank_failure_does_not_move_cursor()
    {
        NativeBattleEffect338ParticleBank bank = NativeBattleEffect338ParticleBank.FromColdProcess().Reset6EC0C0() with { NextOffset = 63 * 24 };
        var last = bank.Allocate6EC980();
        Assert.Equal(63 * 24, last.Offset);
        Assert.Equal(0, last.Bank.NextOffset);
        for (int row = 0; row < 64; row++)
        {
            bank = bank.WriteWord(row * 24 + 10, 1);
        }
        var full = bank.Allocate6EC980();
        Assert.Null(full.Offset);
        Assert.Equal(bank, full.Bank);
    }

    [Fact]
    public void Physics_wraps_WORDs_and_applies_Z_acceleration_only_to_position()
    {
        NativeBattleEffect338ParticleBank bank = NativeBattleEffect338ParticleBank.FromColdProcess().Reset6EC0C0().WriteWord(4, 32767).WriteWord(6, 0).WriteWord(8, 65535).WriteWord(10, 1).WriteWord(12, 2).WriteWord(14, 65535).WriteWord(16, 5).WriteByte(20, 255).WriteByte(21, 128).WriteByte(22, 127);
        bank = JsonSerializer.Deserialize<NativeBattleEffect338ParticleBank>(JsonSerializer.Serialize(bank))!;
        bank = bank.Advance6EC9D0();
        Assert.Equal((ushort)32769, bank.ReadWord(4));
        Assert.Equal((ushort)65535, bank.ReadWord(6));
        Assert.Equal((ushort)512, bank.ReadWord(8));
        Assert.Equal((ushort)65534, bank.ReadWord(12));
        Assert.Equal((ushort)65023, bank.ReadWord(14));
        Assert.Equal((ushort)5, bank.ReadWord(16));
        Assert.Equal((ushort)0, bank.ReadWord(10));
        Assert.Equal(bank, bank.Advance6EC9D0());
    }

    [Fact]
    public void Cursor_requires_executed_reset_and_aligned_physical_row()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect338ParticleBank.FromColdProcess().Allocate6EC980());
        Assert.Throws<InvalidDataException>(() => (NativeBattleEffect338ParticleBank.FromColdProcess() with { NextOffset = 1 }).Validate());
    }
}
