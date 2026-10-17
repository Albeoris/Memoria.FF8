using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShadowSourceTests
{
    [Fact]
    public void Original_shadow_uses_an_identity_offset_flattened_Y_and_zero_initialized_perimeter_axes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleShadowSource source = Assert.IsType<NativeBattleShadowSource>(game.ReadNativeBattleGraphicsRules().ShadowSource);
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, source.OffsetMatrix.Rotation);
        Assert.Equal((0, 0, 0), (source.OffsetMatrix.X, source.OffsetMatrix.Y, source.OffsetMatrix.Z));
        Assert.Equal(new(4096, 0, 4096), source.RowScale);
        Assert.Equal(10, source.Vertices.Length);
        Assert.All(source.Vertices, vertex => Assert.Equal(default, vertex));
    }

    [Fact]
    public void Shadow_constants_require_the_authenticated_executable()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleShadowSourceReader.Read([0, 1, 2, 3]));
    }
}
