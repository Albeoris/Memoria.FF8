using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshFlareTests
{
    [Theory]
    [InlineData(1u, 7)]
    [InlineData(0x200u, 7)]
    [InlineData(0u, 8)]
    public void Flare_draws_before_pause_and_terminal_counter(uint flags, short counter)
    {
        NativeBattleModulePoolTask task = Task(counter);
        NativeBattleGilgameshFlareStep step = NativeBattleGilgameshFlare.Step591470(task, Resources(), Projection(), flags);
        Assert.Equal(48, step.Batch.Attempts.Length);
        Assert.Equal(flags == 0 ? 2 : 0, step.Result);
        Assert.Equal(flags == 0 ? counter + 1 : counter, (short)step.UpdatedTask.ReadWord(12));
        if (flags != 0)
        {
            Assert.Same(task, step.UpdatedTask);
        }
    }

    [Theory]
    [InlineData(-17, -16)]
    [InlineData(-15, -15)]
    [InlineData(17, 16)]
    public void Integration_wraps_word_scale_and_truncates_negative_velocity(short velocity, short next)
    {
        NativeBattleModulePoolTask task = Task(0).WriteWord(0x1C, 32767).WriteWord(0x1E, unchecked((ushort)velocity));
        NativeBattleGilgameshFlareStep step = NativeBattleGilgameshFlare.Step591470(task, Resources(), Projection(), 0);
        Assert.Equal(unchecked((ushort)(32767 + velocity)), step.UpdatedTask.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)next), step.UpdatedTask.ReadWord(0x1E));
        Assert.Equal(task.ReadWord(0x20), step.UpdatedTask.ReadWord(0x20));
        Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(2, 14).ToArray(), NativeBattleStorageSnapshots.Capture(step.UpdatedTask).Payload.AsSpan().Slice(2, 14).ToArray());
    }

    [Fact]
    public void Matrix_is_direct_screen_space_and_projection_and_json_resume_are_preserved()
    {
        NativeBattleModulePoolTask task = Task(short.MaxValue).WriteWord(0x10, unchecked((ushort)-123)).WriteWord(0x12, 456).WriteWord(0x18, 1024).WriteWord(0x1C, 2048).WriteWord(0x20, unchecked((ushort)-4096));
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 1024)), 2048, -4096, -4096) with { X = -123, Y = 456, Z = 288 };
        Assert.Equal(expected.Rotation.ToArray(), NativeBattleGilgameshFlare.Matrix(task).Rotation.ToArray());
        Assert.Equal((-123, 456, 288), (NativeBattleGilgameshFlare.Matrix(task).X, NativeBattleGilgameshFlare.Matrix(task).Y, NativeBattleGilgameshFlare.Matrix(task).Z));
        NativeBattleCameraProjection projection = Projection();
        string original = NativeBattleTestSnapshots.Json(projection);
        NativeBattleGilgameshFlareStep first = NativeBattleGilgameshFlare.Step591470(task, Resources(), projection, 0);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(projection));
        Assert.Equal((ushort)0x8000, first.UpdatedTask.ReadWord(12));
        Assert.Equal(0, first.Result);
        NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(first.UpdatedTask))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleGilgameshFlare.Step591470(first.UpdatedTask, Resources(), projection, 0)), NativeBattleTestSnapshots.Json(NativeBattleGilgameshFlare.Step591470(restored, Resources(), projection, 0)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65535)]
    public void WordE_selects_A_only_for_zero(ushort selector)
    {
        NativeBattleGilgameshFlareResources resources = Resources();
        NativeBattleModulePoolTask task = Task(0).WriteWord(14, selector);
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200((selector == 0 ? resources.SourceA : resources.SourceB).Bytes, 0, default, NativeBattleGilgameshFlare.Matrix(task), Projection() with { FocalLength = 288 }, new(0x33, null, null, null, null, null, 17, 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(NativeBattleGilgameshFlare.Step591470(task, resources, Projection(), 0).Batch));
    }

    private static NativeBattleModulePoolTask Task(short counter) => new NativeBattleModulePoolTask(5, 7, null, 0x591470, new byte[24].ToImmutableArray()).WriteWord(12, unchecked((ushort)counter)).WriteWord(0x1C, 4096).WriteWord(0x20, 4096);
    private static NativeBattleCameraProjection Projection() => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096), 1000, 17, -29);
    private static NativeBattleGilgameshFlareResources Resources() => new(Mesh(0x30000000), Mesh(0x30010203));

    private static NativeBattleStaticMeshResource Mesh(uint color)
    {
        byte[] bytes = new byte[0x680];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 496);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 61);
        int cursor = 496;
        for (int group = 0; group < 8; group++)
        {
            int count = group == 4 ? 4 : group == 5 ? 44 : 0;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), count);
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), color);
                cursor += group == 4 ? 20 : 24;
            }
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
}
