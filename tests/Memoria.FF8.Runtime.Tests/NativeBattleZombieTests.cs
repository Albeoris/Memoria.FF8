using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleZombieTests
{
    [Fact]
    public void Alternate_rotation_uses_X_then_Z_product_before_Y()
    {
        NativeBattleActionMatrix result = NativeBattleZombieDrawing.Euler701530(new(1024, 1024, 1024));
        Assert.Equal(new short[] { 0, 4096, 0, -4096, 0, 0, 0, 0, 4096 }, result.Rotation.ToArray());
        Assert.NotEqual(NativeBattleGfMatrices.Euler701310(new(1024, 1024, 1024)).Rotation.ToArray(), result.Rotation.ToArray());
    }

    [Theory]
    [InlineData(30, 0, 0, 512, 0)]
    [InlineData(45, -724, 2896, 4608, 120)]
    public void Overlay_parameters_preserve_native_phase_lift_and_weight(int age, short lift, int weight, int amplitude, short phase)
    {
        var result = NativeBattleZombieDrawing.OverlayParameters(age, 2048);
        Assert.Equal((lift, weight, amplitude, phase), result);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(60)]
    public void Overlay_rejects_ages_outside_its_native_thirty_callback_window(int age)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleZombieDrawing.OverlayParameters(age, 2048));
    }

    [Fact]
    public void Wave_uses_signed_IR2_modulo_index_and_two_separate_arithmetic_shifts()
    {
        short[] wave = new short[4096];
        wave[4092] = 4096;
        NativeBattleModuleDepthCueVertex source = new(new(1000u | 800u << 16, 4096, 0), 0) { Ir2 = -1 };
        NativeBattleProjectedVertex result = NativeBattleZombieOverlay.Project(source, -1048, wave.ToImmutableArray(), 0, 256);
        Assert.Equal(1256u | 800u << 16, result.Screen);
        Assert.Equal(1024u, result.DepthAndClip);
    }

    [Theory]
    [InlineData(0, 800, 0)]
    [InlineData(2560, 800, 2)]
    [InlineData(1000, 1728, 8)]
    [InlineData(-1, -1, 5)]
    public void Overlay_clip_uses_native_inclusive_lower_and_exclusive_upper_viewport_edges(short x, short y, uint clip)
    {
        NativeBattleModuleDepthCueVertex source = new(new(unchecked((ushort)x) | unchecked((uint)y << 16), 4096, 0), 0);
        NativeBattleProjectedVertex result = NativeBattleZombieOverlay.Project(source, 0, new short[4096].ToImmutableArray(), 0, 0);
        Assert.Equal(1024u | clip << 16, result.DepthAndClip);
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(65535, 16383)]
    public void Projected_depth_uses_the_native_45E250_quarter_WORD_without_a_bucket_clamp(ushort sourceDepth, uint expected)
    {
        NativeBattleModuleDepthCueVertex source = new(new(1000u | 800u << 16, sourceDepth, 0), 0);
        Assert.Equal(expected, NativeBattleZombieOverlay.Project(source, 0, new short[4096].ToImmutableArray(), 0, 0).DepthAndClip);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Zero_quarter_depth_requires_only_the_native_rejection_word(ushort depth)
    {
        NativeBattleModuleDepthCueVertex source = new(new(0x12345678, depth, 0), 0) { Ir2 = short.MinValue };
        Assert.Equal(0x100000u, NativeBattleZombieOverlay.Project(source, 0, new short[4096].ToImmutableArray(), 0, 0).DepthAndClip);
    }
}
