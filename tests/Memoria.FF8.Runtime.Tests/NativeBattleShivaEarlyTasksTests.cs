using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaEarlyTasksTests
{
    [Fact]
    public void First_visual_executes_geometry_before_pause_and_spawns_four_children_without_clearing_suffixes()
    {
        NativeBattleShivaEarlyTaskFrame initial = Fixture();
        int geometryCalls = 0;
        NativeBattleShivaEarlyTaskStep step = NativeBattleShivaEarlyTasks.Advance5C1390(initial, 0, 0x201, (frame, count) =>
        {
            geometryCalls++;
            Assert.Equal(0, count);
            Assert.Equal(new NativeCrtRandomState(1), frame.Random);
            Assert.Single(frame.Pool.Slots, task => (task.Flags & 1) != 0);
            return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(42, new byte[] { 77 }) }, Geometry = frame.Geometry with { Current = new(11, 12, 13, 14) } };
        });
        Assert.Equal(1, geometryCalls);
        Assert.Equal(0, step.Result);
        Assert.Equal((ushort)0, step.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal((ushort)0, step.Frame.Pool.Slots[0].ReadWord(0x0E));
        Assert.Equal(new NativeCrtRandomState(2745024), step.Frame.Random);
        Assert.Equal(new byte[] { 77 }, step.Frame.Memory.Arena!.Read(42, 1));
        Assert.Equal(new NativeBattleShivaRetainedVector(11, unchecked((short)0xD8F1), 13, 14), step.Frame.Geometry.Current);
        Assert.Equal(5, step.Frame.Pool.Slots.Count(task => (task.Flags & 1) != 0));
        for (int index = 0; index < 4; index++)
        {
            NativeBattleModulePoolTask child = step.Frame.Pool.Slots[index + 1];
            Assert.Equal(0x5C16B0u, child.Callback);
            Assert.Equal((ushort)(1 << index), child.ReadWord(0x0E));
            Assert.Equal((ushort)(41 + index * 1024), child.ReadWord(0x18));
            Assert.Equal((ushort)60, child.ReadWord(0x1A));
            Assert.Equal((ushort)4096, child.ReadWord(0x1C));
            Assert.Equal((ushort)6144, child.ReadWord(0x20));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(0x16));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(0x1E));
        }
        int repeatedCalls = 0;
        NativeBattleShivaEarlyTaskStep repeated = NativeBattleShivaEarlyTasks.Advance5C1390(step.Frame, 0, 1, (frame, _) =>
        {
            repeatedCalls++;
            return frame;
        });
        Assert.Equal(1, repeatedCalls);
        Assert.Equal(9, repeated.Frame.Pool.Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal((ushort)0, repeated.Frame.Pool.Slots[0].ReadWord(0x0C));
    }

    [Theory]
    [InlineData(59, 80, 36, 60, 82, 2)]
    [InlineData(32767, 32767, 36, -32768, -32767, 0)]
    [InlineData(1, -3, -3, 2, -1, 0)]
    public void Counters_use_signed_completion_WORD_wrap_and_signed_geometry_limit(short counter, short extent, int requested, short afterCounter, short afterExtent, int result)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture();
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(0, frame.Pool.Slots[0].WriteWord(0x0C, unchecked((ushort)counter)).WriteWord(0x0E, unchecked((ushort)extent))) } };
        NativeBattleShivaEarlyTaskStep step = NativeBattleShivaEarlyTasks.Advance5C1390(frame, 0, 0, (current, count) =>
        {
            Assert.Equal(requested, count);
            return current;
        });
        Assert.Equal(result, step.Result);
        Assert.Equal(unchecked((ushort)afterCounter), step.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(unchecked((ushort)afterExtent), step.Frame.Pool.Slots[0].ReadWord(0x0E));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Burst_preserves_original_six_draw_order_signed_division_and_sparse_child_fields_through_JSON()
    {
        NativeBattleShivaEarlyTaskFrame source = Fixture();
        byte[] bytes = new byte[64];
        short[] row = [-12, 11, -5, 13, 3, -17, 3, 7];
        for (int index = 0; index < 32; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), row[index % 8]);
        }
        NativeBattleShivaEarlyTaskFrame result = NativeBattleShivaEarlyTasks.Spawn5C4820(source, bytes.ToImmutableArray());
        short[][] expected = [[1, -4, 2238, 190, -1, 2], [4, -3, 2386, 194, -1, -5], [5, -9, 1769, 101, -3, 3], [9, -7, 3719, 174, -1, 5]];
        for (int index = 0; index < expected.Length; index++)
        {
            NativeBattleModulePoolTask child = result.Pool.Slots[index + 1];
            int[] offsets = [0x0E, 0x12, 0x18, 0x1A, 0x1C, 0x20];
            Assert.Equal(0x5C4900u, child.Callback);
            for (int column = 0; column < offsets.Length; column++)
            {
                Assert.Equal(unchecked((ushort)expected[index][column]), child.ReadWord(offsets[column]));
            }
            Assert.Equal(child.ReadWord(0x1C), child.ReadWord(0x1E));
            Assert.Equal(unchecked((ushort)-17), child.ReadWord(0x22));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(0x16));
        }
        Assert.Equal(new NativeCrtRandomState(0x994439), result.Random);
        Assert.Equal(source.Geometry, result.Geometry);
        NativeBattleShivaEarlyTaskFrame restored = JsonSerializer.Deserialize<NativeBattleShivaEarlyTaskFrame>(JsonSerializer.Serialize(source))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleShivaEarlyTasks.Spawn5C4820(restored, bytes.ToImmutableArray())));
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaEarlyTasks.Spawn5C4820(source, []));
        Assert.Throws<ArgumentNullException>(() => NativeBattleShivaEarlyTasks.Advance5C1390(source, 0, 0, null!));
    }

    private static NativeBattleShivaEarlyTaskFrame Fixture()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        pool = pool with { Slots = pool.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
        (pool, int slot) = pool.Append(0x5C1390);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x0E, 0)) };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pool, new(1), new(new(0, 0, 0, 0), new(1, 2, 3, 4)));
    }
}
