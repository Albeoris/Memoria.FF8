using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCloudBombCaptureTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Bomb_primary_and_overlay_capture_actual_world_vertices_without_changing_native_packets(bool textured, bool quad)
    {
        byte[] face = new byte[quad ? 20 : 16];
        for (int index = 0; index < (quad ? 4 : 3); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(index * 2), (ushort)index);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0x12340011);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x02BA0022);
        byte[] vertices = new byte[32];
        short[] coordinates = [-100, -100, 0, 100, -100, 0, -100, 100, 0, 100, 100, 0];
        for (int index = 0; index < 4; index++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + axis * 2), coordinates[index * 3 + axis]);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(index * 8 + 6), (ushort)(0xA000 + index));
        }
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleGfGeometryBatch native = NativeBattleBombActorPackets.Emit([new(quad, face.ToImmutableArray())], vertices.ToImmutableArray(), view, projection, 0x808080, 0x123456, 72, textured, 0x155, 0x100);
        string detached = NativeBattleTestSnapshots.Json(native);
        NativeBattlePilotVisualIdentity identity = new(textured ? (ushort)179 : (ushort)180, 0x2309EE0, 2, 31, 4) { ResourceGeneration = 77 };
        NativeBattleGfGeometryBatch captured = NativeBattleBombActorPackets.Emit([new(quad, face.ToImmutableArray())], vertices.ToImmutableArray(), view, projection, 0x808080, 0x123456, 72, textured, 0x155, 0x100, identity);
        Assert.Equal(detached, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(2, captured.Attempts.Length);
        HashSet<NativeBattlePilotVisualIdentity> identities = [];
        foreach (NativeBattleGfGeometryAttempt attempt in captured.Attempts)
        {
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.True(identities.Add(visual.Identity));
            Assert.Equal(identity.Generation, visual.Identity.Generation);
            Assert.Equal(77, visual.Identity.ResourceGeneration);
            Assert.Equal(new Vector3(-100, -100, 0), visual.A);
            Assert.Equal(quad ? new Vector3(100, 100, 0) : default, visual.D);
            Assert.Equal(quad ? 4 : 3, visual.CornerCount);
            Assert.Equal(new Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), visual.SourceDepths);
            Assert.True(visual.UseSourceDepths);
        }
    }

    [Theory]
    [InlineData(168)]
    [InlineData(179)]
    [InlineData(180)]
    public void Reusing_a_real_particle_row_changes_its_visual_birth_without_native_task_writes(ushort effect)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x123400, 0x123500, 36, 1);
        (pool, int slot) = pool.Append(0x500000);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        string detached = NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(task));
        NativeBattlePilotVisualIdentity first = NativeBattleCloudBombVisualLifetimes.Identity(effect, task, 7, 0xD6BDE8);
        NativeBattleCloudBombVisualLifetimes.Allocated(task, 7);
        NativeBattlePilotVisualIdentity next = NativeBattleCloudBombVisualLifetimes.Identity(effect, task, 7, 0xD6BDE8);
        Assert.NotEqual(first.ResourceGeneration, next.ResourceGeneration);
        Assert.Equal(first.Generation, next.Generation);
        Assert.Equal(first.Primitive, next.Primitive);
        Assert.Equal(detached, NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(task)));
        Assert.NotEqual(next, NativeBattleCloudBombVisualLifetimes.Identity(effect, task, 107, 0xD6BDE8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sleeping_cloud_captures_mutable_source_once_and_keeps_rejected_fourth_lazy(bool rejected)
    {
        byte[] model = new byte[52];
        BinaryPrimitives.WriteInt32LittleEndian(model, 0);
        BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(16), 0x2C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(20 + corner * 2), (ushort)(corner * 2));
        }
        if (rejected)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(26), ushort.MaxValue);
        }
        byte[] vertices = new byte[32];
        short[] coordinates = [-100, -100, 100, -100, -100, 100, 100, 100];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8), coordinates[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + 2), coordinates[index * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + 4), rejected ? (short)-1 : (short)4096);
            BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(index * 8 + 6), 0xA5A5);
        }
        byte[] before = (byte[])vertices.Clone();
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattleGfGeometryBatch native = NativeBattleSleepingGas168.DrawCloud(model.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 30, colors);
        string detached = NativeBattleTestSnapshots.Json(native);
        NativeBattleGfGeometryBatch captured = NativeBattleSleepingGas168.DrawCloud(model.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 30, colors, new(168, 0x23B6A88, 3, 45, 0) { ResourceGeneration = 0xD6EDE4 });
        Assert.Equal(detached, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(before, vertices);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(captured.Attempts);
        if (rejected)
        {
            Assert.Null(attempt.Primitive);
        }
        else
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry);
            Assert.Equal(new Vector3(-100, -100, 4096), visual.A);
            Assert.Equal(new Vector3(100, 100, 4096), visual.D);
            Assert.Equal(0xD6EDE4, visual.Identity.ResourceGeneration);
            Assert.True(visual.UseSourceDepths);
        }
    }
}
