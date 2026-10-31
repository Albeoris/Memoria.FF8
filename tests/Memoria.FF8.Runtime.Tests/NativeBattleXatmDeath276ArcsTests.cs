using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleXatmDeath276ArcsTests
{
    [Fact]
    public void Captured276_arc_keeps_original_task_fade_random_draw_count_packet_masks_and_actual_world_centers()
    {
        NativeBattleModulePoolTask Task()
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x4C, 1);
            (pool, _) = pool.Append(0x686A10);
            return NativeBattleXatmDeath276Arcs.Initialize(pool.Slots[0], 0x3000, 0x4000, 0x5000, 12, 7).WriteWord(12, 10);
        }
        NativeBattleXatmDeath276ArcResources resources = new(Enumerable.Repeat((ushort)4096, 256).ToImmutableArray(), Distances);
        NativeBattleQuezacotlRibbonSample Triangle(uint address) => new(new(address == 0x4000 ? (short)0 : (short)700, 0, 1000), new(0, 4096, 0));
        var native = NativeBattleXatmDeath276Arcs.Advance(Task(), resources, new short[8].ToImmutableArray(), Projection, new(17), Triangle);
        NativeBattlePilotVisualIdentity identity = new(276, 0x1000, 0, 1, 0) { ResourceGeneration = 0x40005000 };
        var captured = NativeBattleXatmDeath276Arcs.Advance(Task(), resources, new short[8].ToImmutableArray(), Projection, new(17), Triangle, null, identity);
        Assert.Equal(native.Result, captured.Result);
        Assert.Equal(native.Random, captured.Random);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(native.Task)), System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Task)));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(native.FramePrimitives), System.Text.Json.JsonSerializer.Serialize(captured.FramePrimitives));
        Assert.NotEmpty(captured.FramePrimitives);
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(captured.FramePrimitives[0].PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(0, 0, 1000), visual.A);
        Assert.Equal(4, visual.ScreenOffsets.Length);
        Assert.True(visual.UseSourceDepths);
        Assert.Equal(captured.FramePrimitives[0].VertexPayload4[0], visual.SourceDepths!.Value.X);
    }

    [Fact]
    public void Spawn_compares_triangle_sums_at_exact_256_boundary()
    {
        byte[] triangles = new byte[48];
        BinaryPrimitives.WriteInt16LittleEndian(triangles.AsSpan(24), 255);
        Assert.False(NativeBattleXatmDeath276Arcs.CanSpawn(triangles.ToImmutableArray(), 0, 1));
        BinaryPrimitives.WriteInt16LittleEndian(triangles.AsSpan(24), 256);
        Assert.True(NativeBattleXatmDeath276Arcs.CanSpawn(triangles.ToImmutableArray(), 0, 1));
    }

    [Fact]
    public void Ribbon_emits_seven_native_frame_packets_and_preserves_unused_uv_high_words()
    {
        ImmutableArray<NativeBattleGeometryPrimitive> packets = NativeBattleXatmDeath276Arcs.Draw686DB0(Points, Widths, Brightness, Projection, Distances);
        Assert.Equal(7, packets.Length);
        NativeBattleGeometryPrimitive first = packets[0];
        Assert.Equal(79, first.Bucket);
        Assert.Equal(13, first.Words.Length);
        Assert.Equal(0x3E808080u, first.Words[1]);
        Assert.Equal(0x3CD4C008u, first.Words[3]);
        Assert.Equal(0x00BAC038u, first.Words[6]);
        Assert.Equal(0xC008u, first.Words[9]);
        Assert.Equal(0xC038u, first.Words[12]);
        Assert.Equal(859, (short)(first.Words[2] >> 16));
        Assert.Equal(869, (short)(first.Words[5] >> 16));
        Assert.Equal(new[] { 38, 39, 50, 51 }, Enumerable.Range(0, 52).Where(index => first.PhysicalWriteMask!.Value[index] == NativeBattleGfPacketByteState.Untouched).ToArray());
    }

    [Fact]
    public void Coincident_first_points_require_authentic_direction_residue_only_when_read()
    {
        ImmutableArray<NativeBattleVector> points = Points.SetItem(1, Points[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleXatmDeath276Arcs.Draw686DB0(points, Widths, Brightness, Projection, Distances));
        Assert.Equal(6, NativeBattleXatmDeath276Arcs.Draw686DB0(points, Widths, Brightness, Projection, Distances, 0x12345678).Length);
    }

    [Fact]
    public void Arc_draws_then_fades_and_consumes_eighteen_shared_random_values()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x4C, 1);
        (pool, _) = pool.Append(0x686A10);
        NativeBattleModulePoolTask task = NativeBattleXatmDeath276Arcs.Initialize(pool.Slots[0], 0x3000, 0x4000, 0x5000, 12, 7).WriteWord(12, 10);
        NativeBattleSharedEffectRandomState initial = new(17);
        NativeBattleSharedEffectRandomState expected = initial;
        for (int index = 0; index < 18; index++)
        {
            expected = expected.Draw7059E0(out _);
        }
        NativeBattleXatmDeath276ArcResources resources = new(Enumerable.Repeat((ushort)4096, 256).ToImmutableArray(), Distances);
        NativeBattleXatmDeath276ArcStep step = NativeBattleXatmDeath276Arcs.Advance(task, resources, new short[8].ToImmutableArray(), Projection, initial, address => new(new(address == 0x4000 ? (short)0 : (short)700, 0, 1000), new(0, 4096, 0)));
        Assert.Equal(expected, step.Random);
        Assert.Equal(0, step.Result);
        Assert.Equal(11, step.Task.ReadWord(12));
        Assert.Equal(88, step.Task.ReadWord(0x1E));
        Assert.All(step.FramePrimitives, packet => Assert.Equal(0x3E808080u, packet.Words[1]));
    }

    private static NativeBattleCameraProjection Projection => new(NativeBattleCuragaMatrices.Identity8DD770(), 256, 160, 108);
    private static ImmutableArray<NativeBattleVector> Points => Enumerable.Range(0, 8).Select(index => new NativeBattleVector((short)(index * 12), 0, 1000)).ToImmutableArray();
    private static ImmutableArray<short> Widths => Enumerable.Repeat((short)200, 8).ToImmutableArray();
    private static ImmutableArray<short> Brightness => Enumerable.Repeat((short)128, 8).ToImmutableArray();
    private static ImmutableArray<byte> Distances => Enumerable.Range(0, 16384).Select(index => (byte)Math.Sqrt((index / 128) * (index / 128) + (index % 128) * (index % 128))).ToImmutableArray();
}
