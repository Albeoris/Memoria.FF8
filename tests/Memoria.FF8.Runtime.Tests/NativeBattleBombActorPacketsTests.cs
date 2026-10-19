using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBombActorPacketsTests
{
    [Fact]
    public void Scrolling_overlay_rebases_cross_page_uvs_and_keeps_texture_window_reset_and_padding()
    {
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0x12340011);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x02BA0022);
        byte[] vertices = new byte[24];
        short[] coordinates = [-100, -100, 0, 100, -100, 0, -100, 100, 0];
        for (int index = 0; index < 3; index++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + axis * 2), coordinates[index * 3 + axis]);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(index * 8 + 6), (ushort)((byte)coordinates[index * 3] | (byte)(coordinates[index * 3 + 1] / 3) << 8));
        }
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleBombActorPackets.Emit([new(false, face.ToImmutableArray())], vertices.ToImmutableArray(), matrix, projection, 0x808080, 0x123456, 0, true, 0x155, 0x100);
        Assert.Equal(72, batch.AllocationBytes);
        NativeBattleGeometryPrimitive original = Assert.IsType<NativeBattleGeometryPrimitive>(batch.Attempts[0].Primitive);
        Assert.Equal(0x24808080u, original.Words[1]);
        NativeBattleGfGeometryAttempt overlay = batch.Attempts[1];
        NativeBattleGeometryPrimitive overlayPrimitive = Assert.IsType<NativeBattleGeometryPrimitive>(overlay.Primitive);
        Assert.Equal(0xE2080218u, overlayPrimitive.Words[1]);
        Assert.Equal(0x26123456u, overlayPrimitive.Words[2]);
        Assert.Equal(0x3F545F9Cu, overlayPrimitive.Words[4]);
        Assert.Equal(0x00BA5F64u, overlayPrimitive.Words[6]);
        Assert.Equal(0xA19C, BinaryPrimitives.ReadUInt16LittleEndian(overlay.Data.AsSpan().Slice(32)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, overlay.WriteMask[34]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, overlay.WriteMask[35]);
        Assert.Equal(0xE2000000u, overlayPrimitive.Words[9]);
        Assert.Equal(original.Bucket - 1, overlayPrimitive.Bucket);
    }

    [Fact]
    public void Sprite_y_reflection_uses_the_independent_native_scale_after_rotation()
    {
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = new(100, 200, 4096, 0), Scale = 2048, Direction = -4096, SpriteYScale = -4096, SpriteFlags = 3, Rotation = 1024 };
        NativeBattleActionMatrix actual = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, camera, 0);
        Assert.Equal(new short[] { 0, 2048, 0, -2048, 0, 0, 0, 0, 2048 }, actual.Rotation.ToArray());
        Assert.Equal((100, 200, 4096), (actual.X, actual.Y, actual.Z));
    }
}
