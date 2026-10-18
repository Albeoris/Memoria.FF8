using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshFinalRingTests
{
    [Theory]
    [InlineData(0u, 1)]
    [InlineData(1u, 2)]
    [InlineData(0x200u, 2)]
    public void Positive_signed_delay_suppresses_draw_and_only_unpaused_decrements(uint flags, ushort remaining)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(14, 2);
        NativeBattleGilgameshFinalRingStep step = NativeBattleGilgameshFinalRing.Step591F50(task, Mesh(), View(), Projection(), flags);
        Assert.Empty(step.Batch.Attempts);
        Assert.Equal(remaining, step.UpdatedTask.ReadWord(14));
        Assert.Equal(task.ReadWord(12), step.UpdatedTask.ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(15)]
    [InlineData(16)]
    public void Ring_draws_before_pause_and_physics_with_exact_flags_and_fade(short counter)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(12, unchecked((ushort)counter)).WriteWord(14, 65535).WriteWord(30, 17).WriteWord(34, unchecked((ushort)-17));
        NativeBattleModuleMeshContext context = new(counter >= 4 ? 0xF3u : 0x33u, 0, counter >= 4 ? 341 * (counter - 4) : null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200(Mesh().Bytes, 0, default, NativeBattleGilgameshFinalRing.Matrix(task, View()), Projection(), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGilgameshFinalRingStep paused = NativeBattleGilgameshFinalRing.Step591F50(task, Mesh(), View(), Projection(), 0x201);
        NativeBattleGilgameshFinalRingStep step = NativeBattleGilgameshFinalRing.Step591F50(task, Mesh(), View(), Projection(), 0);
        Assert.Same(task, paused.UpdatedTask);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(paused.Batch));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(step.Batch));
        Assert.Equal(24, step.Batch.Attempts.Length);
        Assert.Equal(counter >= 15 ? 2 : 0, step.Result);
        Assert.Equal((ushort)13, step.UpdatedTask.ReadWord(30));
        Assert.Equal(unchecked((ushort)-13), step.UpdatedTask.ReadWord(34));
        Assert.Equal((ushort)4113, step.UpdatedTask.ReadWord(28));
        Assert.Equal((ushort)4079, step.UpdatedTask.ReadWord(32));
    }

    [Fact]
    public void Ring_matrix_ignores_taskY_and_preserves_exact_shared_composition()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(16, unchecked((ushort)-111)).WriteWord(18, 12345).WriteWord(20, 222).WriteWord(24, 1024).WriteWord(28, unchecked((ushort)-4096)).WriteWord(32, 2048);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 1024, 0)), -4096, 2048, -4096) with { X = -111, Y = 0, Z = 222 };
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGfMatrices.Compose56C2F0(View(), local)), JsonSerializer.Serialize(NativeBattleGilgameshFinalRing.Matrix(task, View())));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGilgameshFinalRing.Matrix(task, View())), JsonSerializer.Serialize(NativeBattleGilgameshFinalRing.Matrix(task.WriteWord(18, 0), View())));
    }

    [Fact]
    public void Signed_counter_and_scale_wrap_and_json_next_callback_are_preserved()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(12, 32767).WriteWord(28, 32767).WriteWord(30, 3).WriteWord(32, 0).WriteWord(34, unchecked((ushort)-3));
        NativeBattleGilgameshFinalRingStep first = NativeBattleGilgameshFinalRing.Step591F50(task, Mesh(), View(), Projection(), 0);
        Assert.Equal((ushort)32768, first.UpdatedTask.ReadWord(12));
        Assert.Equal((ushort)32770, first.UpdatedTask.ReadWord(28));
        Assert.Equal((ushort)65533, first.UpdatedTask.ReadWord(32));
        Assert.Equal((ushort)3, first.UpdatedTask.ReadWord(30));
        Assert.Equal((ushort)65533, first.UpdatedTask.ReadWord(34));
        Assert.Equal(0, first.Result);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(first.UpdatedTask))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGilgameshFinalRing.Step591F50(first.UpdatedTask, Mesh(), View(), Projection(), 0)), JsonSerializer.Serialize(NativeBattleGilgameshFinalRing.Step591F50(restored, Mesh(), View(), Projection(), 0)));
    }

    private static NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(1, 0, null, 0x591F50, new byte[24].ToImmutableArray()).WriteWord(28, 4096).WriteWord(32, 4096);
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[0x3A0];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 320);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 39);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(332), 24);
        return new(0, 0, bytes.ToImmutableArray());
    }
}
