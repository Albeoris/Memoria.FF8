using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup20HistoryCaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Frozen174_skewed_history_retains_native_rounded_triangle_and_quad_payloads(bool quad)
    {
        byte[] polygon = new byte[quad ? 20 : 16];
        for (int corner = 0; corner < (quad ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(corner * 2), (ushort)(corner == 0 ? 0x8000 : corner));
        }
        var mesh = new NativeBattleRawMesh([new(new(-10, -10, 1000), 0), new(new(10, -10, 1000), 0), new(new(0, 10, 1000), 0), new(new(10, 10, 1000), 0)], [new(quad, polygon.ToImmutableArray())]);
        var view = NativeBattleCuragaMatrices.Identity8DD770();
        var bone = new NativeBattleActionMatrix([4096, 0, 0, 0, 4096, 0, 5, 7, 4096], 0, 0, 0, 4096);
        var projection = new NativeBattleCameraProjection(view, 1000, 160, 108);
        var colors = new NativeBattleSummonColors(0, 0, 1f / 4096, 6755401588539392d);
        var native = NativeBattleRawModelBodyDrawing.Draw5099D0([mesh], [bone], projection, 0x808080, 1, new byte[32].ToImmutableArray(), colors);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = NativeBattleRawModelBodyDrawing.Draw5099D0([mesh], [bone], projection, 0x808080, 1, new byte[32].ToImmutableArray(), colors, pilotIdentity: new(174, 1, 2, 3, 4), pilotContinuous: false);
        captured = captured with { Packets = captured.Packets with { Attempts = captured.Packets.Attempts.Select(attempt => attempt with { Primitive = NativeBattleEffect174Presentation.PreserveNativeDepths(attempt.Primitive!) }).ToImmutableArray() } };
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        var primitive = Assert.Single(captured.Packets.Attempts).Primitive!;
        var geometry = primitive.PilotGeometry!;
        Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
        Assert.True(geometry.UseSourceDepths);
        Assert.False(geometry.Continuous);
        Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
        Assert.NotEqual(MathF.Truncate(geometry.A.Z), geometry.A.Z);
    }

    [Fact]
    public void Frozen174_depth_boundary_accepts_actual_empty_and_rejects_unsupported_payload_lengths()
    {
        var identity = new NativeBattlePilotVisualIdentity(174, 1, 2, 3, 4);
        var primitive = new NativeBattleGeometryPrimitive(17, [], []) { PilotGeometry = new(identity, default, default, default, default, 3) };
        var empty = NativeBattleEffect174Presentation.PreserveNativeDepths(primitive);
        Assert.True(empty.PilotGeometry!.UseSourceDepths);
        Assert.Equal(System.Numerics.Vector4.Zero, empty.PilotGeometry.SourceDepths);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect174Presentation.PreserveNativeDepths(primitive with { VertexPayload4 = [1, 2, 3] }));
    }

    [Fact]
    public void Frozen174_history_keeps_native_body_packets_masks_depth_and_324_default_contract()
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        var mesh = new NativeBattleRawMesh([new(new(-10, -10, 1000), 0), new(new(10, -10, 1000), 0), new(new(0, 10, 1000), 0)], [new(false, polygon.ToImmutableArray())]);
        var view = NativeBattleCuragaMatrices.Identity8DD770();
        var projection = new NativeBattleCameraProjection(view, 1000, 160, 108);
        var colors = new NativeBattleSummonColors(0, 0, 1f / 4096, 6755401588539392d);
        var native = NativeBattleRawModelBodyDrawing.Draw5099D0([mesh, mesh], [view], projection, 0x808080, 3, new byte[24].ToImmutableArray(), colors);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var omitted324 = NativeBattleRawModelBodyDrawing.Draw5099D0([mesh, mesh], [view], projection, 0x808080, 3, new byte[24].ToImmutableArray(), colors, pilotIdentity: null, pilotContinuous: true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(omitted324));
        var identity = new NativeBattlePilotVisualIdentity(174, 0x2395E58, 0, 123, 0) { ResourceGeneration = 0x193F000 };
        var captured = NativeBattleRawModelBodyDrawing.Draw5099D0([mesh, mesh], [view], projection, 0x808080, 3, new byte[24].ToImmutableArray(), colors, pilotIdentity: identity, pilotContinuous: false);
        captured = captured with { Packets = captured.Packets with { Attempts = captured.Packets.Attempts.Select(attempt => attempt with { Primitive = NativeBattleEffect174Presentation.PreserveNativeDepths(attempt.Primitive!) }).ToImmutableArray() } };
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(2, captured.Packets.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        Assert.All(captured.Packets.Attempts, attempt =>
        {
            var geometry = attempt.Primitive!.PilotGeometry!;
            Assert.False(geometry.Continuous);
            Assert.Equal((ushort)174, geometry.Identity.Effect);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(new System.Numerics.Vector3(-10, -10, 1000), geometry.A);
        });
    }
}
