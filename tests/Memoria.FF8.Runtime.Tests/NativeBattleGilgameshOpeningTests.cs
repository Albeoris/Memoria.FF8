using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshOpeningTests
{
    [Fact]
    public void Initializer_appends_five_tasks_consumes_exactly_four_CRT_and_preserves_every_unwritten_word()
    {
        NativeBattleEffectTaskPool pool = Pool();
        NativeCrtRandomState random = new(123);
        NativeCrtRandomState expectedRandom = random;
        ushort[] angles = new ushort[4];
        for (int index = 0; index < angles.Length; index++)
        {
            expectedRandom = expectedRandom.Draw(out ushort value);
            angles[index] = (ushort)(value % 4096);
        }
        NativeBattleGilgameshOpeningInitialization result = NativeBattleGilgameshOpening.Initialize592D00(pool, random);
        Assert.Equal(expectedRandom, result.Random);
        Assert.Equal(0, result.Pool.Head);
        Assert.Equal(4, result.Pool.Tail);
        Assert.Equal(new uint[] { 0x592E90, 0x592F90, 0x592F90, 0x5930C0, 0x5930C0 }, result.Pool.Slots.Take(5).Select(task => task.Callback).ToArray());
        ushort[][] expected =
        [
            [0, 0xA5A5, 0, 0xE4A8, 0, 0xA5A5, 0xA5A5, 0xA5A5, 0x2400, 0xA5A5, 0x3000, 0xA5A5],
            [0, 0, 0, 0xE4A8, 0, 0xA5A5, angles[0], 4, 0x2000, 0xA5A5, 0x3000, 0],
            [0, 1, 0, 0xE4A8, 0, 0xA5A5, angles[1], 8, 0x2000, 0xA5A5, 0x3000, 0x600],
            [0, 0, 0, 0xCD38, 0, 0xA5A5, angles[2], 4, 0x3400, 0xA5A5, 0xA5A5, 0x800],
            [0, 1, 0, 0xC950, 0, 0xA5A5, angles[3], 0xFFFC, 0x3000, 0xA5A5, 0xA5A5, 0x800]
        ];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleModulePoolTask task = result.Pool.Slots[index];
            Assert.Equal(expected[index], Enumerable.Range(0, 12).Select(word => task.ReadWord(12 + word * 2)).ToArray());
            Assert.Equal(index == 4 ? (int?)null : index + 1, task.Next);
        }
        Assert.Equal(JsonSerializer.Serialize(pool.Slots[5]), JsonSerializer.Serialize(result.Pool.Slots[5]));
        Assert.Equal((ushort)0xA5A5, pool.Slots[0].ReadWord(12));
    }

    [Fact]
    public void Mandatory_initializer_exhaustion_fails_without_fabricating_success()
    {
        NativeBattleEffectTaskPool pool = Pool();
        for (int index = 0; index < 96; index++)
        {
            (pool, _) = pool.Append(1);
        }
        Assert.Throws<InvalidOperationException>(() => NativeBattleGilgameshOpening.Initialize592D00(pool, new(1)));
    }

    [Theory]
    [InlineData(0x592E90u, 0, 144)]
    [InlineData(0x592F90u, 0, 72)]
    [InlineData(0x592F90u, 1, 48)]
    [InlineData(0x5930C0u, 0, 96)]
    [InlineData(0x5930C0u, 1, 96)]
    public void All_five_mesh_selections_draw_before_pause_and_terminal_counter(uint callback, ushort selector, int faces)
    {
        NativeBattleModulePoolTask task = Task(callback).WriteWord(14, selector).WriteWord(12, 67).WriteWord(0x18, 65535).WriteWord(0x1A, 4);
        NativeBattleGilgameshOpeningStep paused = NativeBattleGilgameshOpening.Step(task, Resources(), View(), Projection(), 0x201);
        Assert.Same(task, paused.UpdatedTask);
        Assert.Equal(0, paused.Result);
        Assert.Equal(faces, paused.Batch.Attempts.Length);
        NativeBattleGilgameshOpeningStep step = NativeBattleGilgameshOpening.Step(task, Resources(), View(), Projection(), 0);
        Assert.Equal(JsonSerializer.Serialize(paused.Batch), JsonSerializer.Serialize(step.Batch));
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)68, step.UpdatedTask.ReadWord(12));
        Assert.Equal(callback == 0x592E90 ? (ushort)65535 : (ushort)3, step.UpdatedTask.ReadWord(0x18));
    }

    [Theory]
    [InlineData(0x592E90u)]
    [InlineData(0x592F90u)]
    [InlineData(0x5930C0u)]
    public void Matrices_use_world_view_composition_signed_task_words_and_actual_scale_owners(uint callback)
    {
        NativeBattleModulePoolTask task = Task(callback).WriteWord(0x10, unchecked((ushort)-100)).WriteWord(0x12, 200).WriteWord(0x14, unchecked((ushort)-300)).WriteWord(0x18, 1024).WriteWord(0x1C, unchecked((ushort)-4096)).WriteWord(0x20, 2048);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, callback == 0x592E90 ? (short)0 : (short)1024, 0));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, -4096, callback == 0x5930C0 ? -4096 : 2048, -4096) with { X = -100, Y = 200, Z = -300 };
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGfMatrices.Compose56C2F0(View(), local)), JsonSerializer.Serialize(NativeBattleGilgameshOpening.Matrix(task, View())));
        if (callback == 0x5930C0)
        {
            Assert.Equal(JsonSerializer.Serialize(NativeBattleGilgameshOpening.Matrix(task, View())), JsonSerializer.Serialize(NativeBattleGilgameshOpening.Matrix(task.WriteWord(0x20, 12345), View())));
        }
    }

    [Theory]
    [InlineData(0x592E90u)]
    [InlineData(0x592F90u)]
    [InlineData(0x5930C0u)]
    public void Signed_counter_wrap_negative_angle_increment_and_json_next_step_are_exact(uint callback)
    {
        NativeBattleModulePoolTask task = Task(callback).WriteWord(12, 32767).WriteWord(0x18, 2).WriteWord(0x1A, 65532);
        NativeBattleGilgameshOpeningStep first = NativeBattleGilgameshOpening.Step(task, Resources(), View(), Projection(), 0);
        Assert.Equal((ushort)32768, first.UpdatedTask.ReadWord(12));
        Assert.Equal(callback == 0x592E90 ? (ushort)2 : (ushort)65534, first.UpdatedTask.ReadWord(0x18));
        Assert.Equal(0, first.Result);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(first.UpdatedTask))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGilgameshOpening.Step(first.UpdatedTask, Resources(), View(), Projection(), 0)), JsonSerializer.Serialize(NativeBattleGilgameshOpening.Step(restored, Resources(), View(), Projection(), 0)));
    }

    [Theory]
    [InlineData(0x592E90u, 0)]
    [InlineData(0x592F90u, 0)]
    [InlineData(0x592F90u, 65535)]
    [InlineData(0x5930C0u, 0)]
    [InlineData(0x5930C0u, 65535)]
    public void Dispatcher_uses_exact_mesh_flags_depth_shift_and_signed_fade(uint callback, ushort selector)
    {
        NativeBattleGilgameshOpeningResources resources = Resources();
        NativeBattleModulePoolTask task = Task(callback).WriteWord(14, selector).WriteWord(0x22, unchecked((ushort)-1));
        NativeBattleStaticMeshResource selected = callback == 0x592E90 ? resources.BaseMesh : callback == 0x592F90 ? selector == 0 ? resources.RingA : resources.RingB : selector == 0 ? resources.SpireA : resources.SpireB;
        bool baseTask = callback == 0x592E90;
        NativeBattleModuleMeshContext context = new(baseTask ? 0u : 0xC3u, 0, baseTask ? null : -1, null, null, null, 17, baseTask ? (byte)1 : (byte)2);
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200(selected.Bytes, 0, default, NativeBattleGilgameshOpening.Matrix(task, View()), Projection(), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(NativeBattleGilgameshOpening.Step(task, resources, View(), Projection(), 0).Batch));
    }
    private static NativeBattleEffectTaskPool Pool()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        return pool with { Slots = pool.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
    }

    private static NativeBattleModulePoolTask Task(uint callback) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(0x1C, 4096).WriteWord(0x20, 4096);
    private static NativeBattleActionMatrix View() => new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 1000, 160, 108);
    private static NativeBattleGilgameshOpeningResources Resources() => new(Mesh(145, [0, 0, 0, 0, 16, 128, 0, 0], 1), Mesh(96, [0, 0, 0, 48, 0, 0, 0, 24], 2), Mesh(72, [0, 0, 0, 48, 0, 0, 0, 0], 3), Mesh(97, [0, 0, 16, 80, 0, 0, 0, 0], 4), Mesh(97, [0, 0, 16, 80, 0, 0, 0, 0], 5));

    private static NativeBattleStaticMeshResource Mesh(int vertices, int[] counts, uint color)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = new byte[8 + vertices * 8 + 32 + counts.Select((count, group) => count * strides[group]).Sum()];
        int cursor = 8 + vertices * 8;
        BinaryPrimitives.WriteInt32LittleEndian(bytes, cursor);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), vertices);
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), counts[group]);
            cursor += 4;
            for (int face = 0; face < counts[group]; face++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), (group >= 6 ? 0x3C000000u : group == 4 || group == 5 ? 0x30000000u : 0x2C000000u) | color);
                cursor += strides[group];
            }
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
}

