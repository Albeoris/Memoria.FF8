using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect239VertexColorTests
{
    [Theory]
    [InlineData(25, 100, 128, 0, 64, 105, 68, 54)]
    [InlineData(1, 3, -128, -86, 255, 182, 234, 9)]
    [InlineData(-32768, 1, 32767, 0, 0, 10, 20, 30)]
    public void Signed_distance_phase_truncation_and_overflow_match_literal_colors(short distance, short radius, short intensity, short phase, int paletteIndex, byte red, byte green, byte blue)
    {
        uint[] colors = new uint[256];
        colors[paletteIndex] = 0x004080FF;
        byte[] destination = [1, 2, 3, 0xA5, 0xCC];
        int written = NativeBattleCinematicActorVertexColor20.WriteGlow(destination, unchecked((ushort)distance), radius, intensity, phase, 0x001E140A, new(0, 0, 0), colors.ToImmutableArray());
        Assert.Equal(3, written);
        Assert.Equal(new byte[] { red, green, blue, 0xA5, 0xCC }, destination);
    }

    [Fact]
    public void Base_channels_clamp_before_glow_and_negative_results_wrap_after_upper_clamp()
    {
        uint[] colors = new uint[256];
        colors[0] = 0x00FFFFFF;
        byte[] destination = [0, 0, 0, 0x7E];
        int written = NativeBattleCinematicActorVertexColor20.WriteGlow(destination, 0, 100, -256, 0, 0x001E140A, new(-100, 300, 0), colors.ToImmutableArray());
        Assert.Equal(3, written);
        Assert.Equal(new byte[] { 1, 0, 31, 0x7E }, destination);
    }

    [Fact]
    public void Active_zero_rgb_preserves_the_retained_high_byte()
    {
        byte[] destination = [9, 8, 7, 0xFE];
        int written = NativeBattleCinematicActorVertexColor20.WriteGlow(destination, 0, 1, 0, 0, 0, new(0, 0, 0), new uint[256].ToImmutableArray());
        Assert.Equal(3, written);
        Assert.Equal(new byte[] { 0, 0, 0, 0xFE }, destination);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    [InlineData(0, 0)]
    public void Inactive_distance_clears_the_complete_dword_without_consuming_a_palette_or_divisor(ushort distance, short radius)
    {
        byte[] destination = [1, 2, 3, 0xA5, 0xCC];
        int written = NativeBattleCinematicActorVertexColor20.WriteGlow(destination, distance, radius, 123, -7, 0x12345678, new(1, 2, 3), default);
        Assert.Equal(4, written);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0xCC }, destination);
    }

    [Fact]
    public void Active_zero_radius_fails_before_any_color_store()
    {
        byte[] destination = [1, 2, 3, 4];
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicActorVertexColor20.WriteGlow(destination, ushort.MaxValue, 0, 1, 0, 0, new(0, 0, 0), new uint[256].ToImmutableArray()));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, destination);
    }

    [Fact]
    public void Signed_palette_words_expand_to_literal_rgb_bytes()
    {
        ushort[] source = new ushort[256];
        source[0] = 0xFFFF;
        source[1] = 0x801F;
        ImmutableArray<uint> colors = NativeBattleCinematicMonsterVertices.Palette(source);
        Assert.Equal(0x00F8F8F8u, colors[0]);
        Assert.Equal(0x000000F8u, colors[1]);
    }
}
