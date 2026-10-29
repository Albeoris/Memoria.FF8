using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltrasonic152Tests
{
    [Fact]
    public void Particle_identity_uses_real_task_owner_and_shared_row_birth_generation()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x244C808, 0x244C818, 36, 100);
        (pool, int slot) = pool.Append(0x60C3C0);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattlePilotVisualIdentity before = NativeBattleUltrasonic152VisualLifetimes.Identity(task, 7, 1);
        NativeBattleUltrasonic152VisualLifetimes.Allocated(task, 7);
        NativeBattlePilotVisualIdentity first = NativeBattleUltrasonic152VisualLifetimes.Identity(task, 7, 1);
        Assert.NotEqual(before.ResourceGeneration, first.ResourceGeneration);
        Assert.Equal(NativeBattlePilotVisualLifetimes.Identity(152, task, 107) with { ResourceGeneration = first.ResourceGeneration }, first);
        Assert.Equal(first, NativeBattleUltrasonic152VisualLifetimes.Identity(task.WriteWord(12, 1), 7, 1));
        Assert.NotEqual(first, NativeBattleUltrasonic152VisualLifetimes.Identity(task, 8, 1));
        NativeBattleUltrasonic152VisualLifetimes.Allocated(task, 7);
        Assert.NotEqual(first, NativeBattleUltrasonic152VisualLifetimes.Identity(task, 7, 1));
    }

    [Fact]
    public void Ring_capture_retains_exact_selected_world_corners_and_native_packet_ledger()
    {
        ImmutableArray<byte> source = Mesh(new(-100, -100, 1000), new(100, -100, 1000), new(-100, 100, 1000), new(100, 100, 1000));
        NativeBattlePilotVisualIdentity identity = new(152, 1, 2, 3, 4);
        NativeBattleGfGeometryBatch native = NativeBattleUltrasonic152.Ring60BA00(source, Identity, Projection, 0x904040, 18, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryBatch captured = NativeBattleUltrasonic152.Ring60BA00(source, Identity, Projection, 0x904040, 18, NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 1000), geometry.A);
        Assert.Equal(new System.Numerics.Vector3(100, 100, 1000), geometry.D);
        Assert.Equal(identity.Generation, geometry.Identity.Generation);
    }

    [Fact]
    public void Ring_uses_uniform_semitransparent_color_and_native_black_depth_cue()
    {
        ImmutableArray<byte> source = Mesh(new(-100, -100, 1000), new(100, -100, 1000), new(-100, 100, 1000), new(100, 100, 1000));
        NativeBattleGfGeometryBatch batch = NativeBattleUltrasonic152.Ring60BA00(source, Identity, Projection, 0x904040, 18, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(0x2E482020u, attempt.Primitive!.Words[1]);
        Assert.Equal(0x00012345u, attempt.Primitive.Words[3]);
    }

    [Fact]
    public void Ring_rejects_all_corners_outside_x_even_across_opposite_screen_edges()
    {
        ImmutableArray<byte> source = Mesh(new(-2000, -100, 1000), new(2000, -100, 1000), new(-2000, 100, 1000), new(2000, 100, 1000));
        NativeBattleGfGeometryBatch batch = NativeBattleUltrasonic152.Ring60BA00(source, Identity, Projection, 0x808080, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Null(Assert.Single(batch.Attempts).Primitive);
        Assert.Equal(0, batch.AllocationBytes);
    }

    [Fact]
    public void Ring_double_sided_mode_still_rejects_zero_area()
    {
        ImmutableArray<byte> source = Mesh(new(0, 0, 1000), new(100, 100, 1000), new(200, 200, 1000), new(300, 300, 1000));
        NativeBattleGfGeometryBatch batch = NativeBattleUltrasonic152.Ring60BA00(source, Identity, Projection, 0x808080, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Null(Assert.Single(batch.Attempts).Primitive);
        Assert.Equal(0, batch.AllocationBytes);
    }

    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(Identity, 256, 160, 108);
    private static ImmutableArray<byte> Mesh(params NativeBattleVector[] points)
    {
        byte[] data = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(data, 40);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 4);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8 + index * 8), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10 + index * 8), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + index * 8), points[index].Z);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60 + index * 2), (ushort)(index * 2));
        }
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(68), 0x12345);
        return data.ToImmutableArray();
    }
}
