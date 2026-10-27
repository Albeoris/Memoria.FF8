using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePandemona291VortexTests
{
    private const uint Task = 0x20DFAB8;

    [Fact]
    public void Reset_clears_only_lifetime_words_and_publishes_first_free_record()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x42C, 128 * 24);
        arena = NativeBattlePandemona291Vortex.Reset6F1520(arena, Task);
        for (int index = 0; index < 128; index++)
        {
            Assert.Equal((ushort)0, Word(arena, 0x432 + index * 24));
            Assert.Throws<NotSupportedException>(() => arena.Read(0x42C + index * 24, 6));
        }
        Assert.Equal(Task + 0x42C, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(0x102C, 4)));
    }

    [Fact]
    public void Velocity_births_preserve_unknown_positions_and_consume_twenty_four_draws()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x102C, BitConverter.GetBytes(Task + 0x42C));
        for (int index = 0; index < 4; index++)
        {
            arena = arena.MarkUnknown(0x42C + index * 24, 6);
        }
        NativeBattlePandemona291TrailAllocation result = NativeBattlePandemona291Vortex.Spawn6EDC66(arena, new(1), Task, Trig());
        Assert.Equal(0x00994439u, result.Random.Seed);
        Assert.Equal((ushort)12, Word(result.Arena, 0x432));
        Assert.Equal((ushort)0, Word(result.Arena, 0x434));
        Assert.Equal((ushort)512, Word(result.Arena, 0x438));
        Assert.Equal((ushort)4096, Word(result.Arena, 0x43A));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x42C, 6));
    }

    [Fact]
    public void Motion_overwrites_unknown_positions_and_expiration_skips_unknown_velocity()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x42C, 6).Write(0x432, BitConverter.GetBytes((ushort)2));
        arena = arena.Write(0x434, BitConverter.GetBytes((short)12)).Write(0x436, BitConverter.GetBytes((short)-20)).Write(0x438, BitConverter.GetBytes((short)50));
        arena = arena.Write(0x440, new byte[] { 255, 2 });
        arena = arena.Write(0x44A, BitConverter.GetBytes((ushort)1)).MarkUnknown(0x444, 6).MarkUnknown(0x44C, 16);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096);
        List<ImmutableArray<byte>> inputs = [];
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena))!;
        NativeBattleEffectArena result = NativeBattlePandemona291Vortex.Advance6F1A30(arena, Task, matrix, 0, 0, Trig(), (input, _) => inputs.Add(input));
        Assert.Single(inputs);
        Assert.Equal((ushort)112, Word(result, 0x42C));
        Assert.Equal((ushort)180, Word(result, 0x42E));
        Assert.Equal((ushort)350, Word(result, 0x430));
        Assert.Equal(unchecked((ushort)-361), Word(result, 0x436));
        Assert.Equal(unchecked((ushort)-4), Word(result, 0x43C));
        Assert.Equal((ushort)8, Word(result, 0x43E));
        Assert.Equal(Task + 0x444, BinaryPrimitives.ReadUInt32LittleEndian(result.Read(0x102C, 4)));
        Assert.Throws<NotSupportedException>(() => result.Read(0x44C, 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattlePandemona291Vortex.Advance6F1A30(restored, Task, matrix, 0, 0, Trig())));
    }

    [Fact]
    public void Floor_morph_uses_retained_signed_ir0_and_zero_count_does_not_read_target()
    {
        byte[] source = new byte[16];
        byte[] target = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(8), 65530);
        BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(8), 10);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(10), 100);
        BinaryPrimitives.WriteInt16LittleEndian(target.AsSpan(10), 104);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(12), -100);
        BinaryPrimitives.WriteInt16LittleEndian(target.AsSpan(12), -104);
        Assert.Equal(new NativeBattleVector(2, 102, -102), NativeBattlePandemona291Vortex.Morph6F2D20(source.ToImmutableArray(), target.ToImmutableArray(), 2048).Single());
        Assert.Equal(new NativeBattleVector(-14, 98, -98), NativeBattlePandemona291Vortex.Morph6F2D20(source.ToImmutableArray(), target.ToImmutableArray(), -2048).Single());
        Assert.Empty(NativeBattlePandemona291Vortex.Morph6F2D20(new byte[8].ToImmutableArray(), default, 2048));
    }

    [Fact]
    public void Vortex_layer_operands_use_unsigned_growth_wrapping_phase_and_current_center()
    {
        ImmutableArray<NativeBattlePandemona291LayerRequest> requests = NativeBattlePandemona291Vortex.Layers(1900, 83);
        Assert.Equal(3, requests.Length);
        Assert.All(requests, request =>
        {
            Assert.Equal(2935, request.Morph);
            Assert.Equal(1900, request.Center);
            Assert.Equal(0, request.Fade);
            Assert.Equal(8192, request.VerticalScale);
        });
        Assert.Equal((byte)96, requests[0].TexturePhase);
        Assert.Equal((byte)184, requests[2].TexturePhase);
        Assert.Equal(9600, requests[0].HorizontalScale);
        Assert.Equal(15104, requests[1].HorizontalScale);
    }

    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));

    private static ImmutableArray<byte> Trig()
    {
        byte[] bytes = new byte[0x4000];
        for (int index = 0; index < 4096; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 4 + 2), 4096);
        }
        return bytes.ToImmutableArray();
    }
}
