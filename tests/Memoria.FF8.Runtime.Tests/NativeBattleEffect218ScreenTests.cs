using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218ScreenTests
{
    [Theory]
    [InlineData(-1, 193)]
    [InlineData(0, 0)]
    [InlineData(3, 191)]
    [InlineData(4, 255)]
    [InlineData(15, 255)]
    [InlineData(16, 255)]
    [InlineData(23, 32)]
    public void Opening_fade_keeps_native_signed_truncation_and_phase_boundaries(short age, int expected)
    {
        Assert.Equal(expected, OpeningIntensity632EB0(age));
    }

    [Fact]
    public void Screen_rectangle_precedes_its_draw_mode_in_physical_allocation_order()
    {
        NativeBattleModulePacketBatch batch = Screen632F50(511, -1, 384, 4096);
        Assert.Equal(24, batch.AllocationBytes);
        Assert.Equal(2, batch.Primitives.Length);
        Assert.Equal(0, batch.Primitives[0].ByteOffset);
        Assert.Equal(16, batch.Primitives[1].ByteOffset);
        Assert.Equal(new uint[] { 0x03000000, 0x6280FFFF, 0, 0x00D80140 }, batch.Primitives[0].Primitive.Words);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000220 }, batch.Primitives[1].Primitive.Words);
        Assert.All(batch.Primitives, packet =>
        {
            Assert.Equal(4113, packet.Primitive.Bucket);
            Assert.Equal((ushort)1, packet.Flags20);
            Assert.All(packet.Primitive.PhysicalWriteMask!.Value, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        });
    }

    [Fact]
    public void Opening_draws_before_pause_and_the_final_age_increment()
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        (pool, int slot) = pool.Append(0x632EB0);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 23)) };
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = ImmutableArray.Create(pool) };
        NativeBattleEffect218Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect218State.FromColdProcess());
        uint address = pool.BankAddress + (uint)(slot * pool.Stride);
        List<NativeBattleModulePacketBatch> draws = [];
        NativeBattleEffect218Frame Deliver(NativeBattleEffect218Frame current, NativeBattleModulePacketBatch batch)
        {
            draws.Add(batch);
            return current;
        }
        var paused = AdvanceOpening632EB0(frame, address, 0x200, Deliver);
        Assert.Single(draws);
        Assert.Equal(0, paused.Result);
        Assert.Equal((ushort)23, Read(paused.Frame, address).ReadWord(12));
        var finished = AdvanceOpening632EB0(paused.Frame, address, 0, Deliver);
        Assert.Equal(2, draws.Count);
        Assert.Equal(0x62202020u, draws[1].Primitives[0].Primitive.Words[1]);
        Assert.Equal(2, finished.Result);
        Assert.Equal((ushort)24, Read(finished.Frame, address).ReadWord(12));
    }
}
