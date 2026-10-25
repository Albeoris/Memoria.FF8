using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect31BankTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Current_root_parity_selects_one_bank_and_rejects_crossing_into_its_neighbor(int parity)
    {
        NativeBattleEffect31Frame frame = Frame(parity);
        uint start = frame.State.ReadDword(CursorGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, start + 0xD000 - 36) };
        Assert.Equal(checked((int)(start + 0xD000 - 36 - NativeBattleEffect31Controller.CommonAddress)), NativeBattleEffect31PacketBank.Require(frame, 36));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect31PacketBank.Require(frame, 37));
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, parity == 0 ? start - 1 : start + 0xD000) };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect31PacketBank.Require(frame, 1));
    }

    [Fact]
    public void Rejected_attempt_still_requires_its_complete_retained_packet_footprint()
    {
        NativeBattleEffect31Frame frame = Frame(1);
        uint start = frame.State.ReadDword(CursorGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, start + 0xD000 - 51) };
        NativeBattleGfGeometryAttempt attempt = new(0, new byte[52].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 52).ToImmutableArray(), null);
        NativeBattleGfGeometryBatch batch = new([attempt], 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect31PacketBank.Require(frame, batch));
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, start + 0xD000 - 52) };
        Assert.Equal(checked((int)(start + 0xD000 - 52 - NativeBattleEffect31Controller.CommonAddress)), NativeBattleEffect31PacketBank.Require(frame, batch));
    }

    private static NativeBattleEffect31Frame Frame(int parity)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 0x64, 2);
        (pool, int slot) = pool.Append(RootCallback);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x5C, checked((ushort)parity))) };
        NativeBattleOriginalEffectState effect = new(31, 31, new(31, Entry, Initializer), new(3, 1, 1, 0, 0, 0), RootDescriptor) { Pools = [pool] };
        uint source = NativeBattleEffect31Controller.CommonAddress + 0x1000;
        NativeBattleEffect31State state = NativeBattleEffect31State.FromColdProcess().WriteDword(InitializerGlobal, source).WriteDword(CursorGlobal, checked(source + (parity == 0 ? 0xD000u : 0u)));
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), state);
    }
}
