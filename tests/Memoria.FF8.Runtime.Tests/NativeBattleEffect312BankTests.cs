using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect312BankTests
{
    [Fact]
    public void Rejected_partial_packet_stores_must_fit_the_selected_bank_even_without_allocation()
    {
        NativeBattleGfGeometryAttempt rejected = new(0, new byte[40].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 40).ToImmutableArray(), null);
        NativeBattleGfGeometryBatch batch = new([rejected], 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect312Controller.RequirePacketCapacity(Frame(0, 0xC000 - 8), batch));
        NativeBattleEffect312Controller.RequirePacketCapacity(Frame(0, 0xC000 - 40), batch);
    }

    [Fact]
    public void Strip_cursor_hole_is_reserved_even_when_every_pair_rejects()
    {
        NativeBattleGfGeometryBatch empty = new([], 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect312Controller.RequirePacketCapacity(Frame(0, 0xC000 - 35), empty, 36));
        NativeBattleEffect312Controller.RequirePacketCapacity(Frame(0, 0xC000 - 36), empty, 36);
    }

    [Fact]
    public void Crossing_into_the_other_bank_does_not_change_the_current_pump_parity()
    {
        NativeBattleEffect312Frame wrong = Frame(0, 0xC000);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect312Controller.RequirePacketCapacity(wrong, 1));
        NativeBattleEffect312Controller.RequirePacketCapacity(Frame(1, 0xC000), 1);
    }

    private static NativeBattleEffect312Frame Frame(ushort parity, uint offset)
    {
        const uint common = 0x20DFAB8;
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteWord(12, parity));
        NativeBattleOriginalEffectState effect = new(Effect, Effect - 1, new(Effect, Entry, Initializer), null, null) { Pools = [root] };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect312State.FromColdProcess().WriteDword(ArenaGlobal, common).WriteDword(CursorGlobal, common + offset));
    }
}
