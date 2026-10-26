using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationArtworkPresentationTests
{
    [Fact]
    public void OptionalInstalledMagazineArtworkReloadsItsBitmapAndKeepsNativeSpriteDimensions()
    {
        string directory = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        Assert.SkipUnless(source.Descriptor.Edition == GameDataEdition.Classic, "The Classic native artwork corpus is required for this presentation check.");
        ResourceService resources = new(source);
        MenuArtworkProvider provider = new(directory, resources);
        UiImageSource sprite = new(UiImageKind.Magazine, 0);
        PixelImage original = provider.Read(sprite);
        TextureKey key = new(new("menu/mag00.tex"), "atlas");
        Rgba32 translatedColor = new(12, 34, 56, 255);
        LocalizationArtworkValue image = new(key, 256, 256, 256, 256, Enumerable.Repeat(translatedColor, 256 * 256).ToImmutableArray(), new string('A', 64), "fixture", "fixture");
        LocalizationCatalog catalog = new([], artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [key] = image });
        var candidate = resources.Prepare(null, localization: catalog);
        Assert.Same(original, provider.Read(sprite));
        candidate.Commit();
        PixelImage translated = provider.Read(sprite);
        Assert.Equal(original.Width, translated.Width);
        Assert.Equal(original.Height, translated.Height);
        Assert.Contains(translated.Pixels, pixel => pixel == translatedColor);
        Assert.DoesNotContain(translated.Pixels, pixel => pixel.A != 0 && pixel != translatedColor);
        resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal(original.Pixels, provider.Read(sprite).Pixels);
        Assert.True(MenuArtworkProvider.IsResourceImage(new(UiImageKind.NativeTutorialPage, 0)));
    }
    [Fact]
    public void OptionalInstalledTutorialBitmapUsesItsNativeTimCanvasAndReloadsOpenPage()
    {
        string directory = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        Assert.SkipUnless(source.Descriptor.Edition == GameDataEdition.Classic, "The Classic native artwork corpus is required for this presentation check.");
        ResourceService resources = new(source);
        using GameAssets assets = new(directory, resources: resources);
        NativeTutorialBrowserPage page = assets.ReadNativeTutorialBrowserCatalog().Categories.SelectMany(category => category.Pages).First(page => Enumerable.Range(0, 4).Any(slot => page.Metadata[39 + slot * 4] != 255));
        MenuArtworkProvider provider = new(directory, resources);
        UiImageSource sprite = new(UiImageKind.NativeTutorialPage, page.Id);
        PixelImage original = provider.Read(sprite);
        TextureKey key = new(new("menu/mngrp.bin"), $"slot{page.ContentResource:000}");
        Rgba32 translatedColor = new(12, 34, 56, 255);
        LocalizationArtworkValue image = new(key, 256, 192, 256, 192, Enumerable.Repeat(translatedColor, 256 * 192).ToImmutableArray(), new string('A', 64), "fixture", "fixture");
        resources.Prepare(null, localization: new([], artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [key] = image })).Commit();
        PixelImage translated = provider.Read(sprite);
        Assert.Equal(original.Width, translated.Width);
        Assert.Equal(original.Height, translated.Height);
        Assert.Contains(translated.Pixels, pixel => pixel == translatedColor);
        resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal(original.Pixels, provider.Read(sprite).Pixels);
    }
    [Fact]
    public void OptionalInstalledStartupLetteringReadsTheCurrentArtworkGeneration()
    {
        string directory = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        Assert.SkipUnless(source.Descriptor.Edition == GameDataEdition.Classic, "The Classic native artwork corpus is required for this presentation check.");
        ResourceService resources = new(source);
        NativeStartupArtwork artwork = NativeStartupArtwork.Load(directory, resources);
        PixelImage original = artwork.Read(4);
        TextureKey key = new(new("main/" + NativeStartupArtwork.ResourceName(4)), "atlas");
        Rgba32 translatedColor = new(12, 34, 56, 255);
        LocalizationArtworkValue image = new(key, NativeStartupArtwork.Width, NativeStartupArtwork.Height, NativeStartupArtwork.Width, NativeStartupArtwork.Height, Enumerable.Repeat(translatedColor, NativeStartupArtwork.Width * NativeStartupArtwork.Height).ToImmutableArray(), new string('A', 64), "fixture", "fixture");
        resources.Prepare(null, localization: new([], artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [key] = image })).Commit();
        Assert.All(artwork.Read(4).Pixels, pixel => Assert.Equal(translatedColor, pixel));
        resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal(original.Pixels, artwork.Read(4).Pixels);
    }
}
