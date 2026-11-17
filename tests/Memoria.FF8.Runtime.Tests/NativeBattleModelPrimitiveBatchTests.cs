using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelPrimitiveBatchTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(Identity, 1000, 160, 108);
    private static readonly NativeBattleGeometryViewport Viewport = new(-100, -100, 420, 316);
    private static readonly NativeBattlePilotVisualIdentity Pilot = new(0, 0x502D40, 11, 7, 0);
    private static readonly NativeBattleModelTextureOwner Material = new("party-10");

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Model_pages_preserve_packet_pilot_and_material_values_across_page_boundaries(bool quad, bool capture)
    {
        using IDisposable? scope = capture ? NativeBattleSceneCapture.Begin(NativeBattleSceneCaptureKind.Image) : null;
        NativeBattleRawMesh mesh = Mesh(quad, 130);
        ImmutableArray<NativeBattleGeometryPrimitive> expected = EmitObjects(mesh);
        NativeBattleModelPrimitiveBatch actual = EmitBatch(mesh);
        Assert.Equal(130, actual.Count);
        Assert.Equal(expected.Length, actual.Count);
        for (int index = 0; index < actual.Count; index++)
        {
            NativeBattleGeometryPrimitive primitive = expected[index];
            NativeBattleModelPrimitive value = actual[index];
            Assert.Equal(primitive.Bucket, value.Bucket);
            Assert.True(primitive.Words.AsSpan().SequenceEqual(value.Words.AsSpan()));
            Assert.True(primitive.VertexPayload4.AsSpan().SequenceEqual(value.VertexPayload4.AsSpan()));
            Assert.True(primitive.ProjectedCoordinates!.Value.AsSpan().SequenceEqual(value.ProjectedCoordinates!.Value.AsSpan()));
            Assert.Equal(primitive.PhysicalWriteMask, value.PhysicalWriteMask);
            Assert.Equal(primitive.PilotGeometry, value.PilotGeometry);
            Assert.Same(primitive.ModelTextureOwner, value.ModelTextureOwner);
            Assert.Same(primitive.SceneCapture, value.SceneCapture);
            Assert.Equal(primitive.PresentationActorId, value.PresentationActorId);
        }
    }

    [Fact]
    public void Retained_model_pages_survive_suspended_journal_replay_and_later_hosted_writes()
    {
        NativeBattleModelPrimitiveBatch first = EmitBatch(Mesh(false, 65));
        NativeBattleModelPrimitiveBatch second = EmitBatch(Mesh(true, 70));
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattleHostedModelPackets storage = new();
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ImmutableArray<NativeBattleLogicalCommand> before;
        ImmutableArray<NativeBattleLogicalCommand> replayed;
        using (NativeBattleLogicalOrderingTransaction transaction = new(ordering))
        {
            transaction.BeginCheckpoint();
            storage.Append(process, transaction.View, first);
            before = transaction.View.Traverse(Lengths());
            NativeBattleLogicalOrdering.Delta delta = transaction.SuspendCheckpointDelta();
            Assert.Empty(transaction.View.Traverse(Lengths()));
            storage.ResetFrame();
            storage.Append(process, NativeBattleLogicalOrdering.Clear(), second);
            transaction.ResumeDelta(delta);
            replayed = transaction.Freeze().Traverse(Lengths());
        }
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(replayed));
        for (int index = 0; index < replayed.Length; index++)
        {
            Assert.Equal(before[index].Pilot, replayed[index].Pilot);
            Assert.True(replayed[index].Pilot.IsModel);
            Assert.True(first[first.Count - index - 1].Words.AsSpan()[1..].SequenceEqual(replayed[index].WordSpan));
        }
        NativeBattleLogicalOrdering detached = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering)))!;
        Assert.Equal(JsonSerializer.Serialize(replayed), JsonSerializer.Serialize(detached.Traverse(Lengths())));
    }

    [Fact]
    public void Published_model_pages_cannot_be_changed_by_their_builder()
    {
        NativeBattleModelPrimitiveBatch.Builder builder = new();
        int emission = builder.BeginEmission(new(null, null, null, Pilot, false, false));
        uint[] words = [0x04000000, 0x200000FF, 0, 8, 8u << 16];
        builder.AddRaw(23, words, [], [], [], default, emission);
        NativeBattleModelPrimitiveBatch frozen = builder.Freeze();
        Assert.Throws<InvalidOperationException>(() => builder.AddRaw(23, words, [], [], [], default, emission));
        words[1] = 0;
        Assert.Equal(0x200000FFu, frozen.ReadWords(0)[1]);
    }

    [Fact]
    public void Model_emission_reduces_allocated_bytes_without_dropping_pilot_geometry()
    {
        NativeBattleRawMesh mesh = Mesh(false, 2048);
        Func<object> objects = () => EmitObjects(mesh);
        Func<object> pages = () => EmitBatch(mesh);
        for (int iteration = 0; iteration < 3; iteration++)
        {
            GC.KeepAlive(objects());
            GC.KeepAlive(pages());
        }
        long objectBytes = Measure(objects);
        long pageBytes = Measure(pages);
        Assert.True(pageBytes < objectBytes - 80 * mesh.Polygons.Length, $"Model pages allocated {pageBytes} bytes; object packets allocated {objectBytes} bytes.");
        Assert.True(EmitBatch(mesh)[2047].Pilot.IsModel);
    }

    private static long Measure(Func<object> emit)
    {
        long start = GC.GetAllocatedBytesForCurrentThread();
        object result = emit();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(result);
        return bytes;
    }

    private static NativeBattleModelPrimitiveBatch EmitBatch(NativeBattleRawMesh mesh)
    {
        return NativeBattleRawGeometryPackets.EmitModel5099D0([mesh], [Identity], Projection, 0x808080, 1, null, 17, 4, null, viewport: Viewport, pilotIdentity: Pilot, actorId: "party-10", material: Material);
    }

    private static ImmutableArray<NativeBattleGeometryPrimitive> EmitObjects(NativeBattleRawMesh mesh)
    {
        return NativeBattleRawGeometryPackets.Emit5099D0([mesh], [Identity], Projection, 0x808080, 1, null, 17, 4, null, viewport: Viewport, pilotIdentity: Pilot, actorId: "party-10", material: Material);
    }

    private static NativeBattleRawMesh Mesh(bool quad, int count)
    {
        byte[] polygon = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(6), 3);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(14), 0x80);
        return new([new(new(-10, -10, 1000), 0), new(new(10, -10, 1000), 0), new(new(-10, 10, 1000), 0), new(new(10, 10, 1000), 0)], Enumerable.Repeat(new NativeBattleRawPolygon(quad, polygon.ToImmutableArray()), count).ToImmutableArray());
    }

    private static ImmutableArray<byte> Lengths()
    {
        return new byte[256].ToImmutableArray().SetItem(0x24, 7).SetItem(0x2C, 9);
    }
}
