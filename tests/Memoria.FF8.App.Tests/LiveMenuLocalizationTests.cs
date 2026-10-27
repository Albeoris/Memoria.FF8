using System.Text.Json;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class LiveMenuLocalizationTests
{
    [Fact]
    public void ExistingCatalogReadsNewPresentationGenerationWithoutMutatingNativeState()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native menu resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        var native = assets.ReadNewGameCharacterVitals();
        string before = JsonSerializer.Serialize(native);
        string? originalMagic = catalog.MagicName(1);
        string? originalCard = catalog.CardName(0);
        string language = assets.Resources.Source.Language;
        using Stream kernelInput = assets.Resources.Get(new ResourceKey("main/kernel.bin")).OpenRead();
        using MemoryStream kernelBytes = new();
        kernelInput.CopyTo(kernelBytes);
        KernelData kernel = KernelData.ReadFromArray(kernelBytes.ToArray(), FF8Encoding.CreateEuropean(FF8TextLanguage.EN));
        int firstFieldItem = kernel.BattleItems.Count;
        int abilityId = kernel.JunctionAbilities.FindIndex(ability => ability.Description is not null);
        Assert.True(abilityId >= 0);
        Dictionary<string, LocalizationValue> translations = new()
        {
            [LocalizationSourceExporter.IndexedKey(language, LocalizationSourceExporter.KernelTable("MagicSpells", "Name"), 1)] = new("Translated spell", "test", "test"),
            [LocalizationSourceExporter.IndexedKey(language, LocalizationSourceExporter.KernelTable("BattleItems", "Name"), 1)] = new("Translated battle item", "test", "test"),
            [LocalizationSourceExporter.IndexedKey(language, LocalizationSourceExporter.KernelTable("FieldItems", "Name"), 0)] = new("Translated field item", "test", "test"),
            [LocalizationSourceExporter.IndexedKey(language, LocalizationSourceExporter.KernelTable("JunctionAbilities", "Description"), abilityId)] = new("Translated ability help", "test", "test"),
            [LocalizationSourceExporter.IndexedKey(language, "names_cards", 0)] = new("Translated card without heading punctuation", "test", "test")
        };
        assets.Resources.Prepare(null, localization: new(translations)).Commit();
        Assert.Equal("Translated spell", catalog.MagicName(1));
        Assert.Equal("Translated battle item", catalog.ItemName(1));
        Assert.Equal("Translated field item", catalog.ItemName(firstFieldItem));
        Assert.Equal("Translated ability help", catalog.AbilityDescription(abilityId));
        Assert.Equal("Translated card without heading punctuation", catalog.CardName(0));
        Assert.Equal(before, JsonSerializer.Serialize(native));
        assets.Resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal(originalMagic, catalog.MagicName(1));
        Assert.Equal(originalCard, catalog.CardName(0));
    }
}
