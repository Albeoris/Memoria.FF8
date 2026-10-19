using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSuicide180ResourcesTests
{
    [Fact]
    public void Original_mesh_groups_and_sprite_directories_reach_their_native_packet_producers()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleSuicide180Resources resources = game.ReadNativeSuicide180Resources();
        Assert.Same(resources, game.ReadNativeSuicide180Resources());
        Assert.Equal(0x110, resources.Camera.Data.Length);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        foreach (NativeBattleStaticMeshResource resource in new[] { resources.Explosion, resources.Ring, resources.SecondRing })
        {
            NativeBattleGfGeometryBatch first = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, projection.View!, projection, new(0x33, 0, 0, null, null, null, 17, 2), colors);
            NativeBattleGfGeometryBatch faded = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, projection.View!, projection, new(0xF3, 0, 3584, null, null, null, 17, 2), colors);
            Assert.NotEmpty(first.Attempts);
            Assert.Equal(first.Attempts.Length, faded.Attempts.Length);
        }
        foreach (NativeBattleModuleResource sprite in new[] { resources.Spark, resources.Smoke })
        {
            sprite.Validate();
            int frames = BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8));
            Assert.True(frames > 1);
            Assert.True(sprite.SpriteFrame(frames - 1).NextOffset < 0);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = sprite, SpriteFrame = sprite.SpriteFrame(0), Anchor = new(0, 0, 0, 0), Scale = 4096, Direction = -4096, SpriteYScale = -4096, SpriteFlags = 2 };
            Assert.NotEmpty(NativeBattleModulePackets.EmitSprite571DC0(draw, projection, 0, 17, 2).Primitives);
        }
    }
}
