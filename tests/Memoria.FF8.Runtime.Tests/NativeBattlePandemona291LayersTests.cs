using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePandemona291LayersTests
{
    [Fact]
    public void Opening_calls_keep_literal_unsigned_growth_scalars_and_independent_texture_phases()
    {
        ImmutableArray<NativeBattlePandemona291LayerRequest> rows = NativeBattlePandemona291Layers.Opening(4000, 25);
        Assert.Equal(3, rows.Length);
        Assert.Equal(new NativeBattlePandemona291LayerRequest(0x13A0E58, 0x13A4280, 6000, 64, 0, 0, 0, 292, 4388, 0, 32, 3950), rows[0]);
        Assert.Equal(new NativeBattlePandemona291LayerRequest(0x13A4F88, 0x13A5EF0, 6000, 64, 0, 0, 0, 584, 4388, 1, 32, 4023), rows[1]);
        Assert.Equal(new NativeBattlePandemona291LayerRequest(0x13A6508, 0x13A7BF0, 6000, 64, 0, 0, 0, 584, 4388, 1, 40, 4023), rows[2]);
    }

    [Fact]
    public void GPF_interpolation_rotation_radial_lookup_and_view_translation_keep_native_order()
    {
        NativeBattlePandemona291Animation source = new(1, Shape(new(10, 20, 30), 1024));
        ImmutableArray<byte> target = Shape(new(50, 60, 70), 0);
        byte[] trig = Trig();
        BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(512 * 4), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(2048 * 4 + 2), -4096);
        NativeBattlePandemona291LayerRequest request = new(1, 2, 1000, 0, 2048, 1024, 4096, 4096, 2048, 0, 255, 2048);
        NativeBattlePandemona291PreparedLayer result = NativeBattlePandemona291Layers.Prepare6F2A30(source, target, trig.ToImmutableArray(), Identity with { X = 1, Y = 2, Z = 3 }, request);
        Assert.Equal(new NativeBattleVector(30, 40, 50), result.Vertices[0].Input);
        Assert.Equal(new NativeBattleVector(30, 20, -1998), result.Vertices[0].Position);
        Assert.Equal((ushort)1024, result.Vertices[0].Padding);
        Assert.Equal((1, 2, 1003), (result.Matrix.X, result.Matrix.Y, result.Matrix.Z));
        Assert.Equal(0x20F0u, result.Context.Flags);
        Assert.Equal(0u, result.Context.FarColor);
        Assert.Equal(2048, result.Context.Fade);
        Assert.Equal((ushort)127, result.Context.UOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 128, 256), result.Context.WindowAfter);
    }

    [Theory]
    [InlineData(-30000, 30000, 2048, -32768)]
    [InlineData(0, 4096, -1, -1)]
    public void Interpolation_narrows_difference_and_IR0_before_GPF_then_adds_unsigned_source_word(short first, short second, short ratio, short expected)
    {
        NativeBattlePandemona291LayerRequest request = new(1, 2, 0, 0, ratio, 0, 0, 4096, 4096, 1, 129, 0);
        NativeBattlePandemona291PreparedLayer result = NativeBattlePandemona291Layers.Prepare6F2A30(new(1, Shape(new(first, 0, 0), 0)), Shape(new(second, 0, 0), 0), Trig().ToImmutableArray(), Identity, request);
        Assert.Equal(expected, result.Vertices[0].Position.X);
        Assert.Equal(0x2030u, result.Context.Flags);
        Assert.Null(result.Context.FarColor);
        Assert.Equal((ushort)1, result.Context.VOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 128, 256, 128), result.Context.WindowAfter);
    }

    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static byte[] Trig()
    {
        byte[] bytes = new byte[0x4000];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), 4096);
        return bytes;
    }

    private static ImmutableArray<byte> Shape(NativeBattleVector point, ushort padding)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), point.Z);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), padding);
        return bytes.ToImmutableArray();
    }
}

