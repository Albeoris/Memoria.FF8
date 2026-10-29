using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStickyWeb153Tests
{
    [Fact]
    public void Actual_task_bank_owner_keeps_particle_generation_across_data_copies_and_changes_on_native_birth()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x243AFC0, 0x243AFD0, 36, 100);
        (pool, int slot) = pool.Append(0x60A990);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattlePilotVisualIdentity before = NativeBattleStickyWeb153VisualLifetimes.Identity(task, 7, 1);
        NativeBattleStickyWeb153VisualLifetimes.Allocated(task, 7);
        NativeBattlePilotVisualIdentity born = NativeBattleStickyWeb153VisualLifetimes.Identity(task, 7, 1);
        Assert.NotEqual(before.ResourceGeneration, born.ResourceGeneration);
        Assert.Equal(born, NativeBattleStickyWeb153VisualLifetimes.Identity(task.WriteWord(12, 2), 7, 1));
        Assert.NotEqual(born, NativeBattleStickyWeb153VisualLifetimes.Identity(task, 7, 2));
        NativeBattleStickyWeb153VisualLifetimes.Allocated(task, 7);
        Assert.NotEqual(born, NativeBattleStickyWeb153VisualLifetimes.Identity(task, 7, 1));
    }

    [Fact]
    public void Actual_line_and_both_strip_layers_capture_distinct_authored_geometry_without_native_writes()
    {
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 1000 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattlePilotVisualIdentity owner = new(153, 1, 2, 3, 4);
        NativeBattleVector a = new(-100, -50, 0);
        NativeBattleVector b = new(100, 50, 0);
        NativeBattleGfGeometryBatch nativeLine = NativeBattleStickyWeb153Drawing.Line(a, b, view, projection, 0x155);
        NativeBattleGfGeometryBatch capturedLine = NativeBattleStickyWeb153Drawing.Line(a, b, view, projection, 0x155, owner);
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeLine), NativeBattleTestSnapshots.Json(capturedLine));
        NativeBattlePilotVisualGeometry line = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(capturedLine.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(2, line.CornerCount);
        Assert.Equal(new System.Numerics.Vector3(a.X, a.Y, a.Z), line.A);
        Assert.Equal(new System.Numerics.Vector3(b.X, b.Y, b.Z), line.B);
        ImmutableArray<NativeBattleVector> first = Enumerable.Range(0, 16).Select(index => new NativeBattleVector((short)(index * 10), -50, 0)).ToImmutableArray();
        ImmutableArray<NativeBattleVector> second = first.Select(point => point with { Y = 50 }).ToImmutableArray();
        NativeBattleGfGeometryBatch native = NativeBattleStickyWeb153Drawing.Strips(first, second, view, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x100);
        NativeBattleGfGeometryBatch captured = NativeBattleStickyWeb153Drawing.Strips(first, second, view, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x100, owner);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(28, captured.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        Assert.All(captured.Attempts, attempt => Assert.True(attempt.Primitive!.PilotGeometry!.UseSourceDepths));
        Assert.Equal(captured.Attempts[0].Primitive!.PilotGeometry!.A, captured.Attempts[1].Primitive!.PilotGeometry!.A);
    }

    [Fact]
    public void Clipped_web_captures_actual_emitted_vertices_with_unchanged_bytes_and_masks()
    {
        byte[] data = new byte[68];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data, 32);
        NativeBattleVector[] vertices = [new(-100, 100, 1000), new(100, 100, 1000), new(0, 200, 1000)];
        for (int index = 0; index < vertices.Length; index++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8 + index * 8), vertices[index].X);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10 + index * 8), vertices[index].Y);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + index * 8), vertices[index].Z);
        }
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(40), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(44), 0x24808080);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(50), 2);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(52), 4);
        NativeBattleStickyWeb153Resources resources = new(new(0, 0x123400, data.ToImmutableArray()), null!, null!, null!, 0, []);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleGfGeometryBatch Run(NativeBattlePilotVisualIdentity? identity) => NativeBattleStickyWeb153Drawing.Clipped(resources, view, projection, 0, 0x155, 0x100, 0, 0, identity);
        NativeBattleGfGeometryBatch native = Run(null);
        NativeBattleGfGeometryBatch captured = Run(new(153, 1, 2, 3, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(3, geometry.CornerCount);
        Assert.Equal(new System.Numerics.Vector3(-100, 100, 1000), geometry.A);
    }

    [Fact]
    public void Rejected_strands_keep_their_attempted_tag_and_native_trailing_reservation()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, -100, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        ImmutableArray<NativeBattleVector> samples = Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 16).ToImmutableArray();
        NativeBattleGfGeometryBatch batch = NativeBattleStickyWeb153Drawing.Strips(samples, samples, matrix, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x100);
        Assert.Equal(36, batch.AllocationBytes);
        Assert.Equal(14, batch.Attempts.Length);
        Assert.All(batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 36);
        NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(arena, "243BE6C", 0, 0x10000, NativeBattleLogicalOrdering.Clear(), batch, 36);
        Assert.Equal(36, written.Cursor);
        Assert.Equal(new byte[] { 0, 0, 0, 8 }, written.Arena.Read(0, 4));
        Assert.Throws<NotSupportedException>(() => written.Arena.Read(4, 32));
    }

    [Fact]
    public void Accepted_strands_link_both_color_planes_and_reserve_an_unwritten_tail()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        ImmutableArray<NativeBattleVector> first = Enumerable.Range(0, 16).Select(index => new NativeBattleVector((short)(index * 10), -50, 0)).ToImmutableArray();
        ImmutableArray<NativeBattleVector> second = first.Select(point => point with { Y = 50 }).ToImmutableArray();
        NativeBattleGfGeometryBatch batch = NativeBattleStickyWeb153Drawing.Strips(first, second, matrix, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x100);
        Assert.Equal(28, batch.Attempts.Length);
        Assert.Equal(14 * 72 + 36, batch.AllocationBytes);
        Assert.Equal(0x3A000000u, batch.Attempts[1].Primitive!.Words[1]);
        NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(NativeBattleEffectArena.Clear().MarkUnknown(0, 1100), "243BE6C", 0, 0x10000, NativeBattleLogicalOrdering.Clear(), batch, 36);
        Assert.Equal(28, written.Ordering.Proxies.Length);
        Assert.Throws<NotSupportedException>(() => written.Arena.Read(14 * 72, 36));
    }
}
