using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleXatmDeath276BodyTests
{
    [Fact]
    public void Captured276_fixed_white_preserves_native_pixel_rectangle_scale_bytes_and_discrete_operation()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleXatmDeath276Body.White7040B0(255, 128, 1, 2048);
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        NativeBattleGeometryPrimitive captured = NativeBattleSelectedProgrammeVisuals.Discrete(primitive, new(276, 0x1000, 0, 1, 0));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(primitive), System.Text.Json.JsonSerializer.Serialize(captured));
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(captured.PilotGeometry);
        Assert.True(visual.ScreenSpace);
        Assert.False(visual.Continuous);
        Assert.Equal(8, visual.ScreenScale);
        Assert.Equal(new System.Numerics.Vector3(320, 0, 0), visual.B);
        Assert.Equal(new System.Numerics.Vector3(320, 216, 0), visual.D);
        Assert.Equal(8, captured.Bucket);
    }

    [Fact]
    public void Captured276_body_keeps_retained_hidden_scratch_and_native_packets_with_actual_visible_world_corners()
    {
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        NativeBattleRawPolygon polygon = new(false, face.ToImmutableArray());
        NativeBattleRawMesh mesh = new([new(new(-20, -20, 0), 0), new(new(20, -20, 0), 0), new(new(0, 20, 0), 0), new(new(1, 2, 3), 1)], [polygon]);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 104);
        ImmutableArray<byte> retained = Enumerable.Repeat((byte)0xAB, 32).ToImmutableArray();
        var nativeProjection = NativeBattleXatmDeath276Body.Project6A2A90(mesh, [matrix], projection, retained, new HashSet<int> { 1 });
        var capturedProjection = NativeBattleXatmDeath276Body.Project6A2A90(mesh, [matrix], projection, retained, new HashSet<int> { 1 }, true);
        Assert.Equal(nativeProjection.Bytes, capturedProjection.Bytes);
        Assert.Null(capturedProjection.World[3]);
        var native = NativeBattleXatmDeath276Body.Packets6859D0(mesh.Polygons, nativeProjection.Vertices, 0x123456, null, 341, 256);
        var captured = NativeBattleXatmDeath276Body.CapturedPackets6859D0(mesh.Polygons, capturedProjection, 0x123456, null, 341, 256, new(276, 0x1000, 0, 1, 0));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(native), System.Text.Json.JsonSerializer.Serialize(captured));
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-20, -20, 4096), visual.A);
        Assert.False(visual.ScreenSpace);
        Assert.True(visual.UseSourceDepths);
    }

    [Fact]
    public void Captured276_debris_row_reallocation_changes_identity_while_preserving_native_geometry_masks_and_task_bytes()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x2454, 1);
        (pool, _) = pool.Append(0x6851F0);
        NativeBattleModulePoolTask director = pool.Slots[0];
        int record = 0x54;
        director = director.WriteWord(record + 6, 1);
        director = Point(director, record, 0, -10, 4096);
        director = Point(director, record + 0x24, -20, -20, 0);
        director = Point(director, record + 0x2A, 20, -20, 0);
        director = Point(director, record + 0x30, 0, 20, 0);
        NativeBattleSelectedProgrammeVisuals.Allocated([pool], 0x2054);
        string before = System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(director));
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 512, 160, 104);
        var native = NativeBattleXatmDeath276DebrisDrawing.DrawDebris(director, view, projection, 341, 256);
        var first = NativeBattleXatmDeath276DebrisDrawing.DrawDebris(director, view, projection, 341, 256, true);
        NativeBattleSelectedProgrammeVisuals.Allocated([pool], 0x2054);
        var second = NativeBattleXatmDeath276DebrisDrawing.DrawDebris(director, view, projection, 341, 256, true);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(director)));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(native), System.Text.Json.JsonSerializer.Serialize(first));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first), System.Text.Json.JsonSerializer.Serialize(second));
        var a = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(first.Attempts).Primitive!.PilotGeometry);
        var b = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(second.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(a.Identity.Generation, b.Identity.Generation);
        Assert.NotEqual(a.Identity.ResourceGeneration, b.Identity.ResourceGeneration);
        Assert.Equal(new System.Numerics.Vector3(-20, -30, 4096), a.A);
    }

    [Fact]
    public void HiddenAndZeroDepthVerticesWriteOnlyTheActualRejectionWord()
    {
        NativeBattleRawMesh mesh = new([new(new(0, 0, 0), 0)], []);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(identity, 512, 160, 104);
        ImmutableArray<byte> retained = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 }.ToImmutableArray();
        NativeBattleXatmDeath276BodyProjection hidden = NativeBattleXatmDeath276Body.Project6A2A90(mesh, [identity], projection, retained, new HashSet<int> { 0 });
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x10, 0 }, hidden.Bytes);
        NativeBattleXatmDeath276BodyProjection zero = NativeBattleXatmDeath276Body.Project6A2A90(mesh, [identity], projection, retained, new HashSet<int>());
        Assert.Equal(hidden.Bytes, zero.Bytes);
    }

    [Fact]
    public void OriginalWhiteQuadUsesBucketEightFullDwordWritesAndWrappedByteColor()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleXatmDeath276Body.White7040B0(255, 128, 1, 2048);
        Assert.Equal(24, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(8, primitive.Bucket);
        Assert.Equal(new uint[] { 0x05000000, 0x2A00407F, 0, 0x140, 0xD80000, 0xD80140 }, primitive.Words);
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void DebrisDrawBeforeMovePreservesUvPaddingAndUsesZeroBaseNativeAverageDepth()
    {
        NativeBattleModulePoolTask director = Task();
        int record = 0x54;
        director = director.WriteWord(record + 6, 1).WriteWord(record + 14, 2);
        director = director.WriteWord(record + 0x10, 4096).WriteWord(record + 0x18, 4096).WriteWord(record + 0x20, 4096);
        director = Point(director, record, 0, -10, 4096);
        director = Point(director, record + 0x24, -20, -20, 0);
        director = Point(director, record + 0x2A, 20, -20, 0);
        director = Point(director, record + 0x30, 0, 20, 0);
        director = director.WriteWord(record + 10, 20).WriteDword(0x50, checked(0x20DFAB8u + (uint)record));
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleGfGeometryBatch batch = NativeBattleXatmDeath276DebrisDrawing.DrawDebris(director, view, new(view, 512, 160, 104), 341, 256);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
        NativeBattleModulePoolTask next = NativeBattleXatmDeath276Debris.MoveDebris(director, 0x20DFAB8);
        Assert.Equal((ushort)1, next.ReadWord(record + 14));
        Assert.Equal((short)10, unchecked((short)next.ReadWord(record + 2)));
        Assert.Equal((short)-50, unchecked((short)next.ReadWord(record + 10)));
        Assert.Equal((ushort)1, next.ReadWord(record + 6));
        next = NativeBattleXatmDeath276Debris.MoveDebris(next, 0x20DFAB8);
        Assert.Equal((ushort)0, next.ReadWord(record + 6));
        Assert.Equal(0x20DFAB8u + record, next.ReadDword(0x50));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x6851F0, new byte[0x2454 - 12].ToImmutableArray());
    private static NativeBattleModulePoolTask Point(NativeBattleModulePoolTask task, int offset, short x, short y, short z) => task.WriteWord(offset, unchecked((ushort)x)).WriteWord(offset + 2, unchecked((ushort)y)).WriteWord(offset + 4, unchecked((ushort)z));
}

