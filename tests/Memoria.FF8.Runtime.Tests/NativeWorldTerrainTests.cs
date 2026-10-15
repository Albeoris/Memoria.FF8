namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldTerrainTests
{
    [Theory]
    [InlineData(0, 128, 32)]
    [InlineData(9, 128, 32)]
    [InlineData(128, 128, 32)]
    [InlineData(32, 64, 64)]
    [InlineData(40, 64, 64)]
    [InlineData(132, 64, 64)]
    [InlineData(48, 32, 64)]
    [InlineData(49, 16, 32)]
    [InlineData(50, 0, 256)]
    public void Native_entity_masks_and_footprints(int entity, byte mask, int radius)
    {
        Assert.Equal(mask, NativeWorldTerrain.PermissionMask(entity));
        Assert.Equal(radius, NativeWorldTerrain.ProbeRadius(entity));
        Assert.True(NativeWorldTerrain.CanTraverse((uint)mask << 16, entity));
        Assert.Equal(mask == 0, NativeWorldTerrain.CanTraverse(0, entity));
        Assert.True(NativeWorldTerrain.CanTraverse(0, entity, false));
        Assert.Equal(mask == 0, NativeWorldTerrain.CanTraverse((uint)mask << 8, entity));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(22)]
    [InlineData(64)]
    [InlineData(66)]
    [InlineData(-1)]
    public void Native_otherwise_permission_is_unconditional_but_height_has_no_authority(int entity)
    {
        Assert.Equal(0, NativeWorldTerrain.PermissionMask(entity));
        Assert.True(NativeWorldTerrain.CanTraverse(0, entity));
        Assert.True(NativeWorldTerrain.CanDisembark(0, entity));
        if (entity is >= 16 and <= 22)
        {
            Assert.Equal(32, NativeWorldTerrain.ProbeRadius(entity));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => NativeWorldTerrain.ProbeRadius(entity));
        }
        Assert.Throws<NotSupportedException>(() => NativeWorldTerrain.ResolveHeight(entity, 0, 0, 0));
    }

    [Theory]
    [InlineData(33, 0x40000)]
    [InlineData(132, 0x40000)]
    [InlineData(48, 0x20000)]
    [InlineData(49, 0x10000)]
    [InlineData(50, 0x8000)]
    public void Boarding_flags_use_their_original_byte(int entity, uint mask)
    {
        Assert.True(NativeWorldTerrain.CanDisembark(mask, entity));
        Assert.False(NativeWorldTerrain.CanDisembark(0, entity));
        Assert.False(NativeWorldTerrain.CanDisembark(mask == 0x8000 ? mask << 8 : mask >> 8, entity));
        Assert.True(NativeWorldTerrain.CanDisembark(0, entity, false));
    }

    [Fact]
    public void Native_permission_arguments_narrow_to_their_word_width()
    {
        Assert.Equal(0x80, NativeWorldTerrain.PermissionMask(65536));
        Assert.Equal(0x20, NativeWorldTerrain.PermissionMask(65584));
        Assert.False(NativeWorldTerrain.CanDisembark(0, 65584));
        Assert.True(NativeWorldTerrain.CanDisembark(0x20000, 65584));
    }

    [Theory]
    [InlineData(0, -1000, 5, 0, -680)]
    [InlineData(33, -1000, 6, 0, -1000)]
    [InlineData(49, -1000, 5, 0, -680)]
    [InlineData(0, 32760, 5, 0, -32456)]
    [InlineData(33, 64536, 6, 0, -1000)]
    public void Ordinary_ground_height_preserves_native_word_wrap(int entity, int ground, byte type, int current, int expected)
    {
        Assert.Equal(new NativeWorldTerrainHeightResult(expected, -123), NativeWorldTerrain.ResolveHeight(entity, ground, type, current, flightHeight: -123));
    }

    [Theory]
    [InlineData(-1000, 30, -950, 0, -946)]
    [InlineData(-1000, 34, -950, 1, -954)]
    [InlineData(-1000, 30, -999, 1, -1000)]
    [InlineData(-1000, 30, -873, 0, -872)]
    [InlineData(-1000, 29, -950, 0, -1000)]
    [InlineData(32767, 30, 32766, 0, 32767)]
    public void Entity49_height_uses_speed_and_signed_word_bounds(int ground, byte type, int current, int speed, int expected)
    {
        Assert.Equal(expected, NativeWorldTerrain.ResolveHeight(49, ground, type, current, rawSpeed: speed).Height);
    }

    [Theory]
    [InlineData(-1000, 6, -2000, -1256)]
    [InlineData(-1000, 6, -1200, -1200)]
    [InlineData(-1000, 6, -1100, -1128)]
    [InlineData(-1000, 5, -1000, -936)]
    [InlineData(-1000, 5, -700, -808)]
    [InlineData(-32768, 6, 0, -32768)]
    public void Entity48_hover_clamps_use_word_arithmetic(int ground, byte type, int current, int expected)
    {
        Assert.Equal(expected, NativeWorldTerrain.ResolveHeight(48, ground, type, current).Height);
    }

    [Theory]
    [InlineData(-1000, -1100, 0, -1200)]
    [InlineData(-1000, -1400, 13, -1400)]
    [InlineData(-1000, -4000, 0, -3584)]
    [InlineData(-32768, 0, 0, 32568)]
    public void Flight_height_updates_its_original_word_after_dword_ground_comparison(int ground, int flight, byte mode, int expected)
    {
        Assert.Equal(new NativeWorldTerrainHeightResult(expected, expected), NativeWorldTerrain.ResolveHeight(50, ground, 0, 0, scriptMode: mode, flightHeight: flight));
    }

    [Fact]
    public void Scripted_flight_pointer_result_is_not_invented_as_a_height()
    {
        Assert.Throws<NotSupportedException>(() => NativeWorldTerrain.ResolveHeight(50, 0, 0, 0, scriptMode: 5));
    }
}
