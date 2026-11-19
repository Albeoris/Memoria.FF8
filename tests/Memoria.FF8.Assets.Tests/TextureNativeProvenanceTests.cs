using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureNativeProvenanceTests
{
    [Fact]
    public void MissingNativeReplacementStillCapturesOnlyPresentedSourceOwnership()
    {
        using InMemoryResourceSource storage = new(new("synthetic", GameDataEdition.Classic, []));
        ResourceService resources = new(storage);
        using NativeBattleEffectTextureSource source = new(resources);
        NativeBattleEffectTextureState previous = State("previous.tim");
        NativeBattleEffectTextureState current = State("current.tim");
        PixelImage previousImage = new(4, 1);
        PixelImage currentImage = new(4, 1);
        source.ObservePresented(previousImage, previous, 0, 0, 0, 0, 4, 1);
        source.ObservePresented(currentImage, current, 0, 0, 0, 0, 4, 1);
        Assert.Equal(0, resources.ResidentTextureCount);

        TextureSourceSnapshot captured = Assert.Single(resources.CaptureTextureSourcesForImages([currentImage]));

        Assert.Equal(new TextureTarget("ff8", "magic/current.tim", "texture/2"), captured.Target);
        Assert.DoesNotContain("scope", captured.Target.Subresource);
        Assert.Equal("native-effect-texture", captured.Context.Layout);
        Assert.Same(currentImage, captured.Original);
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    [Fact]
    public void PrefixMaterialRetainsItsCompleteSourceIdentityAndCropCoordinates()
    {
        using InMemoryResourceSource storage = new(new("synthetic", GameDataEdition.Classic, []));
        ResourceService resources = new(storage);
        using NativeBattleEffectTextureSource source = new(resources);
        PixelImage prefix = new(2, 1, [new(1, 2, 3, 127), new(4, 5, 6, 127)]);

        source.ObservePresented(prefix, State("prefix.tim"), 0, 0, 0, 0, 2, 1);
        TextureSourceSnapshot captured = Assert.Single(resources.CaptureTextureSourcesForImages([prefix]));

        Assert.Equal(4, captured.Context.LogicalWidth);
        Assert.Equal(2, captured.Context.NativeAlias!.Width);
        Assert.Equal(4, captured.Context.NativeAlias.CanvasWidth);
        Assert.Equal(2, captured.Original.Width);
        Assert.Equal(new TextureTarget("ff8", "magic/prefix.tim", "texture/2"), captured.Target);
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    private static NativeBattleEffectTextureState State(string name)
    {
        NativeBattleTextureOrigin origin = new(name, 2, NativeBattleTextureRole.Pixels, 4, 1, 1) { Kind = NativeBattleTextureSourceKind.Tim };
        return NativeBattleEffectTextureState.FromColdProcess().EnqueueRectangle505DF0(new(0, 0, 1, 1), new("synthetic-pixels", new byte[2].ToImmutableArray()), origin: origin).Drain505D20().State;
    }
}
