using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSprite64GeometryTests
{
    [Theory]
    [InlineData(1, 22, 28)]
    [InlineData(0, 246, 84)]
    public void Sprite_dimensions_follow_proxy_coordinate_units_without_scaling_UV(int flags, int right, int bottom)
    {
        ImmutableArray<NativeBattleSprite64Corner> corners = NativeBattleSprite64Geometry.Expand(Words(0x64, -10, 20, 40, 50, 32, 8), (ushort)flags);
        Assert.Equal(new NativeBattleSprite64Corner(-10, 20, 40, 50), corners[0]);
        Assert.Equal(new NativeBattleSprite64Corner(right, 20, 72, 50), corners[1]);
        Assert.Equal(new NativeBattleSprite64Corner(-10, bottom, 40, 58), corners[2]);
        Assert.Equal(new NativeBattleSprite64Corner(right, bottom, 72, 58), corners[3]);
    }

    [Fact]
    public void Negative_dimensions_reverse_geometry_and_UV_inside_the_actual_lookup_owner()
    {
        ImmutableArray<NativeBattleSprite64Corner> corners = NativeBattleSprite64Geometry.Expand(Words(0x66, 100, 120, 64, 96, -32, -64), 1);
        Assert.Equal(new NativeBattleSprite64Corner(68, 56, 32, 32), corners[3]);
        Assert.Equal(32 / 256f, corners[3].TextureU);
        Assert.Equal(32 / 256f, corners[3].TextureV);
    }

    [Fact]
    public void Lookup_endpoint256_remains_one_instead_of_wrapping_to_zero()
    {
        ImmutableArray<NativeBattleSprite64Corner> corners = NativeBattleSprite64Geometry.Expand(Words(0x64, 0, 0, 250, 250, 6, 6), 1);
        Assert.Equal(256, corners[3].U);
        Assert.Equal(256, corners[3].V);
        Assert.Equal(1f, corners[3].TextureU);
        Assert.Equal(1f, corners[3].TextureV);
    }

    [Theory]
    [InlineData(250, 16)]
    [InlineData(0, -1)]
    public void Uninitialized_UV_lookup_endpoints_fail_without_extrapolation_or_byte_wrap(int u, int width)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleSprite64Geometry.Expand(Words(0x64, 0, 0, (byte)u, 0, (short)width, 1), 1));
    }

    [Fact]
    public void Zero_dimensions_reach_the_backend_as_degenerate_corners()
    {
        ImmutableArray<NativeBattleSprite64Corner> corners = NativeBattleSprite64Geometry.Expand(Words(0x64, -100, -200, 20, 30, 0, 0), 1);
        Assert.All(corners, corner => Assert.Equal(corners[0], corner));
    }

    [Fact]
    public void Original_rectangle_addition_does_not_wrap_back_to_a_signed_word()
    {
        ImmutableArray<NativeBattleSprite64Corner> corners = NativeBattleSprite64Geometry.Expand(Words(0x66, 32760, 32760, 0, 0, 32, 32), 0);
        Assert.Equal(33016, corners[3].X);
        Assert.Equal(33016, corners[3].Y);
    }

    [Fact]
    public void Other_families_and_incomplete_command_words_are_not_sprite_admission()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleSprite64Geometry.Expand([0x64000000, 0, 0], 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleSprite64Geometry.Expand(Words(0x74, 0, 0, 0, 0, 8, 8), 1));
    }

    private static ImmutableArray<uint> Words(byte opcode, short x, short y, byte u, byte v, short width, short height) => [(uint)opcode << 24 | 0x808080u, (uint)unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16, (uint)u | (uint)v << 8 | 0xBEEF0000, (uint)unchecked((ushort)width) | (uint)unchecked((ushort)height) << 16];
}
