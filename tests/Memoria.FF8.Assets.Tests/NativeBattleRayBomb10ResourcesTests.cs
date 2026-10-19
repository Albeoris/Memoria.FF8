using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRayBomb10ResourcesTests
{
    [Fact]
    public void OriginalRayBombResourcesKeepTheActualCameraMeshesSpriteDirectoriesAndStridedTables()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRayBomb10Resources resources = game.ReadNativeRayBomb10Resources();
        Assert.Same(resources, game.ReadNativeRayBomb10Resources());
        Assert.Equal(0x270, resources.Camera.Data.Length);
        Assert.Equal(new uint[] { 0xDEAD64, 0xDED00C, 0xDEC964, 0xDEC13C, 0xDEC3F4 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new uint[] { 0xDEABB8, 0xDEA9E4, 0xDEAA0C, 0xDEA838 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.All(resources.Sprites, sprite => Assert.NotEmpty(sprite.ProjectedFrame571C80(0).Quads));
        Assert.Equal(48, resources.DustOffsets.Length);
        Assert.Equal(48, resources.DustDirections.Length);
        Assert.Equal(20, resources.PuffX.Length);
        Assert.Equal(20, resources.PuffZ.Length);
        Assert.Equal(20, resources.DebrisX.Length);
        Assert.Equal(20, resources.DebrisZ.Length);
    }
}
