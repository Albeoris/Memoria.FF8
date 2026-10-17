using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModulePacketResourcesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void OriginalCastingMeshesEmitTheirActualFt4OrG4RecordsWithGteDepth(int meshId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleStaticMeshResource resource = game.ReadNativeEffectMeshes().Single(mesh => mesh.Id == meshId);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 12000, 4096);
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = new(0, 0, 0, 0), MeshId = (byte)meshId, MeshRotation = new(0, 0, 0), MeshScale = new(4096, 4096, 4096), MeshFlags = 0x2F3, MeshClut = 0x3867, MeshFade = 2048 };
        NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitMesh572200(draw, resource, projection, 17, 2);
        NativeBattleSummonColors actualRules = Assert.IsType<NativeBattleSummonColors>(game.ReadNativeBattleGraphicsRules().SummonColors);
        NativeBattleSummonColors code = NativeBattleGteColorInterpolation.CodeConstants45F270;
        Assert.Equal(BitConverter.SingleToInt32Bits(actualRules.FixedScale), BitConverter.SingleToInt32Bits(code.FixedScale));
        Assert.Equal(BitConverter.DoubleToInt64Bits(actualRules.IntegerBias), BitConverter.DoubleToInt64Bits(code.IntegerBias));
        NativeBattleGfGeometryBatch explicitNative = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, camera, projection, new(draw.MeshFlags, 0, draw.MeshFade, null, draw.MeshClut, null, 17, 2), actualRules);
        Assert.Equal(JsonSerializer.Serialize(explicitNative), JsonSerializer.Serialize(batch.CapturedWrites));
        NativeBattleModulePacketBatch restored = JsonSerializer.Deserialize<NativeBattleModulePacketBatch>(JsonSerializer.Serialize(batch))!;
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(restored));
        Assert.NotEmpty(batch.Primitives);
        Assert.Equal(batch.Primitives.Sum(primitive => primitive.Primitive.Words.Length * 4), batch.AllocationBytes);
        Assert.All(batch.Primitives, allocation =>
        {
            Assert.Equal((ushort)0, allocation.Flags20);
            Assert.Equal(4, allocation.Primitive.VertexPayload4.Length);
            Assert.Equal(17 + (allocation.Primitive.VertexPayload4.Sum() >> 6), allocation.Primitive.Bucket);
            Assert.InRange(allocation.ByteOffset, 0, batch.AllocationBytes - 36);
            Assert.Equal(meshId == 7 ? 9 : 10, allocation.Primitive.Words.Length);
            if (meshId != 7)
            {
                Assert.Equal((ushort)0x3867, (ushort)(allocation.Primitive.Words[3] >> 16));
            }
        });
    }
}
