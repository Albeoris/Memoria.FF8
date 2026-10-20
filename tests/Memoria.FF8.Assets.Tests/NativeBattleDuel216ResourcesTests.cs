using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDuel216ResourcesTests
{
    [Fact]
    public void OriginalDuel216RetainsItsActorDirectorySixWorkspacesTextureAndFiles()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleDuel216Resources resources = game.ReadNativeDuel216Resources();
        resources.Validate();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([216])[215];
        Assert.Equal(0x6959E0u, binding.Entry);
        Assert.Equal(0x6959F0u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag215.tim", 0x2508870), binding.InitializerResource);
        Assert.Same(resources, binding.Duel216Resources);
        Assert.Equal(0x557C9u, resources.Dword(0x113C1A8));
        Assert.Equal(5112, resources.ReadActorDat().Length);
        Assert.Equal(2u, resources.Dword(NativeBattleDuel216Resources.ActorDirectory));
        Assert.Equal("mag215.tim", resources.InitialTim.Identity);
        Assert.Equal(new short[] { 360, 361, 362 }, resources.Files.Select(file => file.FileId));
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        (textures, _) = textures.EnqueueTim505E30(resources.InitialTim);
        textures.Validate();
        foreach ((uint address, int size) in new (uint, int)[] { (0x1140C5C, 64), (0x11431D4, 1156), (0x11484D0, 352), (0x1141444, 168), (0x114A6DC, 1668), (0x11512C8, 236), (0x115049C, 236) })
        {
            NativeBattleGfAnimationWorkspace workspace = new(new byte[size].ToImmutableArray());
            workspace = workspace.Initialize7016B0(resources.Hierarchy(address), size);
            Assert.Equal(address, workspace.ResourceAddress);
        }
    }
}
