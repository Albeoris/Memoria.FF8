using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRawModelBodyDrawingTests
{
    [Fact]
    public void Group17_actor_animation_body_capture_retains_actual232_slot_object_source_and_native_packets()
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([new(new(-10, -10, 1000), 0), new(new(10, -10, 1000), 0), new(new(0, 10, 1000), 0)], [new(false, polygon.ToImmutableArray())]);
        NativeBattleCameraProjection projection = new(Identity, 1000, 160, 108);
        var frame = WaterSchedulerFixture.Create();
        var bank = new NativeBattleWaterSlotBank(0, 0x4000, 2, 0x2000);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, bank, 0, 0, 0, 0) };
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(232, frame, bank, 0) with { ResourceGeneration = 0x193F000 };
        var native = NativeBattleRawModelBodyDrawing.Draw5099D0([mesh, mesh], [Identity], projection, 0x808080, 3, new byte[24].ToImmutableArray(), Colors);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var actual = NativeBattleRawModelBodyDrawing.Draw5099D0([mesh, mesh], [Identity], projection, 0x808080, 3, new byte[24].ToImmutableArray(), Colors, pilotIdentity: identity);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(2, actual.Packets.Attempts.Length);
        Assert.Equal(2, actual.Packets.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        foreach (var packet in actual.Packets.Attempts)
        {
            var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(packet.Primitive!.PilotGeometry);
            Assert.Equal((ushort)232, visual.Identity.Effect);
            Assert.Equal(identity.Generation, visual.Identity.Generation);
            Assert.Equal(identity.ResourceGeneration, visual.Identity.ResourceGeneration);
            Assert.Equal(new System.Numerics.Vector3(-10, -10, 1000), visual.A);
        }
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, bank, 0, 0, 0, 0) };
        var recycled = NativeBattleCinematicVisualIdentity.Identity(232, frame, bank, 0);
        Assert.NotEqual(identity.Generation, recycled.Generation);
        Assert.NotEqual(identity, identity with { ResourceGeneration = 0x193F100 });
    }

    [Fact]
    public void Explicit324_saved_pose_body_keeps_native_scratch_packets_and_actual_world_depth_sidecar()
    {
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([new(new(-20, -20, 0), 0), new(new(20, -20, 0), 0), new(new(0, 20, 0), 0)], [new(false, face.ToImmutableArray())]);
        NativeBattleActionMatrix bone = Identity with { Z = 4000 };
        NativeBattleCameraProjection projection = new(Identity, 512, 160, 104);
        var scratch = new byte[24].ToImmutableArray();
        var plain = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [bone], projection, 0x123456, 1, scratch, Colors);
        string expected = System.Text.Json.JsonSerializer.Serialize(plain);
        NativeBattlePilotVisualIdentity identity = new(324, 0x2202E28, 0, 17, 2) { ResourceGeneration = 0x193F000 };
        var captured = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [bone], projection, 0x123456, 1, scratch, Colors, pilotIdentity: identity);
        Assert.Equal(expected, System.Text.Json.JsonSerializer.Serialize(captured));
        var packet = Assert.Single(NativeBattleActor33Presentation.Depths(captured.Packets).Attempts).Primitive!;
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(packet.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-20, -20, 4000), visual.A);
        Assert.Equal(new System.Numerics.Vector4(packet.VertexPayload4[0], packet.VertexPayload4[1], packet.VertexPayload4[2], packet.VertexPayload4[3]), visual.SourceDepths!.Value);
        Assert.False(visual.ScreenSpace);
        Assert.True(visual.Continuous);
        Assert.True(visual.UseSourceDepths);
    }

    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleSummonColors Colors = new(0, 0, 1f / 4096, 6755401588539392d);

    [Fact]
    public void Selected_objects_reuse_projection_prefix_preserve_previous_tail_and_shift_masks_per_object()
    {
        NativeBattleRawMesh first = new([new(new(10, 20, 1000), 0), new(new(30, 40, 1000), 0)], []);
        NativeBattleRawMesh second = new([new(new(50, 60, 1000), 0)], []);
        NativeBattleCameraProjection projection = new(null, 1000, 0, 0);
        ImmutableArray<byte> initial = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray();
        NativeBattleRawModelBodyDraw firstOnly = NativeBattleRawModelBodyDrawing.Draw5099D0(first, [Identity], projection, 0, 1, initial, Colors);
        NativeBattleRawModelBodyDraw secondOnly = NativeBattleRawModelBodyDrawing.Draw5099D0(second, [Identity], projection, 0, 1, initial, Colors);
        NativeBattleRawModelBodyDraw both = NativeBattleRawModelBodyDrawing.Draw5099D0([first, second], [Identity], projection, 0, 3, initial, Colors);
        Assert.Equal(16, both.ProjectedBytesWritten);
        Assert.Equal(secondOnly.ProjectedScratch.AsSpan().Slice(0, 8).ToArray(), both.ProjectedScratch.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal(firstOnly.ProjectedScratch.AsSpan().Slice(8, 8).ToArray(), both.ProjectedScratch.AsSpan().Slice(8, 8).ToArray());
        Assert.All(both.ProjectedScratch.AsSpan().Slice(16).ToArray(), value => Assert.Equal(0xA5, value));
        NativeBattleRawModelBodyDraw masked = NativeBattleRawModelBodyDrawing.Draw5099D0([first, second], [Identity], projection, 0, 2, initial, Colors);
        Assert.Equal(8, masked.ProjectedBytesWritten);
        Assert.Equal(secondOnly.ProjectedScratch, masked.ProjectedScratch);
        NativeBattleRawModelBodyDraw none = NativeBattleRawModelBodyDrawing.Draw5099D0([first, second], [Identity], projection, 0, 0, initial, Colors);
        Assert.Equal(0, none.ProjectedBytesWritten);
        Assert.Equal(initial, none.ProjectedScratch);
    }

    [Fact]
    public void Explicit_embedded_viewport_clips_208_without_changing_existing_default_216()
    {
        NativeBattleRawMesh mesh = new([new(new(0, 210, 1000), 0)], []);
        NativeBattleCameraProjection projection = new(null, 1000, 0, 0);
        NativeBattleRawModelBodyDraw old = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Identity], projection, 0, 1, new byte[8].ToImmutableArray(), Colors);
        NativeBattleRawModelBodyDraw embedded = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Identity], projection, 0, 1, new byte[8].ToImmutableArray(), Colors, viewport: new(0, 0, 320, 208));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(old.ProjectedScratch.AsSpan().Slice(4)) & 0x2000000);
        Assert.Equal(0x2000000u, BinaryPrimitives.ReadUInt32LittleEndian(embedded.ProjectedScratch.AsSpan().Slice(4)) & 0x2000000);
        Assert.Equal(old.ProjectedScratch.AsSpan().Slice(0, 4).ToArray(), embedded.ProjectedScratch.AsSpan().Slice(0, 4).ToArray());
        Assert.Equal(8, embedded.ProjectedBytesWritten);
    }

    [Fact]
    public void Actual_ordering_table_base_is_forwarded_without_rewriting_packet_bytes()
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([new(new(-10, -10, 1000), 0), new(new(10, -10, 1000), 0), new(new(0, 10, 1000), 0)], [new(false, polygon.ToImmutableArray())]);
        NativeBattleCameraProjection projection = new(null, 1000, 160, 108);
        NativeBattleRawModelBodyDraw old = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Identity], projection, 0x808080, 1, new byte[24].ToImmutableArray(), Colors);
        NativeBattleRawModelBodyDraw embedded = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Identity], projection, 0x808080, 1, new byte[24].ToImmutableArray(), Colors, viewport: new(0, 0, 320, 208), tableBaseBucket: 51, depthCue: -17);
        NativeBattleGeometryPrimitive original = Assert.Single(old.Packets.Attempts).Primitive!;
        NativeBattleGeometryPrimitive actual = Assert.Single(embedded.Packets.Attempts).Primitive!;
        Assert.Equal(original.Bucket + 34, actual.Bucket);
        Assert.Equal(original.Words, actual.Words);
        Assert.Equal(old.ProjectedScratch, embedded.ProjectedScratch);
    }
}
