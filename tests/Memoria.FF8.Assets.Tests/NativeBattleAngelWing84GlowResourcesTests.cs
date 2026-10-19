using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAngelWing84GlowResourcesTests
{
    [Fact]
    public void Original_glow_meshes_have_distinct_complete_sources()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleAngelWing84GlowResources resources = game.ReadNativeAngelWing84GlowResources();
        Assert.Equal(new uint[] { 0x15720B0, 0x1572810 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.All(resources.Meshes, mesh => Assert.True(mesh.Bytes.Length > 40));
        Assert.NotEqual(resources.Meshes[0].Bytes.Length, resources.Meshes[1].Bytes.Length);
    }
}
