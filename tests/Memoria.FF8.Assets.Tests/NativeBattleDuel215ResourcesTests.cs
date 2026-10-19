using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDuel215ResourcesTests
{
    [Fact]
    public void OriginalDuel215RetainsItsDeclaredActorProgramsTextureAndLookupTable()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleDuel215Resources resources = game.ReadNativeDuel215Resources();
        resources.Validate();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([215])[214];
        Assert.Equal(0x69AB60u, binding.Entry);
        Assert.Equal(0x69AB70u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag214.tim", 0x2508938), binding.InitializerResource);
        Assert.Same(resources, binding.Duel215Resources);
        Assert.Equal(0x1970, resources.ReadActorDat().Length);
        Assert.Equal(2u, resources.Dword(NativeBattleDuel215Resources.ActorDirectory));
        Assert.Equal((short)0, resources.Sine(0));
        Assert.Equal((short)4096, resources.Cosine(0));
        Assert.Equal((short)4096, resources.Sine(1024));
        Assert.Equal((short)0, resources.Cosine(1024));
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        (textures, _) = textures.EnqueueTim505E30(resources.InitialTim);
        textures.Validate();
        foreach ((uint address, int size) in new (uint, int)[] { (0x11550A8, 880), (0x115A910, 436), (0x115C4C8, 64), (0x115CA08, 352), (0x115DCCC, 636), (0x1160BB0, 392), (0x11625E0, 148), (0x116703C, 188), (0x1168A5C, 176), (0x1169844, 340), (0x116AD0C, 340) })
        {
            NativeBattleGfAnimationSource source = new(address, resources.Read(address, checked((int)(NativeBattleDuel215Resources.EndAddress - address))));
            NativeBattleGfAnimationWorkspace workspace = new(ImmutableArray.CreateRange(new byte[size]));
            workspace = workspace.Initialize7016B0(source, size);
            Assert.Equal(address, workspace.ResourceAddress);
        }
    }
}
