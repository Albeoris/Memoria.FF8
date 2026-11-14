namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverWallRenderingTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Original_wall_captures_host_arena_renders_on_party_targets_and_releases_before_following_Fire(int targetSlot)
    {
        new CrossoverAllGuardianRenderingTests(output).Run("Wall", true, targetSlot: targetSlot, requireArenaCapture: true);
    }
}
