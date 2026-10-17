using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpriteCameraOffsetTests
{
    [Theory]
    [InlineData(0, -32, 3328)]
    [InlineData(0, -8, 4096)]
    [InlineData(192, -104, 3328)]
    [InlineData(0, 0, 4096)]
    [InlineData(-104, -16, 3072)]
    public void Status_placement_is_applied_after_prior_view_and_normalized_depth(int cameraX, int cameraY, int scale)
    {
        NativeBattleModuleDraw draw = Sprite(false) with { Scale = scale, SpriteCameraXOffset = cameraX, SpriteCameraYOffset = cameraY };
        NativeBattleActionMatrix result = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, PriorView, 512);
        NativeBattleActionMatrix unshifted = NativeBattlePoseMatrices.ModuleSpriteCamera(draw with { SpriteCameraXOffset = 0, SpriteCameraYOffset = 0 }, PriorView, 512);
        Assert.Equal(cameraX, result.X);
        Assert.Equal(cameraY, result.Y);
        Assert.Equal(8000, result.Z);
        Assert.Equal(unshifted.Rotation.ToArray(), result.Rotation.ToArray());
        Assert.Equal(unshifted.Scale, result.Scale);
    }

    [Fact]
    public void Existing_camera_z_bias_remains_before_normalization_and_xy_remains_after_it()
    {
        NativeBattleModuleDraw draw = Sprite(false) with { Anchor = new(0, 0, 0, 0), DepthOffset = 1000, SpriteCameraZOffset = -1200, SpriteCameraXOffset = 100, SpriteCameraYOffset = -200 };
        NativeBattleActionMatrix view = Identity with { X = 300, Y = 400, Z = 1200 };
        NativeBattleActionMatrix result = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, view, 0);
        Assert.Equal(1000, result.X);
        Assert.Equal(1000, result.Y);
        Assert.Equal(0, result.Z);
    }

    [Fact]
    public void Camera_xy_translation_uses_native_dword_wrap_without_word_narrowing()
    {
        NativeBattleModuleDraw draw = Sprite(false) with { Anchor = new(0, 0, 0, 0), DepthOffset = 0, SpriteCameraXOffset = 1, SpriteCameraYOffset = -1 };
        NativeBattleActionMatrix result = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, Identity with { X = int.MaxValue, Y = int.MinValue }, 0);
        Assert.Equal(int.MinValue, result.X);
        Assert.Equal(int.MaxValue, result.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Offset_ingress_uses_the_same_sprite_packet_core_roll_depth_and_extension_order(bool extended)
    {
        NativeBattleModuleDraw draw = Sprite(extended) with { Scale = 3328, SpriteCameraXOffset = 192, SpriteCameraYOffset = -104 };
        NativeBattleCameraProjection projection = new(PriorView, 216, 160, 108);
        NativeBattleModulePacketBatch actual = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, 512, 17, 2);
        NativeBattleModuleDraw cameraSpace = draw with { Anchor = new(192, -104, 8000, 0), DepthOffset = 0, SpriteCameraXOffset = 0, SpriteCameraYOffset = 0 };
        NativeBattleModulePacketBatch equivalent = NativeBattleModulePackets.EmitSprite571DC0(cameraSpace, projection with { View = Identity }, 512, 17, 2);
        Assert.Equal(JsonSerializer.Serialize(equivalent), JsonSerializer.Serialize(actual));
        Assert.Equal(extended ? 52 : 40, actual.AllocationBytes);
        Assert.Equal(extended ? new[] { 40, 0 } : new[] { 0 }, actual.Primitives.Select(primitive => primitive.ByteOffset).ToArray());
        NativeBattleGeometryPrimitive packet = actual.Primitives[^1].Primitive;
        Assert.Equal(0x2E808080u, packet.Words[1]);
        Assert.Equal(new[] { 8000, 8000, 8000, 8000 }, packet.VertexPayload4.ToArray());
        Assert.Equal(517, packet.Bucket);
        Assert.Equal(7u, packet.Words[9] & 0xFF);
        Assert.Equal(7u, packet.Words[9] >> 8 & 0xFF);
        NativeBattleModuleDraw restored = JsonSerializer.Deserialize<NativeBattleModuleDraw>(JsonSerializer.Serialize(draw))!;
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(NativeBattleModulePackets.EmitSprite571DC0(restored, projection, 512, 17, 2)));
    }

    [Fact]
    public void Missing_prior_view_remains_unsupported_with_explicit_placement()
    {
        NativeBattleModuleDraw draw = Sprite(false) with { SpriteCameraXOffset = 192, SpriteCameraYOffset = -104 };
        Assert.Throws<NotSupportedException>(() => NativeBattleModulePackets.EmitSprite571DC0(draw, new(null, 216, 160, 108), 0, 17, 2));
    }

    private static NativeBattleModuleDraw Sprite(bool extended)
    {
        byte[] quad = new byte[20];
        quad[0] = 8;
        quad[1] = 128;
        quad[2] = 8;
        quad[3] = 0x2E;
        return new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Anchor = new(1000, 2000, 3000, 0),
            Scale = 4096,
            DepthOffset = -1000,
            SpriteFlags = 8,
            Resource = new(0, 0xC7EC54, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]),
            SpriteFrame = new(-1, extended, quad.ToImmutableArray())
        };
    }

    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleActionMatrix PriorView => new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], -3000, -2000, 10000, 4096);
}
