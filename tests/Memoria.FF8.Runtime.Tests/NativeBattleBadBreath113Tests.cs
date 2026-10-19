using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath113Tests
{
    [Fact]
    public void Zero_depth_sway_writes_only_the_rejection_word()
    {
        NativeBattleProjectedVertex prior = new(0x12345678, 0xABCD4321);
        NativeBattleProjectedVertex result = NativeBattleBadBreath113.Sway6CB7D0(new(0, 0, 0), 0, 0, 0, 0, new short[4096].ToImmutableArray(), prior);
        Assert.Equal(0x12345678u, result.Screen);
        Assert.Equal(0x00104321u, result.DepthAndClip);
    }

    [Fact]
    public void Sway_uses_camera_ir2_and_signed_word_screen_displacement()
    {
        short[] wave = new short[4096];
        wave[4095] = 4096;
        NativeBattleProjectedVertex result = NativeBattleBadBreath113.Sway6CB7D0(new(1024u | 800u << 16, 1000, 0x80020000), -1, 0, 1024, 3, wave.ToImmutableArray(), default);
        Assert.Equal(1536u | 800u << 16, result.Screen);
        Assert.Equal(1000u, result.DepthAndClip);
    }

    [Fact]
    public void Cone_direction_and_rise_height_preserve_original_integer_sign_rules()
    {
        Assert.Equal(new NativeBattleVector(0, 0, -4096), NativeBattleBadBreath113.Direction6CBB40(0, 0, 0));
        Assert.Equal(-1000, NativeBattleBadBreath113.RiseHeight(24000, -1000));
        Assert.Equal(-1, NativeBattleBadBreath113.RiseHeight(32767, -1));
        Assert.Equal(0, NativeBattleBadBreath113.RiseHeight(1, -1));
    }
}
