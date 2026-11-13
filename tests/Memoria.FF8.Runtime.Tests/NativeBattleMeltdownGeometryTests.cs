using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeltdownGeometryTests
{
    private sealed record Material : INativeBattleForeignMaterial;

    [Theory]
    [InlineData(0)]
    [InlineData(2000)]
    public void Posed_host_world_vertices_use_native_deformation_packets_and_keep_their_material(int strength)
    {
        NativeBattleActionMatrix identity = NativeBattleForeignModelGeometry.Identity;
        NativeBattleForeignSurface surface = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattleForeignModelFace face = new(new(-100, -100, 0), new(100, -100, 0), new(0, 100, 0), surface);
        NativeBattleForeignModelGeometry geometry = new(identity with { X = 70, Y = -300, Z = 200 }, new(default, default, 0, new(default, default)), [face]);
        NativeBattleCameraProjection projection = new(identity with { Z = 4096 }, 216, 160, 108);
        NativeBattleModuleAnchor target = new(70, -300, 200, 0);
        NativeBattleModuleAnchor caster = new(0, -2000, 1000, 0);
        NativeBattlePilotVisualIdentity tag = new(148, 0x612540, 3, 0, 0);
        NativeBattleRawMesh mesh = face.Triangle();
        ImmutableArray<byte> vertices = NativeBattleMeltdownGeometry.MeltVertices(mesh, [geometry.World], target, caster, strength);
        NativeBattleGfGeometryBatch expected = NativeBattleMeltdownGeometry.Emit612B40(mesh, vertices, projection.View!, projection, 0x808080, tag);
        NativeBattleGfGeometryBatch actual = NativeBattleForeignModelDrawing.DrawWorld(geometry, (selected, world, occurrence) => NativeBattleMeltdownGeometry.Emit612B40(selected, NativeBattleMeltdownGeometry.MeltVertices(selected, [world], target, caster, strength), projection.View!, projection, 0x808080, occurrence), tag);
        Assert.Equal(expected.AllocationBytes, actual.AllocationBytes);
        NativeBattleGfGeometryAttempt expectedAttempt = Assert.Single(expected.Attempts);
        NativeBattleGfGeometryAttempt actualAttempt = Assert.Single(actual.Attempts);
        Assert.Equal(expectedAttempt.Data, actualAttempt.Data);
        Assert.Equal(expectedAttempt.WriteMask, actualAttempt.WriteMask);
        Assert.Equal(expectedAttempt.Primitive!.Words, actualAttempt.Primitive!.Words);
        Assert.Equal(expectedAttempt.Primitive.VertexPayload4, actualAttempt.Primitive.VertexPayload4);
        Assert.Equal(expectedAttempt.Primitive.PilotGeometry!.A, actualAttempt.Primitive.PilotGeometry!.A);
        Assert.Equal(expectedAttempt.Primitive.PilotGeometry.B, actualAttempt.Primitive.PilotGeometry.B);
        Assert.Equal(expectedAttempt.Primitive.PilotGeometry.C, actualAttempt.Primitive.PilotGeometry.C);
        Assert.Same(surface, actualAttempt.Primitive.PilotGeometry.ForeignSurface);
    }

    [Fact]
    public void Melt_uses_wrapping_distance_weight_word_stores_and_conditional_ground_clamp()
    {
        NativeBattleRawMesh mesh = new([new(new(100, -100, 0), 0), new(new(3000, 5, 0), 0), new(new(0, 100, 0), 0)], []);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        ImmutableArray<byte> vertices = NativeBattleMeltdownGeometry.MeltVertices(mesh, [identity], new(0, -100, 0, 0), new(0, -2000, 1000, 0), 2000);
        Assert.Equal(new short[] { 100, -77, -11, 0, 3000, 5, 0, 0, 0, 0, -11, 0 }, Enumerable.Range(0, 12).Select(index => BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(index * 2))));
    }

    [Fact]
    public void Meltdown_face_keeps_offscreen_geometry_and_wraps_all_uv_corners_together()
    {
        byte[] bytes = new byte[8 + 24 + 8 * 4 + 20];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        short[] positions = [-100, -100, 100, -100, -100, 100];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), positions[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), positions[index * 2 + 1]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), 250);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 240);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 245);
        NativeBattleStaticMeshResource resource = new(0, 0, bytes.ToImmutableArray());
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], -10000, 0, 4096, 4096), 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleMeltdownDrawing.Prim612EE0(resource, projection.View!, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x30, 0, 20, 0, 0, 0, 128, 128);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal((byte)132, attempt.Data[16]);
        Assert.Equal((byte)137, attempt.Data[24]);
        Assert.Equal((byte)142, attempt.Data[32]);
        Assert.Equal(0xE2000000u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(36)));
    }
}
