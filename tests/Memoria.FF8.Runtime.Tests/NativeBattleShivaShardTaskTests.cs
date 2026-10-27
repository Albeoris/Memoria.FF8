using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaShardTaskTests
{
    [Fact]
    public void Static_phase_reads_only_delay_and_completion_uses_actual_packet_allocation()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(17);
        NativeBattleEffectArena arena = frame.Memory.Arena!.MarkUnknown(0x2E000, 170 * 32);
        for (int index = 0; index < 170; index++)
        {
            arena = PutWord(arena, 0x2E000 + index * 32 + 6, 10);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleShivaEarlyTaskFrame independent = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleShivaShardTaskStep visible = NativeBattleShivaShardTask.Advance5C2260(frame, 0, 0, Inputs(Mesh(false)));
        Assert.Equal(170, visible.Packets.Attempts.Length);
        Assert.True(visible.Packets.AllocationBytes > 0);
        Assert.Equal(0, visible.Result);
        Assert.Equal((ushort)18, visible.Frame.Pool.Slots[0].ReadWord(0x0C));
        NativeBattleShivaShardTaskStep culled = NativeBattleShivaShardTask.Advance5C2260(independent, 0, 0, Inputs(Mesh(true)));
        Assert.Equal(170, culled.Packets.Attempts.Length);
        Assert.Equal(0, culled.Packets.AllocationBytes);
        Assert.Equal(2, culled.Result);
        Assert.All(culled.Packets.Attempts, attempt => Assert.Equal(0, attempt.ByteOffsetRelative));
        Assert.Throws<NotSupportedException>(() => culled.Frame.Memory.Arena!.Read(0x2E00E, 2));
    }

    [Fact]
    public void Becoming_dead_still_draws_with_old_lifetime_scale_and_old_vertical_translation()
    {
        NativeBattleShivaEarlyTaskFrame frame = OneFragment(80, 47, 0);
        List<NativeBattleActionMatrix> matrices = [];
        NativeBattleShivaShardTaskInputs inputs = Inputs(Mesh(false)) with { ObserveGte = operation =>
        {
            if (operation.Matrix is { } matrix)
            {
                matrices.Add(matrix);
            }
        } };
        NativeBattleShivaShardTaskStep result = NativeBattleShivaShardTask.Advance5C2260(frame, 0, 0, inputs);
        NativeBattleActionMatrix drawn = Assert.Single(matrices);
        Assert.Equal(new short[] { -1920, 0, 0, 0, -1920, 0, 0, 0, -1920 }, drawn.Rotation.ToArray());
        Assert.Equal(10, drawn.X);
        Assert.Equal(30, drawn.Y);
        Assert.Equal(1030, drawn.Z);
        Assert.Single(result.Packets.Attempts);
        Assert.Equal(ushort.MaxValue, Word(result.Frame.Memory.Arena!, 0x2E00E));
        Assert.Equal((ushort)1, Word(result.Frame.Memory.Arena!, 0x2E00A));
        Assert.Equal(unchecked((ushort)-7), Word(result.Frame.Memory.Arena!, 0x2E012));
        Assert.Equal(frame.Random, result.Frame.Random);
        NativeBattleShivaShardTaskStep next = NativeBattleShivaShardTask.Advance5C2260(result.Frame, 0, 0, inputs);
        Assert.Empty(next.Packets.Attempts);
        Assert.Equal(2, next.Result);
    }

    [Fact]
    public void Rotation_uses_updated_angles_but_the_zero_lifetime_draw_keeps_its_original_scale()
    {
        NativeBattleShivaEarlyTaskFrame frame = OneFragment(19, 0, 1024);
        NativeBattleActionMatrix? drawn = null;
        NativeBattleShivaShardTaskInputs inputs = Inputs(Mesh(false)) with { ObserveGte = operation => drawn = operation.Matrix ?? drawn };
        NativeBattleShivaShardTaskStep result = NativeBattleShivaShardTask.Advance5C2260(frame, 0, 0, inputs);
        Assert.NotNull(drawn);
        Assert.Equal(new short[] { 4096, 0, 0, 0, 0, -4096, 0, 4096, 0 }, drawn.Rotation.ToArray());
        Assert.Equal(30, drawn.Y);
        Assert.Equal((ushort)1024, Word(result.Frame.Memory.Arena!, 0x2E018));
        Assert.Equal((ushort)1, Word(result.Frame.Memory.Arena!, 0x2E00E));
        Assert.Single(result.Packets.Attempts);
    }

    [Fact]
    public void Pause_draws_current_shards_without_record_updates_and_disk_equivalent_state_continues_independently()
    {
        NativeBattleShivaEarlyTaskFrame frame = OneFragment(19, 0, 1024);
        NativeBattleShivaShardTaskInputs inputs = Inputs(Mesh(false));
        NativeBattleShivaShardTaskStep paused = NativeBattleShivaShardTask.Advance5C2260(frame, 0, 0x201, inputs);
        Assert.Single(paused.Packets.Attempts);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(paused.Frame));
        Assert.Equal(0, paused.Result);
        NativeBattleShivaEarlyTaskFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShivaEarlyTaskFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleShivaShardTaskStep first = NativeBattleShivaShardTask.Advance5C2260(frame, 0, 0, inputs);
        NativeBattleShivaShardTaskStep second = NativeBattleShivaShardTask.Advance5C2260(restored, 0, 0, inputs);
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(second));
    }

    private static NativeBattleShivaEarlyTaskFrame OneFragment(ushort counter, short lifetime, short angleStep)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(counter);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 170; index++)
        {
            arena = PutWord(arena, 0x2E000 + index * 32 + 0x0E, -1);
        }
        arena = PutWord(arena, 0x2E000, 10);
        arena = PutWord(arena, 0x2E002, 20);
        arena = PutWord(arena, 0x2E004, 30);
        arena = PutWord(arena, 0x2E006, 0);
        arena = PutWord(arena, 0x2E00A, 10);
        arena = PutWord(arena, 0x2E00E, lifetime);
        arena = PutWord(arena, 0x2E012, -9);
        arena = PutWord(arena, 0x2E01A, angleStep);
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    private static NativeBattleShivaEarlyTaskFrame Fixture(ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        (pool, int slot) = pool.Append(0x5C2260);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, counter).WriteWord(0x1C, 4096).WriteWord(0x20, 4096)) });
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pool, new(1), new(new(0, 0, 0, 0), new(0, 0, 0, 0)));
    }

    private static NativeBattleShivaShardTaskInputs Inputs(NativeBattleStaticMeshResource mesh)
    {
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 1000 };
        return new(view, mesh, new(view, 216, 160, 108), 341, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleStaticMeshResource Mesh(bool degenerate)
    {
        byte[] source = new byte[0x12C8];
        BinaryPrimitives.WriteUInt32LittleEndian(source, 0x340);
        short[][] vertices = degenerate ? [[0, 0, 0], [0, 0, 0], [0, 0, 0]] : [[-100, -100, 0], [100, -100, 0], [-100, 100, 0]];
        for (int vertex = 0; vertex < vertices.Length; vertex++)
        {
            for (int coordinate = 0; coordinate < 3; coordinate++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8 + vertex * 8 + coordinate * 2), vertices[vertex][coordinate]);
            }
        }
        int cursor = 0x340;
        for (int group = 0; group < 8; group++)
        {
            int count = group == 2 ? 102 : group == 6 ? 68 : 0;
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(cursor), (uint)count);
            cursor += 4;
            for (int index = 0; index < count; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(cursor), 0x24808080);
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(cursor + 4), 0);
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(cursor + 6), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(cursor + 8), 4);
                cursor += group == 2 ? 20 : 28;
            }
        }
        return new(0, 0xD2F1C4, source.ToImmutableArray());
    }

    private static ushort Word(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(address, 2));

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int address, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(address, bytes);
    }
}