public sealed partial class NativeBattlePandemona291EntryTests
{
    [Fact]
    public void Layer_particle_births_consume_random_only_after_success_and_preserve_task_tail_unknownness()
    {
        NativeBattlePandemona291Frame frame = ParticleFixture(0);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(0x1AA4 + 0x1A, 2) } };
        NativeBattlePandemona291Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePandemona291Frame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattlePandemona291Frame result = NativeBattlePandemona291LayerParticles.Spawn6EDBCF(frame, 28);
        NativeBattleEffectTaskPool pool = Assert.Single(result.Effect.Pools);
        Assert.Equal(0, pool.Head);
        Assert.Equal(1, pool.Tail);
        Assert.Equal(0x6F0ED0u, pool.Slots[0].Callback);
        Assert.Equal((ushort)292, pool.Slots[0].ReadWord(0xE));
        Assert.Equal((ushort)6000, pool.Slots[0].ReadWord(0x14));
        NativeCrtRandomState random = frame.Random.Draw(out ushort first).Draw(out ushort second);
        Assert.Equal(random, result.Random);
        Assert.Equal(first, pool.Slots[0].ReadWord(0x10));
        Assert.Equal(second, pool.Slots[1].ReadWord(0x10));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(0x1AA4 + 0x1A, 2));
        NativeBattlePandemona291Frame full = NativeBattlePandemona291LayerParticles.Spawn6EDBCF(result, 28);
        Assert.Equal(result.Random, full.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattlePandemona291LayerParticles.Spawn6EDBCF(restored, 28)));
    }

    [Theory]
    [InlineData(0, 0, 1, 128, 0)]
    [InlineData(0, 11, 12, 0, 2)]
    [InlineData(17, 11, 11, 0, 0)]
    public void Layer_particle_pause_and_terminal_counter_preserve_native_spin_order(uint pause, ushort age, ushort expectedAge, ushort angle, int status)
    {
        NativeBattlePandemona291Frame frame = ParticleFixture(pause);
        NativeBattleModulePoolTask task = frame.Effect.Pools[0].Slots[0].WriteWord(0xC, age).WriteWord(0x10, 0).WriteWord(0x12, 128);
        NativeBattlePandemona291ChildStep result = NativeBattlePandemona291LayerParticles.Finish6F0ED0(frame, 0x20DFAB8u + 0x1AA4, task);
        Assert.Equal(expectedAge, result.Task.ReadWord(0xC));
        Assert.Equal(angle, result.Task.ReadWord(0x10));
        Assert.Equal(status, result.Result);
    }

    [Fact]
    public void Layer_particle_uses_current_phase_scale_depth_displacement_and_real_arena_packet_publication()
    {
        NativeBattlePandemona291Frame frame = ParticleFixture(0);
        NativeBattleModulePoolTask task = frame.Effect.Pools[0].Slots[0].WriteWord(0xC, 0).WriteWord(0xE, 100).WriteWord(0x10, 0).WriteWord(0x14, 6000).WriteWord(0x16, 4096).WriteWord(0x18, 2048);
        byte[] bytes = new byte[60];
        bytes[3] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 12);
        for (int index = 0; index <= 12; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * index), 36);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 1);
        bytes[40] = 16;
        bytes[41] = 128;
        bytes[42] = 16;
        bytes[43] = 0x2C;
        NativeBattlePandemona291SpriteResource resource = new(1, bytes.ToImmutableArray());
        byte[] trig = new byte[0x4000];
        BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(2), 4096);
        NativeBattleModuleDraw draw = NativeBattlePandemona291LayerParticles.Draw6F0ED0(task, resource, trig.ToImmutableArray());
        Assert.Equal(new NativeBattleModuleAnchor(0, 0, 6100, 0), draw.Anchor);
        Assert.Equal(-512, draw.DepthOffset);
        Assert.Equal(4096, draw.Scale);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, new(identity, 100, 160, 100), 0, 17, 2);
        Assert.Equal(40, batch.AllocationBytes);
        NativeBattleAllocatedPrimitive primitive = Assert.Single(batch.Primitives);
        Assert.Equal(new int[] { 5588, 5588, 5588, 5588 }, primitive.Primitive.VertexPayload4);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, 0x1000, NativeBattleLogicalOrdering.Clear(), batch, NativeBattleEffectGouraudWrites.FullDword);
        Assert.Equal(0x1028, written.Cursor);
        Assert.Single(written.Ordering.Proxies);
    }

    private static NativeBattlePandemona291Frame ParticleFixture(uint pause)
    {
        NativeBattlePandemona291Frame frame = Fixture().Frame;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2556298, 0x20DFAB8u + 0x1AA4, 0x1C, 2);
        return frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory with { Pandemona291 = frame.Memory.Pandemona291! with { Pause2556258 = pause, Center25562E8 = 4000 } }, [pool]), Effect = frame.Effect with { Pools = [pool] } };
    }
}
