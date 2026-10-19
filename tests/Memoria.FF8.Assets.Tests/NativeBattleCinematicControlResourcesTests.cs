using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCinematicControlResourcesTests
{
    [Theory]
    [InlineData(18, 6, true)]
    [InlineData(248, 3, false)]
    public void OriginalControlModulesKeepTheirOwnDirectoriesAndVibrationAvailability(int effect, int textureCount, bool vibration)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        NativeBattleCinematicResources resources = game.ReadNativeCinematicResources(effect);
        resources.Validate();
        Assert.Equal(textureCount, resources.TextureProfile.Count);
        Assert.Equal(profile.InitializationAddress, resources.StaticAddress);
        Assert.Equal(vibration, resources.Vibration is not null);
        Assert.Equal(profile.OwnerDirectoryAddress, resources.TextureProfile.OwnerDirectoryAddress);
        Assert.Equal(profile.QueueAddress, effect == 18 ? 0x2796EC0u : 0x27966D8u);
    }
}
