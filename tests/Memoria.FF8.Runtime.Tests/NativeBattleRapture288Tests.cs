using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRapture288Tests
{
    [Theory]
    [InlineData(23, false, 100)]
    [InlineData(23, true, 100)]
    [InlineData(33, false, -1137)]
    [InlineData(33, true, -156)]
    [InlineData(43, false, -1948)]
    [InlineData(43, true, 100)]
    public void Target_ascent_preserves_separate_hit_result_curves(int age, bool resisted, short expected)
    {
        Assert.Equal(expected, NativeBattleRapture288Geometry.TargetY(age, 100, 512, resisted));
    }

    [Theory]
    [InlineData(22)]
    [InlineData(63)]
    public void Target_ascent_rejects_frames_outside_its_native_window(int age)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleRapture288Geometry.TargetY(age, 0, 512, false));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(7, 1536)]
    [InlineData(13, 3072)]
    [InlineData(63, 3072)]
    [InlineData(69, 1536)]
    [InlineData(75, 0)]
    public void Stage_envelope_uses_original_unsigned_age_window(int age, int expected)
    {
        ImmutableArray<uint> colors = [0x8080A0, 0x8090A0, 0x80A0A0, 0x80A080, 0xC08080, 0xC08080];
        Assert.Equal(expected, NativeBattleRapture288Geometry.Flash(age, colors).Strength);
    }
}
