using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActorMaterialGeometryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BodyAndLinkedMeshesUseTheirOwnPaletteProvenance(bool relocatedBody, bool sharedSkeleton)
    {
        NativeBattleRawMesh body = Mesh(relocatedBody ? 230 : 224, relocatedBody ? (byte)151 : (byte)23);
        NativeBattleRawMesh weapon = Mesh(224, 37);
        NativeBattleRawMesh replaced = Mesh(224, 99);
        NativeBattleGraphicsResource resource = new([])
        {
            Geometry = sharedSkeleton ? [body, replaced] : [body],
            GeometryRelocated507550 = relocatedBody,
            LinkedGeometry = [weapon],
            LinkedGeometryUsesBodySkeleton = sharedSkeleton
        };
        NativeBattleGraphicsModel model = new(0)
        {
            BodyTextureSlots = new(ImmutableArray.CreateRange(new byte?[16]).SetItem(0, 1)),
            LinkedTextureSlots = new(ImmutableArray.CreateRange(new byte?[16]).SetItem(0, 2))
        };

        ImmutableArray<NativeBattleRawMesh> preparedBody = NativeBattleActorMaterialGeometry.PrepareBody(resource, model);
        ImmutableArray<NativeBattleRawMesh> preparedLinked = NativeBattleActorMaterialGeometry.PrepareSeparateLinked(resource, model);
        if (relocatedBody)
        {
            Assert.Same(body, preparedBody[0]);
        }
        else
        {
            Assert.Equal((225, 10, 151), Material(preparedBody[0]));
            Assert.Equal((224, 0, 23), Material(body));
        }
        NativeBattleRawMesh preparedWeapon;
        if (sharedSkeleton)
        {
            Assert.Equal(2, preparedBody.Length);
            Assert.Empty(preparedLinked);
            preparedWeapon = preparedBody[1];
        }
        else
        {
            Assert.Single(preparedBody);
            preparedWeapon = Assert.Single(preparedLinked);
        }
        Assert.Equal((226, 11, 37), Material(preparedWeapon));
        Assert.Equal((224, 0, 37), Material(weapon));
    }

    [Fact]
    public void AuthoredPaletteRowsWithoutAnUploadStillFail()
    {
        NativeBattleGraphicsResource resource = new([]) { Geometry = [Mesh(230, 5)], LinkedGeometry = [] };
        NativeBattleGraphicsModel model = new(0) { BodyTextureSlots = new(ImmutableArray.CreateRange(new byte?[16])) };
        Assert.Throws<NotSupportedException>(() => NativeBattleActorMaterialGeometry.PrepareBody(resource, model));
    }

    private static NativeBattleRawMesh Mesh(int clutRow, byte v)
    {
        byte[] polygon = new byte[16];
        polygon[9] = v;
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(10), checked((ushort)(clutRow << 6)));
        return new([], [new(false, polygon.ToImmutableArray())]);
    }

    private static (int ClutRow, int Page, int V) Material(NativeBattleRawMesh mesh)
    {
        ImmutableArray<byte> polygon = Assert.Single(mesh.Polygons).Bytes;
        return (BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan().Slice(10)) >> 6, BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan().Slice(14)) & 31, polygon[9]);
    }
}
