using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect173Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect173LowerTests
{
    [Fact]
    public void Orbit_births_consume_eight_random_values_each_and_retain_unwritten_word()
    {
        NativeBattleEffect173Frame frame = Frame();
        frame = frame with { State = frame.State.WriteView(0x23972E0, frame.State.ReadView(0x23972E0, 24).MarkUnknown(20, 2), 24) };
        NativeCrtRandomState random = frame.Random;
        ushort[] values = new ushort[24];
        for (int index = 0; index < values.Length; index++)
        {
            random = random.Draw(out values[index]);
        }
        NativeBattleEffect173Frame result = NativeBattleEffect173Lower.BirthOrbit(frame);
        Assert.Equal(random, result.Random);
        NativeBattleEffectArena row = result.State.ReadView(0x23972E0, 24);
        Assert.Equal(1u, U(row, 0));
        Assert.Equal(values[0] % 1536 + 1536, S(row, 6));
        Assert.Equal(values[4] % 4096, S(row, 14));
        Assert.Equal(values[5] % 1200 + 400, S(row, 16));
        Assert.Equal(S(row, 16) / (values[6] % 14 + 18), S(row, 18));
        Assert.Equal(values[7] % 50 + 25, S(row, 22));
        Assert.DoesNotContain(true, row.ReadSnapshot(20, 2).Known);
        Assert.Equal(1u, result.State.ReadDword(0x23972E0 + 48));
    }

    [Fact]
    public void Cloud_births_keep_the_extra_Y_random_draw_and_partial_anchor_W()
    {
        NativeBattleEffect173Frame frame = Frame();
        NativeBattleEffectArena anchor = NativeBattleEffectArena.Clear().Write(6, [0xA5]).MarkUnknown(7, 1);
        frame = frame with { State = frame.State.WriteView(0x2398AA0, anchor, 8).WriteDword(0x2398A84, 4096) };
        NativeCrtRandomState random = frame.Random;
        ushort[] values = new ushort[18];
        for (int index = 0; index < values.Length; index++)
        {
            random = random.Draw(out values[index]);
        }
        NativeBattleEffect173Frame result = NativeBattleEffect173Lower.BirthCloud(frame, 7);
        Assert.Equal(random, result.Random);
        NativeBattleEffectArena row = result.State.ReadView(0x23972E0, 24);
        Assert.Equal(2u, U(row, 0));
        Assert.Equal(values[2] % 200 - 28, S(row, 10));
        Assert.Equal(values[4] % 150 + 290 + values[6] % 120 + values[7] % 20 - 10, S(row, 18));
        Assert.Equal(0xA5, row.Read(14, 1)[0]);
        Assert.False(row.ReadSnapshot(15, 1).Known[0]);
    }

    [Fact]
    public void Ray_births_keep_height_fixed_and_consume_four_random_values_each()
    {
        NativeBattleEffect173Frame frame = Frame();
        frame = frame with { State = frame.State.WriteWord(0x2398A92, unchecked((ushort)-500)) };
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 28; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 36, 1).Slots[0].WriteWord(16, 0).WriteWord(20, 0);
        NativeBattleEffect173Frame result = NativeBattleEffect173Lower.BirthRays(frame, task);
        Assert.Equal(expected, result.Random);
        for (int index = 0; index < 7; index++)
        {
            NativeBattleEffectArena row = result.State.ReadView(0x23972E0 + (uint)(index * 24), 24);
            Assert.Equal(4u, U(row, 0));
            Assert.Equal((short)-500, S(row, 10));
            Assert.Equal((short)0, S(row, 18));
            Assert.InRange(S(row, 14), (short)30, (short)99);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Full_shared_record_bank_stops_before_any_random_consumption(int kind)
    {
        NativeBattleEffect173Frame frame = Frame();
        for (int index = 0; index < 100; index++)
        {
            frame = frame with { State = frame.State.WriteDword(0x23972E0 + (uint)(index * 24), 7) };
        }
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 36, 1).Slots[0];
        NativeBattleEffect173Frame result = kind switch
        {
            1 => NativeBattleEffect173Lower.BirthOrbit(frame),
            2 => NativeBattleEffect173Lower.BirthCloud(frame, 0),
            _ => NativeBattleEffect173Lower.BirthRays(frame, task)
        };
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.State, result.State);
    }

    [Theory]
    [InlineData(0u, 2)]
    [InlineData(0x201u, 3)]
    public void Static_positive_delay_precedes_all_allocation_and_geometry_reads(uint flags, int delay)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2397C40, 0x2397C50, 36, 100);
        (pool, _) = pool.Append(0x5D7E70);
        pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(12, 0).WriteWord(14, 3));
        NativeBattleEffect173Frame frame = Frame() with { Effect = Frame().Effect with { Pools = [pool] } };
        NativeBattleEffect173Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => flags, null!, null!, null!, _ => throw new InvalidOperationException("Delay must not read the view."), null!, null!, null!, (_, _) => throw new InvalidOperationException("Delay must not draw."));
        NativeBattleEffect173Step result = NativeBattleEffect173Lower.Advance(frame, 0x2397C50, null!, inputs);
        Assert.Equal(delay, Read(result.Frame, 0x2397C50).ReadWord(14));
        Assert.Equal(0, Read(result.Frame, 0x2397C50).ReadWord(12));
        Assert.Equal(0, result.Result);
        Assert.Null(result.Frame.Staging);
    }

    private static NativeBattleEffect173Frame Frame() => new(NativeBattleEffect173Mesh172Tests.Memory(), new(173, 172, new(173, 0x5D74B0, 0x5D7490), null, null) { Pools = [] }, new(1234), NativeBattleEffect173State.FromColdProcess());
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
