using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignModelDeformationTests
{
    private sealed record Material : INativeBattleForeignMaterial;
    private static readonly NativeBattleActionMatrix Identity = NativeBattleForeignModelGeometry.Identity;
    private static readonly NativeBattlePilotVisualIdentity IdentityTag = new(124, 1, 0, 1, 0);

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    public void Projected_ripple_keeps_native_packets_and_radius_feedback_and_host_material(short radius)
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        NativeBattleCameraProjection projection = new(Identity with { Z = 1024 }, 256, 160, 108);
        byte[] data = new byte[0x12652A4 - (int)NativeBattleFlare124Resources.BaseAddress];
        data.AsSpan(0x125DEA0 - (int)NativeBattleFlare124Resources.BaseAddress, 0x4000).Fill(16);
        for (int index = 0; index < 128; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x1261EA0 - (int)NativeBattleFlare124Resources.BaseAddress + index * 2), 2048);
        }
        NativeBattleFlare124Resources resources = new(data.ToImmutableArray());
        NativeBattleFlare124ActorDraw native = NativeBattleFlare124ActorPackets.Emit(geometry.Faces[0].Triangle(), [projection.View!], projection, 0x808080, 160, 108, radius, 0, 0, 0, resources, 341, 256);
        short extent = 0;
        NativeBattleGfGeometryBatch foreign = NativeBattleForeignModelDrawing.Deform(geometry, projection, Deform, (mesh, points, identity) => NativeBattleFlare124ActorPackets.EmitProjected(mesh.Polygons, points.Select(point => new NativeBattleModuleProjectedVertex(point.Screen, unchecked((ushort)point.DepthAndClip), point.DepthAndClip >> 16)).ToArray(), 0x808080, 341, 256, identity), IdentityTag);
        NativeBattleGeometryPrimitive actual = Assert.Single(foreign.Attempts).Primitive!;
        NativeBattleGeometryPrimitive expected = Assert.Single(native.Packets.Attempts).Primitive!;
        Assert.Equal(expected.Words, actual.Words);
        Assert.Equal(expected.Bucket, actual.Bucket);
        Assert.Equal(expected.VertexPayload4, actual.VertexPayload4);
        Assert.Equal(native.Radius, extent);
        Assert.Equal((short)16, extent);
        Assert.Same(geometry.Faces[0].Surface, actual.PilotGeometry!.ForeignSurface);
        Assert.True(actual.PilotGeometry.ScreenSpace);

        NativeBattleProjectedVertex Deform(NativeBattleModuleProjectedVertex point)
        {
            NativeBattleModuleProjectedVertex changed = NativeBattleFlare124ActorPackets.Ripple(point, 160, 108, radius, 0, 0, 0, resources, out short current);
            extent = Math.Max(extent, current);
            return new(changed.Screen, changed.Depth | changed.Flags << 16);
        }
    }

    [Fact]
    public void Local_wave_precedes_actor_world_and_camera_and_preserves_retained_work_and_material()
    {
        NativeBattleForeignModelGeometry geometry = Geometry() with { World = Identity with { X = 200, Y = -100, Z = 100 } };
        NativeBattleCameraProjection projection = new(Identity with { Z = 1024 }, 256, 160, 108);
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(projection.View!, geometry.World);
        NativeBattleEffectArena work = NativeBattleEffectArena.Clear().MarkUnknown(0, 64);
        NativeBattleBio22ActorDraw expected = NativeBattleBio22ActorPackets.Emit89D780(geometry.Faces[0].Triangle(), [Identity], camera, projection, 0x808080, 1024, 4096, 341, 256, work, 0, pilotIdentity: IdentityTag);
        NativeBattleGfGeometryBatch foreign = NativeBattleForeignModelDrawing.DeformLocal(geometry, projection, point => NativeBattleBio22ActorPackets.DeformVertex(point, 1024, 4096), Emit, IdentityTag);
        NativeBattleGeometryPrimitive actual = Assert.Single(foreign.Attempts).Primitive!;
        NativeBattleGeometryPrimitive native = Assert.Single(expected.Packets.Attempts).Primitive!;
        Assert.Equal(native.Words, actual.Words);
        Assert.Equal(native.Bucket, actual.Bucket);
        Assert.Equal(native.VertexPayload4, actual.VertexPayload4);
        Assert.Equal(native.PilotGeometry!.A, actual.PilotGeometry!.A);
        Assert.Equal(native.PilotGeometry.B, actual.PilotGeometry.B);
        Assert.Equal(native.PilotGeometry.C, actual.PilotGeometry.C);
        Assert.Same(geometry.Faces[0].Surface, actual.PilotGeometry.ForeignSurface);
        Assert.Throws<NotSupportedException>(() => work.Read(0, 4));
        Assert.Throws<NotSupportedException>(() => work.Read(10, 6));
        Assert.Equal(expected.Work.Read(4, 6), work.Read(4, 6));

        NativeBattleGfGeometryBatch Emit(NativeBattleRawMesh mesh, NativeBattleActionMatrix selectedCamera, NativeBattlePilotVisualIdentity identity)
        {
            NativeBattleBio22ActorDraw drawn = NativeBattleBio22ActorPackets.Emit89D780(mesh, [Identity], selectedCamera, projection, 0x808080, 0, 0, 341, 256, work, 0, pilotIdentity: identity);
            work = drawn.Work;
            return drawn.Packets;
        }
    }

    private static NativeBattleForeignModelGeometry Geometry()
    {
        NativeBattleForeignSurface surface = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        return new(Identity, new(default, default, 0, new(new(0, 0, 0), new(64, 64, 0))), [new(new(0, 0, 0), new(64, 0, 0), new(0, 64, 0), surface)]);
    }
}
