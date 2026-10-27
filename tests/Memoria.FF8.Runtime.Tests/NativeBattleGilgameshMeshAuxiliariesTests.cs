using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshMeshAuxiliariesTests
{
    [Fact]
    public void Rising_initializer_uses_one_CRT_and_four_quadrants_without_touching_other_words()
    {
        NativeCrtRandomState initial = new(123);
        NativeCrtRandomState expectedRandom = initial.Draw(out ushort value);
        NativeBattleGilgameshMeshAuxiliaryInitialization result = NativeBattleGilgameshMeshAuxiliaries.Initialize593550(Pool(), initial);
        Assert.Equal(expectedRandom, result.Random);
        Assert.Equal(3, result.Pool.Tail);
        for (int index = 0; index < 4; index++)
        {
            int angle = value % 4096 + index * 1024;
            NativeBattleModulePoolTask task = result.Pool.Slots[index];
            Assert.Equal(0x593600u, task.Callback);
            Assert.Equal(new ushort[] { 0, 0xA5A5, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) * 1000 >> 12)), unchecked((ushort)-15000), unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) * 1000 >> 12)), 0xA5A5, unchecked((ushort)angle), 0xA5A5, 1024, 0xA5A5, 0xA5A5, 90 }, Words(task));
        }
        Assert.Equal((ushort)0xA5A5, result.Pool.Slots[4].ReadWord(12));
    }

    [Fact]
    public void Delayed_initializer_uses_each_authenticated_signed_row_and_exactly_nine_CRT_draws()
    {
        NativeBattleGilgameshMeshAuxiliaryResources resources = Resources();
        resources = resources with { Rows = resources.Rows.SetItem(0, new(14, -550, -13, 3072)) };
        NativeCrtRandomState random = new(123);
        NativeBattleGilgameshMeshAuxiliaryInitialization result = NativeBattleGilgameshMeshAuxiliaries.Initialize594160(Pool(), random, resources);
        for (int index = 0; index < 3; index++)
        {
            NativeBattleGilgameshMeshAuxiliaryRow row = resources.Rows[index];
            random = random.Draw(out ushort angle);
            random = random.Draw(out ushort angularVelocity);
            random = random.Draw(out ushort yScale);
            int velocity = row.ScaleSource / 8;
            NativeBattleModulePoolTask task = result.Pool.Slots[index];
            Assert.Equal(0x594210u, task.Callback);
            Assert.Equal(new ushort[] { 0, unchecked((ushort)row.Delay), 0, unchecked((ushort)row.Y), 0, 0xA5A5, (ushort)(angle % 4096), (ushort)(angularVelocity % 60 + 20), unchecked((ushort)(2 * velocity)), unchecked((ushort)velocity), unchecked((ushort)(yScale % 1536 + row.YScaleBase)), 0xA5A5 }, Words(task));
        }
        Assert.Equal(random, result.Random);
        Assert.Equal(2, result.Pool.Tail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Both_initializers_fail_on_mandatory_pool_exhaustion(bool delayed)
    {
        NativeBattleEffectTaskPool pool = Pool();
        for (int index = 0; index < 98; index++)
        {
            (pool, _) = pool.Append(1);
        }
        Assert.Throws<InvalidOperationException>(() => delayed ? NativeBattleGilgameshMeshAuxiliaries.Initialize594160(pool, new(1), Resources()) : NativeBattleGilgameshMeshAuxiliaries.Initialize593550(pool, new(1)));
    }

    [Theory]
    [InlineData(0x593600u, -1)]
    [InlineData(0x593600u, 0)]
    [InlineData(0x593600u, 5)]
    [InlineData(0x593600u, 6)]
    [InlineData(0x593600u, 29)]
    [InlineData(0x594210u, -1)]
    [InlineData(0x594210u, 0)]
    [InlineData(0x594210u, 3)]
    [InlineData(0x594210u, 4)]
    [InlineData(0x594210u, 5)]
    [InlineData(0x594210u, 6)]
    [InlineData(0x594210u, 13)]
    [InlineData(0x594210u, 14)]
    public void Finite_meshes_use_exact_fade_boundaries_and_draw_before_pause_and_physics(uint callback, short counter)
    {
        NativeBattleGilgameshMeshAuxiliaryResources resources = Resources();
        NativeBattleModulePoolTask task = Task(callback).WriteWord(12, unchecked((ushort)counter)).WriteWord(26, 4).WriteWord(30, 17).WriteWord(34, 17);
        bool delayed = callback == 0x594210;
        int? fade = delayed ? counter < 4 ? (4 - counter) << 10 : counter >= 6 ? (counter - 6) << 9 : null : counter < 6 ? 4096 - 682 * counter : null;
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200((delayed ? resources.DelayedMesh : resources.RisingMesh).Bytes, 0, default, NativeBattleGilgameshMeshAuxiliaries.Matrix(task, View()), Projection(), new(fade is null ? 0x33u : 0xF3u, 0, fade, null, null, null, 17, 2), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGilgameshMeshAuxiliaryStep paused = Run(task, 0x201);
        NativeBattleGilgameshMeshAuxiliaryStep current = Run(task);
        Assert.Same(task, paused.UpdatedTask);
        Assert.Equal(0, paused.Result);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(paused.Batch));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(current.Batch));
        Assert.Equal(delayed ? 96 : 10, current.Batch.Attempts.Length);
        Assert.Equal(counter + 1 >= (delayed ? 14 : 30) ? 2 : 0, current.Result);
    }

    [Theory]
    [InlineData(1u, 2)]
    [InlineData(0x200u, 2)]
    [InlineData(0u, 1)]
    public void Delayed_mesh_positive_signed_delay_waits_without_drawing(uint flags, ushort expected)
    {
        NativeBattleModulePoolTask task = Task(0x594210).WriteWord(14, 2);
        NativeBattleGilgameshMeshAuxiliaryStep result = Run(task, flags);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(expected, result.UpdatedTask.ReadWord(14));
        Assert.Equal((ushort)0, result.UpdatedTask.ReadWord(12));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Rising_physics_uses_SAR_scale_growth_and_trunc0_velocity_growth_with_WORD_wrap()
    {
        NativeBattleModulePoolTask task = Task(0x593600).WriteWord(18, 32767).WriteWord(28, unchecked((ushort)-1)).WriteWord(34, 13);
        NativeBattleModulePoolTask next = Run(task).UpdatedTask;
        Assert.Equal(unchecked((ushort)(32767 + 13)), next.ReadWord(18));
        Assert.Equal(unchecked((ushort)-2), next.ReadWord(28));
        Assert.Equal((ushort)14, next.ReadWord(34));
        Assert.Equal(unchecked((ushort)-14), Run(task.WriteWord(34, unchecked((ushort)-13))).UpdatedTask.ReadWord(34));
        Assert.Equal(unchecked((ushort)-11), Run(task.WriteWord(34, unchecked((ushort)-11))).UpdatedTask.ReadWord(34));
    }

    [Fact]
    public void Delayed_physics_adds_old_velocity_then_damps_with_trunc0_and_wraps_angle()
    {
        NativeBattleModulePoolTask task = Task(0x594210).WriteWord(24, 65535).WriteWord(26, 4).WriteWord(28, 32767).WriteWord(30, 13);
        NativeBattleModulePoolTask next = Run(task).UpdatedTask;
        Assert.Equal((ushort)3, next.ReadWord(24));
        Assert.Equal((ushort)32780, next.ReadWord(28));
        Assert.Equal((ushort)12, next.ReadWord(30));
        Assert.Equal(unchecked((ushort)-12), Run(task.WriteWord(30, unchecked((ushort)-13))).UpdatedTask.ReadWord(30));
        Assert.Equal(task.ReadWord(18), next.ReadWord(18));
        Assert.Equal(task.ReadWord(32), next.ReadWord(32));
    }

    [Theory]
    [InlineData(0x593600u)]
    [InlineData(0x594210u)]
    public void Signed_matrix_uses_shared_world_view_composition_and_json_counter_wrap_continues_identically(uint callback)
    {
        NativeBattleModulePoolTask task = Task(callback).WriteWord(12, 32767).WriteWord(16, unchecked((ushort)-100)).WriteWord(18, 200).WriteWord(20, unchecked((ushort)-300)).WriteWord(24, 1024).WriteWord(28, unchecked((ushort)-4096)).WriteWord(32, 2048);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 1024, 0)), -4096, callback == 0x594210 ? 2048 : -4096, -4096) with { X = -100, Y = 200, Z = -300 };
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleGfMatrices.Compose56C2F0(View(), local)), NativeBattleTestSnapshots.Json(NativeBattleGilgameshMeshAuxiliaries.Matrix(task, View())));
        NativeBattleGilgameshMeshAuxiliaryStep first = Run(task);
        Assert.Equal((ushort)32768, first.UpdatedTask.ReadWord(12));
        Assert.Equal(0, first.Result);
        NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(first.UpdatedTask))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Run(first.UpdatedTask)), NativeBattleTestSnapshots.Json(Run(restored)));
    }

    private static NativeBattleGilgameshMeshAuxiliaryStep Run(NativeBattleModulePoolTask task, uint flags = 0) => NativeBattleGilgameshMeshAuxiliaries.Step(task, Resources(), View(), Projection(), flags);
    private static ushort[] Words(NativeBattleModulePoolTask task) => Enumerable.Range(0, 12).Select(word => task.ReadWord(12 + word * 2)).ToArray();
    private static NativeBattleModulePoolTask Task(uint callback) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(28, 4096).WriteWord(32, 4096);
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);

    private static NativeBattleEffectTaskPool Pool()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        return NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.Select(task => NativeBattleTaskFixture.EditTask(task, taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })).ToImmutableArray() });
    }

    private static NativeBattleGilgameshMeshAuxiliaryResources Resources() => new(Mesh(18, 10), Mesh(120, 96), [new(14, -550, 4608, 3072), new(28, -1000, 3072, 2560), new(36, -1300, 3584, 2944)]);

    private static NativeBattleStaticMeshResource Mesh(int vertices, int faces)
    {
        byte[] bytes = new byte[8 + vertices * 8 + 32 + faces * 24];
        int cursor = 8 + vertices * 8;
        BinaryPrimitives.WriteInt32LittleEndian(bytes, cursor);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), vertices);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor + 12), faces);
        return new(0, 0, bytes.ToImmutableArray());
    }
}
