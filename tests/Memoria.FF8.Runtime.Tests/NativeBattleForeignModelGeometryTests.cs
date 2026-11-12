using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignModelGeometryTests
{
    private sealed record Material : INativeBattleForeignMaterial;
    private static readonly NativeBattleActionMatrix Identity = NativeBattleForeignModelGeometry.Identity;
    private static readonly NativeBattlePilotVisualIdentity IdentityTag = new(104, 1, 0, 1, 0);

    [Fact]
    public void PosedHostBoundsAndAnchorsRequireNoSourceGameSkeletonOrAnimationResource()
    {
        NativeBattleEffectActorGeometry geometry = new(new(10, -700, 30), new(20, -300, 40), 50, new(new(-90, -700, -40), new(130, 0, 60)));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 100, 1);
        (pool, int slot) = pool.Append(0x3000);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteByte(0x2D, 3);
        NativeBattleModulePoolTask actual = NativeBattleEffectModelHelpers.UpdateTargetWithBounds8DC610(task, NativeBattleGraphicsModels.ClearBank(), _ => throw new InvalidOperationException("A host skeleton is not an FFVIII animation bank."), index => index == 3 ? geometry : null);
        Assert.Equal((short)10, unchecked((short)actual.ReadWord(0x40)));
        Assert.Equal((short)-700, unchecked((short)actual.ReadWord(0x42)));
        Assert.Equal((short)20, unchecked((short)actual.ReadWord(0x30)));
        Assert.Equal((short)50, unchecked((short)actual.ReadWord(0x32)));
        Assert.Equal((short)-90, unchecked((short)actual.ReadWord(0x48)));
        Assert.Equal((short)130, unchecked((short)actual.ReadWord(0x50)));
        Assert.Equal((short)-700, unchecked((short)actual.ReadWord(0x4A)));
        Assert.Equal((short)0, unchecked((short)actual.ReadWord(0x52)));
    }

    [Fact]
    public void ModelBandUsesActualHostTriangleAndKeepsHostMaterialOutsideNativeIceBand()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        NativeBattleCameraProjection projection = new(Identity with { Z = 8192 }, 128, 160, 108);
        NativeBattleBlizzagaModelBandResult outside = NativeBattleForeignModelDrawing.Band(geometry, projection, 0x808080, 1000, 2000, IdentityTag);
        NativeBattleGeometryPrimitive original = Assert.Single(outside.Primitives);
        Assert.Same(geometry.Faces[0].Surface, original.PilotGeometry!.ForeignSurface);
        Assert.Equal(new Vector3(-256, -256, 0), original.PilotGeometry.A);
        NativeBattleBlizzagaModelBandResult inside = NativeBattleForeignModelDrawing.Band(geometry, projection, 0x808080, -1000, 1000, IdentityTag);
        NativeBattleGeometryPrimitive ice = Assert.Single(inside.Primitives);
        Assert.Null(ice.PilotGeometry!.ForeignSurface);
        Assert.Equal((ushort)0x3D14, (ushort)(ice.Words[3] >> 16));
        Assert.Equal((ushort)0xBA, (ushort)(ice.Words[5] >> 16));
        Assert.Equal(original.Bucket, ice.Bucket);
        Assert.Equal(original.VertexPayload4, ice.VertexPayload4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CureAndCuraKeepOriginalHostBodyMaterialAndDistinctNativeOverlayProfiles(bool cura)
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        geometry = geometry with { Faces = [geometry.Faces[0] with { A = new(-128, -128, 0), B = new(128, -128, 0), C = new(-128, 128, 0) }] };
        NativeBattleActionMatrix camera = Identity with { Z = 8192 };
        NativeBattleCameraProjection projection = new(camera, 128, 20, 15);
        NativeBattleCureTextureProjection texture = cura ? NativeBattleCureActorPackets.TextureProjection8D7050(640, 448, 320, 244, 128, 64) : NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 128, 128);
        NativeBattleCureActorPacketBatch batch = NativeBattleForeignModelDrawing.Cure(geometry, camera, projection, new(0, 0, -8192), 0, 0, 0x808080, IdentityTag, texture, cura ? 0x404040u : 0x808080u);
        NativeBattleGeometryPrimitive body = Assert.Single(batch.Primitives.Where(item => item.Bank == NativeBattleCurePacketBank.Process)).Primitive;
        Assert.Same(geometry.Faces[0].Surface, body.PilotGeometry!.ForeignSurface);
        Assert.Equal(32, batch.ProcessAllocationBytes);
        Assert.NotEmpty(batch.Primitives.Where(item => item.Bank == NativeBattleCurePacketBank.EffectPrimary));
        Assert.All(batch.Primitives.Where(item => item.Bank == NativeBattleCurePacketBank.EffectPrimary), item =>
        {
            Assert.Null(item.Primitive.PilotGeometry!.ForeignSurface);
            Assert.Equal(texture.Clut, (ushort)(item.Primitive.Words[3] >> 16));
            Assert.Equal(cura ? 0x36404040u : 0x36808080u, item.Primitive.Words[1]);
        });
    }

    private static NativeBattleForeignModelGeometry Geometry()
    {
        NativeBattleForeignSurface surface = new(new Material(), [new(0, 0), new(1, 0), new(0, 1)], [Vector4.One, new(1, 0, 0, 1), new(0, 1, 0, 1)]);
        return new(Identity, new(default, default, 0, new(new(-256, -256, 0), new(256, 256, 0))), [new(new(-256, -256, 0), new(256, -256, 0), new(-256, 256, 0), surface)]);
    }
}
