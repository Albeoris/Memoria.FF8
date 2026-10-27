using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFlare124Tests
{
    [Fact]
    public void Particle_sprite_consumes_only_the_three_written_brightness_bytes_and_preserves_unknown_alpha()
    {
        byte[] bytes = new byte[38];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), -1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 1);
        bytes[18] = 8;
        bytes[19] = 128;
        bytes[20] = 4;
        bytes[21] = 0x2E;
        NativeBattleModuleResource resource = new(0, 0xD70000, 0, 0, bytes.ToImmutableArray());
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, new byte[31]).Write(address + 32, new byte[148]);
        staging = NativeBattleModuleSpriteTemporary.Begin571C80(staging, address, resource.Address, 4).Write(address + 28, [32, 32, 32]);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1024, 4096), 256, 160, 108);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(0), Anchor = new(0, 0, 0, 0), Scale = 4096, SpriteFlags = 4, SpriteColor = 0x202020 };
        NativeBattleModuleSpriteTemporaryResult result = NativeBattleModuleSpriteTemporary.Emit571C80(staging, address, draw, projection, 0, 17, 2);
        Assert.Equal(new byte[] { 32, 32, 32 }, result.Staging.Read(address + 28, 3));
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(address + 31, 1));
        NativeBattleModulePacketBatch expected = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, 0, 17, 2);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Primitives), NativeBattleTestSnapshots.Json(result.Batch.Primitives));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 1792)]
    [InlineData(8, 2048)]
    [InlineData(76, 2048)]
    [InlineData(77, 1536)]
    [InlineData(79, 512)]
    [InlineData(80, 0)]
    public void Target_flash_uses_the_original_two_distinct_ramp_slopes(short counter, int expected)
    {
        Assert.Equal(expected, NativeBattleFlare124Geometry.Flash(counter));
    }

    [Fact]
    public void Target_color_blend_retains_the_destination_fourth_byte_until_a_direct_assignment()
    {
        const uint original = 0xA0808080;
        const uint red = 0x002020FF;
        Assert.Equal(original, NativeBattleFlare124Geometry.Color(3, original, red, 0xB0123456));
        Assert.Equal(0xB0808080u, NativeBattleFlare124Geometry.Color(4, original, red, 0xB0123456));
        Assert.Equal(red, NativeBattleFlare124Geometry.Color(36, original, red, 0xB0123456));
        Assert.Equal(0xB0808080u, NativeBattleFlare124Geometry.Color(80, original, red, 0xB0123456));
        Assert.Equal(original, NativeBattleFlare124Geometry.Color(81, original, red, 0xB0123456));
    }

    [Fact]
    public void Particle_collapse_uses_unsigned_elapsed_and_burst_growth_precedes_terminal_fade()
    {
        NativeBattleModulePoolTask task = new(0, 0, null, 0x6BD020, new byte[32].ToImmutableArray());
        task = task.WriteWord(12, 2048).WriteWord(14, 2048).WriteWord(16, 8).WriteWord(40, 7);
        (NativeBattleModulePoolTask before, _) = NativeBattleFlare124Geometry.PrepareParticle(task);
        Assert.Equal((ushort)2048, before.ReadWord(12));
        (NativeBattleModulePoolTask collapsed, int mode) = NativeBattleFlare124Geometry.PrepareParticle(NativeBattleTestSnapshots.Independent(task).WriteWord(40, 16));
        Assert.Equal((ushort)0, collapsed.ReadWord(12));
        Assert.Equal(-1, mode);
        NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task.WriteWord(40, 56).WriteWord(32, 32)))!;
        (NativeBattleModulePoolTask faded, int terminal) = NativeBattleFlare124Geometry.PrepareParticle(restored);
        Assert.Equal((ushort)2560, faded.ReadWord(12));
        Assert.Equal((ushort)1536, faded.ReadWord(14));
        Assert.Equal((ushort)0, faded.ReadWord(32));
        Assert.Equal(-2, terminal);
    }

    [Fact]
    public void Ripple_reads_the_original_adjacent_row_when_coordinate_extent_is128()
    {
        byte[] bytes = new byte[0x12652A4 - (int)NativeBattleFlare124Resources.BaseAddress];
        NativeBattleFlare124Resources resources = new(bytes.ToImmutableArray());
        NativeBattleModuleProjectedVertex input = new((uint)(200 * 8) | (uint)(200 * 8) << 16, 1000, 0);
        NativeBattleModuleProjectedVertex actual = NativeBattleFlare124ActorPackets.Ripple(input, 0, 0, 0, 0, 0, 0, resources, out short extent);
        Assert.Equal((short)128, extent);
        Assert.Equal(input.Screen, actual.Screen);
        Assert.Equal(0u, actual.Flags);
        Assert.Equal(16u, NativeBattleFlare124ActorPackets.Ripple(input with { Depth = 0 }, 0, 0, 0, 0, 0, 0, resources, out _).Flags);
    }

    [Fact]
    public void Silhouette_keeps_native_unshifted_depth_and_moves_page_blending_into_the_command()
    {
        NativeBattleFlare124Resources resources = new(new byte[0x12652A4 - (int)NativeBattleFlare124Resources.BaseAddress].ToImmutableArray());
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x02560000);
        NativeBattleRawMesh mesh = new([new(new(0, 0, 0), 0), new(new(32, 0, 0), 0), new(new(0, 32, 0), 0)], [new(false, face.ToImmutableArray())]);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1024, 4096);
        NativeBattleCameraProjection projection = new(matrix, 256, 160, 108);
        NativeBattleFlare124ActorDraw result = NativeBattleFlare124ActorPackets.Emit(mesh, [matrix], projection, 0x2020FF, 160, 108, 0, 0, 0, 0, resources, 341, 256);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.Equal(272, primitive.Bucket);
        Assert.Equal(0x262020FFu, primitive.Words[1]);
        Assert.Equal(0x00560000u, primitive.Words[5]);
        Assert.Equal(32, result.Packets.AllocationBytes);
        Assert.Equal((short)8, result.Radius);
    }
}
