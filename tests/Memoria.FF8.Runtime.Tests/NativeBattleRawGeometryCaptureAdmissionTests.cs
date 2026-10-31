using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRawGeometryCaptureAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fully_native_rejected_zero_view_does_not_require_world_inverse_and_keeps_original_projection_scratch(bool expanded)
    {
        NativeBattleActionMatrix zero = new([0, 0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 4096);
        NativeBattleCameraProjection projection = new(zero, 512, 160, 108);
        NativeBattleGeometryViewport? viewport = expanded ? new(short.MinValue / 8, short.MinValue / 8, ushort.MaxValue / 8, ushort.MaxValue / 8) : null;
        NativeBattleRawMesh mesh = Mesh();
        NativeBattleProjectedVertex[]? nativeScratch = null;
        NativeBattleProjectedVertex[]? capturedScratch = null;
        int nativePublications = 0;
        int capturedPublications = 0;
        ImmutableArray<NativeBattleGeometryPrimitive> native = NativeBattleRawGeometryPackets.Emit5099D0([mesh], [zero], projection, 0x808080, 1, null, 17, 4, null, publishProjectedScratch: vertices =>
        {
            nativePublications++;
            nativeScratch = vertices.ToArray();
        }, viewport: viewport);
        ImmutableArray<NativeBattleGeometryPrimitive> captured = NativeBattleRawGeometryPackets.Emit5099D0([mesh], [zero], projection, 0x808080, 1, null, 17, 4, null, publishProjectedScratch: vertices =>
        {
            capturedPublications++;
            capturedScratch = vertices.ToArray();
        }, viewport: viewport, pilotIdentity: new(0, 0x502D40, 0, 3, 0));
        Assert.Empty(native);
        Assert.Empty(captured);
        Assert.Equal(1, nativePublications);
        Assert.Equal(nativePublications, capturedPublications);
        Assert.NotNull(nativeScratch);
        Assert.NotNull(capturedScratch);
        Assert.Equal(nativeScratch, capturedScratch);
        Assert.All(capturedScratch, vertex =>
        {
            Assert.Equal((ushort)0, vertex.Depth);
            Assert.NotEqual(0u, vertex.DepthAndClip & 0x20000u);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Accepted_corners_keep_original_worlds_and_all_native_packets_after_deferred_capture(bool expanded)
    {
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { X = 10 };
        NativeBattleActionMatrix composed = NativeBattleCuragaMatrices.Identity8DD770() with { X = 110, Z = 1000 };
        NativeBattleCameraProjection projection = new(view, 512, 160, 108);
        NativeBattleGeometryViewport? viewport = expanded ? new(short.MinValue / 8, short.MinValue / 8, ushort.MaxValue / 8, ushort.MaxValue / 8) : null;
        NativeBattleRawMesh mesh = Mesh();
        NativeBattleProjectedVertex[]? nativeScratch = null;
        NativeBattleProjectedVertex[]? capturedScratch = null;
        ImmutableArray<NativeBattleGeometryPrimitive> native = NativeBattleRawGeometryPackets.Emit5099D0([mesh], [composed], projection, 0x808080, 1, null, 17, 4, null, publishProjectedScratch: vertices => nativeScratch = vertices.ToArray(), viewport: viewport);
        ImmutableArray<NativeBattleGeometryPrimitive> captured = NativeBattleRawGeometryPackets.Emit5099D0([mesh], [composed], projection, 0x808080, 1, null, 17, 4, null, publishProjectedScratch: vertices => capturedScratch = vertices.ToArray(), viewport: viewport, pilotIdentity: new(0, 0x502D40, 0, 3, 0));
        Assert.Equal(2, native.Length);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(nativeScratch, capturedScratch);
        Assert.All(native, primitive => Assert.Null(primitive.PilotGeometry));
        Assert.All(captured, primitive =>
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.False(visual.ScreenSpace);
            Assert.True(visual.Continuous);
            Assert.Equal(new Vector3(68, -16, 1000), visual.A);
            Assert.Equal(new Vector3(132, -16, 1000), visual.B);
            Assert.Equal(new Vector3(68, 16, 1000), visual.C);
            if (visual.CornerCount == 4)
            {
                Assert.Equal(new Vector3(132, 16, 1000), visual.D);
            }
        });
    }

    [Fact]
    public void Accepted_geometry_still_requires_a_supported_actual_world_transform()
    {
        NativeBattleActionMatrix zeroView = new([0, 0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 4096);
        NativeBattleActionMatrix nativeMatrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 1000 };
        NativeBattleCameraProjection projection = new(zeroView, 512, 160, 108);
        NativeBattleRawMesh mesh = Mesh();
        Assert.Equal(2, NativeBattleRawGeometryPackets.Emit5099D0([mesh], [nativeMatrix], projection, 0x808080, 1, null, 17, 4, null).Length);
        Assert.Throws<InvalidOperationException>(() => NativeBattleRawGeometryPackets.Emit5099D0([mesh], [nativeMatrix], projection, 0x808080, 1, null, 17, 4, null, pilotIdentity: new(0, 0x502D40, 0, 3, 0)));
    }

    private static NativeBattleRawMesh Mesh()
    {
        NativeBattleRawPolygon Face(bool quad)
        {
            byte[] bytes = new byte[quad ? 20 : 16];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2);
            if (quad)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 3);
            }
            return new(quad, bytes.ToImmutableArray());
        }
        return new([new(new(-32, -16, 0), 0), new(new(32, -16, 0), 0), new(new(-32, 16, 0), 0), new(new(32, 16, 0), 0)], [Face(false), Face(true)]);
    }
}
