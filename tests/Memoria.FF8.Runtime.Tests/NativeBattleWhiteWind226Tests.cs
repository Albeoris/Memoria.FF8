namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWhiteWind226Tests
{
    [Theory]
    [InlineData((short)-7, (short)100, (short)97)]
    [InlineData((short)7, (short)-100, (short)-97)]
    public void Anchor_height_truncates_each_native_division_before_adding_word36(short minimumY, short word36, short expected)
    {
        Assert.Equal(expected, NativeBattleWhiteWind226.AnchorY(minimumY, word36));
        Assert.Equal(4092, NativeBattleWhiteWind226.SizeScale(-3));
        Assert.Equal(4100, NativeBattleWhiteWind226.SizeScale(3));
    }

    [Fact]
    public void Billboard_copies_current_camera_rotation_and_transforms_full_translation()
    {
        NativeBattleActionMatrix camera = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        NativeBattleActionMatrix output = NativeBattleWhiteWind226.Billboard673810(camera, new(10, 20, 30));
        Assert.Equal(camera.Rotation, output.Rotation);
        Assert.Equal((130, 220, 290), (output.X, output.Y, output.Z));
    }
}
