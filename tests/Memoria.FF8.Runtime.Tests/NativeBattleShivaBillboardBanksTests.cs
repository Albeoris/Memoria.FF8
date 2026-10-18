using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaBillboardBanksTests
{
    [Fact]
    public void Warm_billboard_rotation_preserves_four_offdiagonals_and_raw_padding_while_real_sprite_packets_use_the_explicit_matrix()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(1, 23);
        frame = frame with { Memory = frame.Memory with { Arena = Record(frame.Memory.Arena!, 1, 0) } };
        byte[] original = frame.Memory.Shiva!.BillboardMatrix22BD0E8.ToArray();
        int draws = 0;
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        NativeBattleShivaEarlyTaskStep result = Advance(frame, 1, 0x201, view, (current, draw) =>
        {
            draws++;
            Assert.Equal((ushort)8, draw.Sprite.Phase);
            Assert.Equal((ushort)0x208, draw.Sprite.SpriteFlags);
            Assert.Equal(new short[] { 512, 0, 17, 19, 512, 0, 31, 37, 512 }, draw.Matrix.Rotation.ToArray());
            Assert.Equal(0, draw.Matrix.X);
            Assert.Equal(0, draw.Matrix.Y);
            Assert.Equal(4032, draw.Matrix.Z);
            NativeBattleModulePacketBatch packets = NativeBattleShivaBillboardBanks.Emit571C80(draw, new(view, 216, 160, 108), 0);
            Assert.NotEmpty(packets.Primitives);
            Assert.True(packets.AllocationBytes > 0);
            return current;
        });
        Assert.Equal(1, draws);
        byte[] retained = result.Frame.Memory.Shiva!.BillboardMatrix22BD0E8.ToArray();
        Assert.Equal(original.AsSpan(0, 20).ToArray(), retained.AsSpan(0, 20).ToArray());
        Assert.Equal(4032, BinaryPrimitives.ReadInt32LittleEndian(retained.AsSpan(28)));
        Assert.Equal((ushort)0, Word(result.Frame.Memory.Arena!, 0x2C004));
        Assert.Equal((ushort)23, result.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Theory]
    [InlineData((ushort)8, false)]
    [InlineData((ushort)0x208, true)]
    public void Sprite_packet_adapter_consumes_actual_camera_roll_unless_the_native_flag_disables_it(ushort flags, bool same)
    {
        NativeBattleModuleResource resource = Sprite(0xD20E10);
        NativeBattleModuleDraw sprite = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(0), SpriteFlags = flags };
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
        NativeBattleShivaLateSpriteDraw draw = new(sprite, matrix);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleModulePacketBatch first = NativeBattleShivaBillboardBanks.Emit571C80(draw, projection, 0);
        NativeBattleModulePacketBatch rotated = NativeBattleShivaBillboardBanks.Emit571C80(draw, projection, 1024);
        Assert.NotEmpty(first.Primitives);
        Assert.Equal(same, JsonSerializer.Serialize(first) == JsonSerializer.Serialize(rotated));
    }

    [Fact]
    public void Viewpoint_MAC_keeps_full_DWORD_translation_and_clouds_replace_all_rotation_without_touching_the_retained_matrix()
    {
        NativeBattleShivaEarlyTaskFrame stream = Fixture(1, 23);
        NativeBattleEffectArena arena = Record(stream.Memory.Arena!, 1, 0);
        arena = PutWord(arena, 0x2C008, 30000);
        stream = stream with { Memory = stream.Memory with { Arena = arena } };
        _ = Advance(stream, 1, 0x201, NativeBattleCuragaMatrices.Identity8DD770() with { X = 100000, Z = 4096 }, (current, draw) =>
        {
            Assert.True(draw.Matrix.X > 120000);
            return current;
        });
        NativeBattleShivaEarlyTaskFrame cloud = Fixture(3, 27);
        cloud = cloud with { Memory = cloud.Memory with { Arena = Record(cloud.Memory.Arena!, 4, 0) } };
        NativeBattleShivaEarlyTaskStep result = Advance(cloud, 3, 0x201, NativeBattleCuragaMatrices.Identity8DD770() with { Z = 8000 }, (current, draw) =>
        {
            Assert.Equal(new short[] { 512, 0, 0, 0, 512, 0, 0, 0, 512 }, draw.Matrix.Rotation.ToArray());
            Assert.Equal((ushort)0, draw.Sprite.SpriteFlags);
            return current;
        });
        Assert.Equal(cloud.Memory.Shiva!.BillboardMatrix22BD0E8.ToArray(), result.Frame.Memory.Shiva!.BillboardMatrix22BD0E8.ToArray());
        Assert.Equal(JsonSerializer.Serialize(cloud), JsonSerializer.Serialize(result.Frame));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Bank_updates_draw_before_mutation_and_preserve_their_distinct_velocity_rules(int kind)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(kind, 42);
        NativeBattleEffectArena arena = Record(frame.Memory.Arena!, Flag(kind), 0).MarkUnknown(0x2C001, 3);
        arena = PutWord(arena, 0x2C008, 800);
        arena = PutWord(arena, 0x2C00A, 100);
        arena = PutWord(arena, 0x2C00C, 0);
        arena = PutWord(arena, 0x2C010, kind == 0 ? 10 : -9);
        arena = PutWord(arena, 0x2C012, kind == 0 ? -7 : -1);
        arena = PutWord(arena, 0x2C014, kind == 0 ? -9 : -17);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleShivaEarlyTaskStep result = Advance(frame, kind, 0, NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 }, (current, _) =>
        {
            Assert.Equal((ushort)0, Word(current.Memory.Arena!, 0x2C004));
            Assert.Equal((ushort)800, Word(current.Memory.Arena!, 0x2C008));
            return current;
        });
        NativeBattleEffectArena after = result.Frame.Memory.Arena!;
        Assert.Equal((ushort)1, Word(after, 0x2C004));
        Assert.Throws<NotSupportedException>(() => after.Read(0x2C001, 3));
        if (kind == 0)
        {
            Assert.Equal((ushort)790, Word(after, 0x2C008));
            Assert.Equal((ushort)107, Word(after, 0x2C00A));
            Assert.Equal(unchecked((ushort)-9), Word(after, 0x2C00C));
            Assert.Equal(unchecked((ushort)-11), Word(after, 0x2C014));
        }
        else if (kind == 3)
        {
            Assert.Equal((ushort)800, Word(after, 0x2C008));
            Assert.Equal((ushort)100, Word(after, 0x2C00A));
            Assert.Equal((ushort)512, Word(after, 0x2C006));
        }
        else
        {
            Assert.Equal((ushort)791, Word(after, 0x2C008));
            Assert.Equal((ushort)99, Word(after, 0x2C00A));
            Assert.Equal(unchecked((ushort)-17), Word(after, 0x2C00C));
            Assert.Equal(kind == 1 ? unchecked((ushort)-8) : unchecked((ushort)-7), Word(after, 0x2C010));
            Assert.Equal((ushort)0, Word(after, 0x2C012));
            Assert.Equal(kind == 1 ? unchecked((ushort)-15) : unchecked((ushort)-19), Word(after, 0x2C014));
        }
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(0, result.Result);
    }

    [Theory]
    [InlineData(0, 4, 0x00994439u)]
    [InlineData(1, 3, 0x00994439u)]
    [InlineData(2, 5, 0xA6A6B409u)]
    [InlineData(3, 3, 0x6DF109FDu)]
    public void Fresh_spawns_have_exact_random_budgets_and_are_excluded_from_current_completion(int kind, int records, uint seed)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(kind, 3);
        NativeBattleShivaEarlyTaskStep result = Advance(frame, kind, 0, NativeBattleCuragaMatrices.Identity8DD770(), (_, _) => throw new InvalidOperationException("New records cannot draw in the spawn pass."));
        Assert.Equal(new NativeCrtRandomState(seed), result.Frame.Random);
        Assert.Equal(records, Enumerable.Range(0, 150).Count(index => Dword(result.Frame.Memory.Arena!, 0x2C000 + index * 24) != 0));
        Assert.Equal(2, result.Result);
        NativeBattleShivaEarlyTaskFrame restored = JsonSerializer.Deserialize<NativeBattleShivaEarlyTaskFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleShivaEarlyTaskStep independent = Advance(restored, kind, 0, NativeBattleCuragaMatrices.Identity8DD770(), null!);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(independent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Exhausted_bank_consumes_no_random_and_terminal_sprite_increments_phase_before_clearing(int kind)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(kind, 3);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 150; index++)
        {
            arena = PutDword(arena, 0x2C000 + index * 24, 16);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleShivaEarlyTaskStep full = Advance(frame, kind, 0, NativeBattleCuragaMatrices.Identity8DD770(), null!);
        Assert.Equal(frame.Random, full.Frame.Random);
        frame = Fixture(kind, 42);
        frame = frame with { Memory = frame.Memory with { Arena = Record(frame.Memory.Arena!, Flag(kind), 15) } };
        NativeBattleShivaEarlyTaskStep expired = Advance(frame, kind, 0, NativeBattleCuragaMatrices.Identity8DD770(), (current, _) => current);
        Assert.Equal((ushort)16, Word(expired.Frame.Memory.Arena!, 0x2C004));
        Assert.Equal(0u, Dword(expired.Frame.Memory.Arena!, 0x2C000));
        Assert.Equal(2, expired.Result);
    }

    private static NativeBattleShivaEarlyTaskStep Advance(NativeBattleShivaEarlyTaskFrame frame, int kind, uint flags, NativeBattleActionMatrix view, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaLateSpriteDraw, NativeBattleShivaEarlyTaskFrame> draw)
    {
        NativeBattleModuleResource sprite = Sprite(kind < 2 ? 0xD20E10u : kind == 2 ? 0xD20AD0u : 0xD208B0u);
        return kind switch
        {
            0 => NativeBattleShivaBillboardBanks.Advance5C62F0(frame, 0, flags, view, sprite, draw),
            1 => NativeBattleShivaBillboardBanks.Advance5C6BB0(frame, 0, flags, view, sprite, draw),
            2 => NativeBattleShivaBillboardBanks.Advance5C71B0(frame, 0, flags, view, sprite, draw),
            _ => NativeBattleShivaBillboardBanks.Advance5C7CA0(frame, 0, flags, view, sprite, draw)
        };
    }

    private static byte Flag(int kind) => kind switch { 0 => 8, 1 => 1, 2 => 2, _ => 4 };

    private static NativeBattleShivaEarlyTaskFrame Fixture(int kind, ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        uint callback = kind switch { 0 => 0x5C62F0u, 1 => 0x5C6BB0u, 2 => 0x5C71B0u, _ => 0x5C7CA0u };
        (pool, int slot) = pool.Append(callback);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, counter)) };
        byte[] matrix = new byte[32];
        short[] rotation = [11, 13, 17, 19, 23, 29, 31, 37, 41];
        for (int index = 0; index < rotation.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(index * 2), rotation[index]);
        }
        matrix[18] = 0xAA;
        matrix[19] = 0xBB;
        NativeBattleShivaProcess process = NativeBattleShivaProcess.FromColdProcess() with { BillboardMatrix22BD0E8 = matrix.ToImmutableArray() };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Shiva = process }, pool, new(1), new(new(0, 0, 0, 0), new(0, 0, 0, 0)));
    }

    private static NativeBattleEffectArena Record(NativeBattleEffectArena arena, uint flag, ushort phase)
    {
        arena = PutDword(arena, 0x2C000, flag);
        arena = PutWord(arena, 0x2C004, phase);
        return PutWord(arena, 0x2C006, 512);
    }

    private static NativeBattleModuleResource Sprite(uint address)
    {
        byte[] bytes = new byte[68];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int index = 0; index < 16; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * index), 44);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), -1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 1);
        bytes[48] = 2;
        bytes[49] = 128;
        bytes[50] = 2;
        bytes[51] = 0x2C;
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }

    private static ushort Word(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(address, 2));
    private static uint Dword(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(address, 4));

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int address, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(address, bytes);
    }

    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(address, bytes);
    }
}
