using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect76Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect76Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect76ParticleTests
{
    [Theory]
    [InlineData(0, 100, 6)]
    [InlineData(1, 100, 5)]
    [InlineData(2, 200, 6)]
    [InlineData(3, 100, 7)]
    [InlineData(4, 200, 10)]
    public void One_available_slot_consumes_exact_birth_random_and_preserves_unwritten_words(int kind, int capacity, int draws)
    {
        NativeBattleEffect76Frame frame = Frame();
        uint bank = kind is 0 or 1 or 3 ? FirstRecords : SecondRecords;
        for (int index = 1; index < capacity; index++)
        {
            frame = WriteBytes(frame, bank + (uint)(index * 32), Long(NativeBattleEffectArena.Clear(), 0, 16), 4);
        }
        frame = WriteBytes(frame, bank, Word(Word(NativeBattleEffectArena.Clear(), 14, 0x5A6B), 0x1E, 0x6B7C), 32);
        NativeCrtRandomState random = frame.Random;
        for (int draw = 0; draw < draws; draw++)
        {
            random = random.Draw(out _);
        }
        (NativeBattleEffect76Frame result, _) = NativeBattleEffect76Particles.Birth(frame, NativeBattleEffectArena.Clear().MarkUnknown(0, 152), 0, kind);
        Assert.Equal(random, result.Random);
        NativeBattleEffectArena row = ReadBytes(result, bank, 32);
        Assert.Equal(kind == 1 ? 2u : kind == 3 ? 4u : 1u, Dword(row, 0));
        Assert.Equal(0, Short(row, 4));
        if (kind != 3)
        {
            Assert.Equal(unchecked((short)0x5A6B), Short(row, 14));
        }
        if (kind < 4)
        {
            Assert.Equal(unchecked((short)0x6B7C), Short(row, 0x1E));
        }
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(4, 200)]
    public void Full_bank_stops_birth_without_consuming_random_or_rewriting_retained_rows(int kind, int capacity)
    {
        NativeBattleEffect76Frame frame = Frame();
        uint bank = kind == 0 ? FirstRecords : SecondRecords;
        for (int index = 0; index < capacity; index++)
        {
            frame = WriteBytes(frame, bank + (uint)(index * 32), Long(NativeBattleEffectArena.Clear(), 0, 32), 4);
        }
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 152);
        (NativeBattleEffect76Frame result, NativeBattleEffectArena retained) = NativeBattleEffect76Particles.Birth(frame, temporary, 0, kind);
        Assert.Equal(frame.Random, result.Random);
        Assert.Same(temporary, retained);
        Assert.Equal(ReadBytes(frame, bank, 32).Read(0, 32), ReadBytes(result, bank, 32).Read(0, 32));
    }

    [Fact]
    public void Accelerating_velocity_uses_signed_word_SAR_and_moves_before_retirement()
    {
        NativeBattleEffectArena row = PutVector(PutVector(Word(Long(NativeBattleEffectArena.Clear(), 0, 1), 4, 23), 8, 100, 200, 300), 0x10, -9, 9, -1);
        (row, bool alive) = NativeBattleEffect76Particles.Update(row, 0);
        Assert.True(alive);
        Assert.Equal(new NativeBattleVector(91, 209, 299), NativeBattleEffect76Lower.Vector(row, 8));
        Assert.Equal(new NativeBattleVector(-11, 10, -2), NativeBattleEffect76Lower.Vector(row, 0x10));
        Assert.Equal(24, Short(row, 4));
        NativeBattleEffectArena before = row;
        (row, alive) = NativeBattleEffect76Particles.Update(row, 0);
        Assert.False(alive);
        Assert.Equal(0u, Dword(row, 0));
        Assert.Equal(before.Read(4, 28), row.Read(4, 28));
    }

    [Fact]
    public void Delayed_ring_counts_alive_without_age_or_motion_and_uses_new_age_for_growth()
    {
        NativeBattleEffectArena row = Word(Word(Word(Word(Long(NativeBattleEffectArena.Clear(), 0, 4), 6, 1), 0x12, 100), 0x16, -7), 4, 6);
        (row, bool alive) = NativeBattleEffect76Particles.Update(row, 3);
        Assert.True(alive);
        Assert.Equal(0, Short(row, 6));
        Assert.Equal(6, Short(row, 4));
        (row, alive) = NativeBattleEffect76Particles.Update(row, 3);
        Assert.Equal(7, Short(row, 4));
        Assert.Equal(93, Short(row, 0x12));
        Assert.Equal(-6, Short(row, 0x16));
        (row, alive) = NativeBattleEffect76Particles.Update(row, 3);
        Assert.True(alive);
        Assert.Equal(8, Short(row, 4));
        Assert.Equal(93, Short(row, 0x12));
    }

    [Fact]
    public void Debris_keeps_distinct_horizontal_and_vertical_drag_and_accelerates_signed_spins()
    {
        NativeBattleEffectArena row = PutVector(PutVector(Long(NativeBattleEffectArena.Clear(), 0, 1), 0x10, -9, -9, -9), 8, 0, 0, 0);
        row = Word(Word(Word(Word(row, 0x18, 10), 0x1A, -17), 0x1C, 20), 0x1E, 17);
        (row, bool alive) = NativeBattleEffect76Particles.Update(row, 4);
        Assert.True(alive);
        Assert.Equal(new NativeBattleVector(-7, -10, -7), NativeBattleEffect76Lower.Vector(row, 0x10));
        Assert.Equal(-7, Short(row, 0x18));
        Assert.Equal(-19, Short(row, 0x1A));
        Assert.Equal(37, Short(row, 0x1C));
        Assert.Equal(18, Short(row, 0x1E));
    }

    [Theory]
    [InlineData(0, 15, 0x33, 0)]
    [InlineData(0, 16, 0xF3, 0)]
    [InlineData(1, 0, 0xF3, 4096)]
    [InlineData(1, 8, 0x33, 0)]
    [InlineData(3, 54, 0xF3, 0)]
    [InlineData(4, 3, 0xF3, 1024)]
    public void Cue_writes_only_the_native_reached_fade_field(int kind, int age, int flags, int cue)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(12, 4);
        context = NativeBattleEffect76Particles.Cue(context, checked((short)age), kind);
        Assert.Equal((uint)flags, Dword(context, 0x1C));
        if (flags == 0x33)
        {
            Assert.DoesNotContain(true, context.ReadSnapshot(12, 4).Known);
        }
        else
        {
            Assert.Equal(cue, unchecked((int)Dword(context, 12)));
        }
    }

    internal static NativeBattleEffect76Frame Frame() => new(NativeBattleEffectMemory.FromColdProcess(), new(76, 75, new(76, 0x61F9A0, 0x61F980), null, null) { Pools = [] }, new(123), NativeBattleEffect76State.FromColdProcess());
}
