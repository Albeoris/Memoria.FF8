using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSceneVisibilitySmokeTests
{
    [Fact]
    public void Configured_visible_area_uses_signed_bounds_and_restores_native_scopes()
    {
        string? resolution = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISIBLE_RESOLUTION");
        Assert.SkipUnless(resolution is not null, "Configure the current visible resolution.");
        int[] size = resolution!.Split('x').Select(int.Parse).ToArray();
        Assert.Equal(2, size.Length);
        PixelBounds projection = ViewportLayout.Fit(320, 240, size[0], size[1]);
        BattleSceneVisibleArea area = BattleSceneVisibleArea.FromViewport(size[0], size[1], projection.X, projection.Y, projection.Width, projection.Height);
        Assert.True(area.Left < 0 && area.Right > 2560, "The configured current scene must include the expanded left and right areas.");
        BattleSceneVisibleArea previous = BattleSceneVisibleArea.Current;
        uint[] left = Triangle((int)Math.Ceiling(area.Left / 2));
        uint[] right = Triangle((int)Math.Floor((2560 + area.Right) / 2));
        using (BattleSceneVisibleArea.Begin(area))
        {
            Assert.False(BattleSceneVisibleArea.RejectPacked(left));
            Assert.False(BattleSceneVisibleArea.RejectPacked(right));
            Assert.True(BattleSceneVisibleArea.RejectPacked(Triangle((int)Math.Floor(area.Left) - 32)));
            Assert.True(BattleSceneVisibleArea.RejectPacked(Triangle((int)Math.Ceiling(area.Right) + 32)));
            Assert.False(BattleSceneVisibleArea.RejectPacked([Pack((int)Math.Floor(area.Left) - 32, 100), Pack((int)Math.Ceiling(area.Right) + 32, 100), Pack((int)Math.Floor(area.Left) - 32, 200)]));
            using (BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native))
            {
                Assert.Equal(BattleSceneVisibleArea.Native, BattleSceneVisibleArea.Current);
                Assert.True(BattleSceneVisibleArea.RejectPacked(left));
                Assert.True(BattleSceneVisibleArea.RejectPacked(right));
            }
            Assert.Equal(area, BattleSceneVisibleArea.Current);
            Assert.False(BattleSceneVisibleArea.RejectPacked(left));
        }
        Assert.Equal(previous, BattleSceneVisibleArea.Current);
    }

    private static uint[] Triangle(int x) => [Pack(x, 100), Pack(x + 8, 100), Pack(x, 200)];
    private static uint Pack(int x, int y) => unchecked((ushort)x) | unchecked((uint)(ushort)y << 16);
}
