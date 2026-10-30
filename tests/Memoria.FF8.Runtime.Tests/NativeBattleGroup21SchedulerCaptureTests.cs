using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup21SchedulerCaptureTests
{
    [Fact]
    public void Colored_mesh_empty_source_keeps_native_prefix_without_reading_unknown_unused_matrix()
    {
        NativeBattleEffect326Controller.LateColoredMeshStep Run(bool capture)
        {
            byte[] bytes = new byte[48];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, 16);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
            var source = new NativeBattleStaticMeshResource(0, 0xE18798, bytes.ToImmutableArray());
            var memory = NativeBattleEffect167BodyDrawingTests.Memory() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() };
            var context = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(source.Address)).Write(0x1C, BitConverter.GetBytes(0xF3u)).Write(0x20, BitConverter.GetBytes(0x808080u));
            return NativeBattleEffect326Controller.LateColoredMesh108.Draw6314A0(memory, source, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: capture ? new(326, 1, 2, 3, 4) : null);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Empty(captured.Batch.Attempts);
        Assert.Equal(0, captured.Batch.AllocationBytes);
    }


    [Theory]
    [InlineData(256, 32)]
    [InlineData(512, 16)]
    public void Opening_grid_keeps_actual_row_column_corners_partial_FIFO_and_native_ledger(int size, int count)
    {
        NativeBattleEffect326OpeningGridStep Run(bool capture)
        {
            var memory = NativeBattleEffect167BodyDrawingTests.Memory(20000);
            var context = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(size)).Write(4, BitConverter.GetBytes(32)).Write(8, BitConverter.GetBytes(count)).Write(0x74, BitConverter.GetBytes(0x404040));
            return NativeBattleEffect326OpeningGrid.Draw62B1C0(memory, context, new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), capture ? new(326, 1, 2, 3, 4) : null);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        var polygons = captured.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.NotEmpty(polygons);
        Assert.Equal(polygons.Length, polygons.Select(primitive => primitive.PilotGeometry!.Identity).Distinct().Count());
        Assert.Equal(count * count, polygons.Length);
        int origin = -(count / 2) * size;
        for (int index = 0; index < polygons.Length; index++)
        {
            var primitive = polygons[index];
            var geometry = primitive.PilotGeometry!;
            int left = origin + index % count * size;
            int top = origin + index / count * size;
            Assert.Equal(new System.Numerics.Vector3(left, 0, 20000 + top), geometry.A);
            Assert.Equal(new System.Numerics.Vector3(left + size, 0, 20000 + top), geometry.B);
            Assert.Equal(new System.Numerics.Vector3(left, 0, 20000 + top + size), geometry.C);
            Assert.Equal(new System.Numerics.Vector3(left + size, 0, 20000 + top + size), geometry.D);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
        }
    }

    [Fact]
    public void Nested_scheduler_replaces_stale_input_owner_and_repeated_callbacks_keep_actual_child_identity()
    {
        (NativeBattleEffect326Frame Frame, List<NativeBattleGfGeometryBatch> Batches) Run(bool capture)
        {
            var pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
            for (int index = 0; index < 2; index++)
            {
                var root = pools[0].Append(RootCallback);
                pools = pools.SetItem(0, root.Pool.WriteSlot(root.Slot, root.Pool.Slots[root.Slot].WriteWord(12, 0)));
                var child = pools[1].Append(0x62CD40);
                pools = pools.SetItem(1, child.Pool.WriteSlot(child.Slot, child.Pool.Slots[child.Slot].WriteBytes(12, new byte[24])));
            }
            var effect = new NativeBattleOriginalEffectState(Effect, Effect - 1, new(Effect, Entry, Initializer), null, RootDescriptor) { Pools = pools };
            var frame = new NativeBattleEffect326Frame(NativeBattleEffectMemory.FromColdProcess(), effect, new(123), NativeBattleEffect326State.FromColdProcess().DetachBank(0).DetachBank(1));
            List<NativeBattleGfGeometryBatch> batches = [];
            NativeBattleEffect326Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, _ => 0, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, (current, operation) =>
            {
                batches.Add(Assert.IsType<NativeBattleEffect326DeliverBatch>(operation).Batch);
                return current;
            }) { CapturePresentation = capture, PresentationTask = new(326, 0xDEADBEEF, 99, 777, 888) };
            return (NativeBattleEffect326Controller.Advance(frame, null!, inputs), batches);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batches), NativeBattleTestSnapshots.Json(captured.Batches));
        Assert.Equal(4, captured.Batches.Count);
        var identities = captured.Batches.Select(batch => Assert.Single(batch.Attempts).Primitive!.PilotGeometry!.Identity).ToArray();
        Assert.Equal(2, identities.Distinct().Count());
        Assert.All(identities, identity => Assert.Equal(ChildDescriptor, identity.Bank));
        Assert.Equal(new[] { 0, 1, 0, 1 }, identities.Select(identity => identity.Slot));
        Assert.All(identities, identity => Assert.NotEqual(777, identity.Generation));
        Assert.All(native.Batches.SelectMany(batch => batch.Attempts), attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
    }
}
