using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325RingPuffTests
{
    [Fact]
    public void Thirty_two_births_alternate_sprite_countdown_and_consume_exact_three_CRT_draws_before_same_callback_drift()
    {
        NativeBattleDiablos325Frame source = Fixture();
        var preserved = NativeBattleStorageSnapshots.Capture(source);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleDiablos325Frame born = NativeBattleDiablos325Puffs.SpawnRing6558A4(source);
        Assert.Equal(new NativeCrtRandomState(0x542236E1), born.Random);
        Assert.Equal((byte)3, born.Memory.Diablos325!.Mode51F0);
        Assert.Equal(0x20DFAB8u + 0x4E5C, born.Memory.Diablos325!.ParticleCursor51F4);
        Assert.Equal(new byte[] { 0xC0, 0x69, 0xFD, 0, 0x21, 2, 0x22, 0xE0, 0xE4, 6, 2, 78, 8, 0, 0xF8, 16 }, born.Memory.Arena!.Read(0x4C5C, 16));
        for (int index = 0; index < 32; index++)
        {
            byte[] record = born.Memory.Arena.Read(0x4C5C + index * 16, 16);
            Assert.Equal((index & 1) == 0 ? 0xFD69C0u : 0xFD6814u, BinaryPrimitives.ReadUInt32LittleEndian(record));
            Assert.Equal((byte)16, record[15]);
        }
        NativeBattleDiablos325Frame moved = NativeBattleDiablos325Puffs.Drift658CE0(born);
        Assert.Equal(new byte[] { 0xC0, 0x69, 0xFD, 0, 0x41, 2, 0x22, 0xE0, 0xC4, 6, 2, 78, 8, 0, 0xF8, 15 }, moved.Memory.Arena!.Read(0x4C5C, 16));
        Assert.Equal(born.Random, moved.Random);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    [Fact]
    public void One_last_slot_consumes_only_three_draws_and_wraps_cursor_while_full_bank_needs_no_random_owner()
    {
        NativeBattleDiablos325Frame frame = Fixture();
        NativeBattleEffectArenaWriter writer = new(frame.Memory.Arena!);
        for (int index = 0; index < 160; index++)
        {
            writer.Write(0x4C5C + index * 16 + 15, [1]);
        }
        frame = frame with { Memory = frame.Memory with { Arena = writer.Freeze() }, Random = null };
        string full = NativeBattleTestSnapshots.Json(frame);
        Assert.Equal(full, NativeBattleTestSnapshots.Json(NativeBattleDiablos325Puffs.SpawnRing6558A4(frame)));
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x564C + 15, [0]) }, Random = new(1) };
        NativeBattleDiablos325Frame result = NativeBattleDiablos325Puffs.SpawnRing6558A4(frame);
        Assert.Equal(new NativeCrtRandomState(0x18BE873A), result.Random);
        Assert.Equal(0x20DFAB8u + 0x4C5C, result.Memory.Diablos325!.ParticleCursor51F4);
        Assert.Equal(0xFD69C0u, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.Arena!.Read(0x564C, 4)));
        Assert.Equal((byte)16, result.Memory.Arena.Read(0x564C + 15, 1)[0]);
    }

    [Fact]
    public void Last_life_tick_still_moves_signed_velocities_with_WORD_wrap_and_does_not_consume_unknown_shade_or_inactive_records()
    {
        NativeBattleDiablos325Frame frame = Fixture();
        byte[] record = new byte[16];
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(4), -32760);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(6), 32760);
        record[12] = 0x80;
        record[13] = 0x7F;
        record[14] = 0xFF;
        record[15] = 1;
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x4C5C, record).MarkUnknown(0x4C5C + 10, 2).MarkUnknown(0x4C5C + 16, 15);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        var preserved = NativeBattleStorageSnapshots.Capture(frame);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleDiablos325Frame result = NativeBattleDiablos325Puffs.Drift658CE0(frame);
        byte[] position = result.Memory.Arena!.Read(0x4C5C + 4, 6);
        Assert.Equal((short)32264, BinaryPrimitives.ReadInt16LittleEndian(position));
        Assert.Equal((short)-32268, BinaryPrimitives.ReadInt16LittleEndian(position.AsSpan(2)));
        Assert.Equal((short)-4, BinaryPrimitives.ReadInt16LittleEndian(position.AsSpan(4)));
        Assert.Equal((byte)0, result.Memory.Arena.Read(0x4C5C + 15, 1)[0]);
        Assert.Equal(new byte[] { 0x80, 0x7F, 0xFF }, result.Memory.Arena.Read(0x4C5C + 12, 3));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(0x4C5C + 10, 2));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(0x4C5C + 16, 15));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    private static NativeBattleDiablos325Frame Fixture()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = NativeBattleDiablos325Puffs.Reset6545B0(frame);
        return frame with { Random = new(1), Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { FarZ51DC = 1900, CenterZ51D4 = -1234 } } };
    }
}
