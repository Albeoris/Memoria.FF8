using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.Launcher.Tests;

public sealed class LauncherLanguageTests
{
    private static readonly GameLanguageProfile English = new("Classic", "en");
    private static readonly GameLanguageProfile German = new("Classic", "de");

    [Fact]
    public void TwoLevelInventoryShowsOriginalFirstAndReviewedJunctionChoices()
    {
        RemoteModEntry ilp = new(RemoteModCatalogAdapter.SourceId, Guid.Parse("3d7436a1-2cd7-4503-821a-910b0f7aa7e3"), "ILP", "1.8") { PublishedSize = "34 MB" };
        RemoteModEntry succession = new(RemoteModCatalogAdapter.SourceId, Guid.Parse("bdd7d71d-8ffd-4aa5-93c1-bf5ae182ad66"), "Succession", "1.10");
        GameLanguageInventory inventory = GameLanguageInventory.Create([English], English, [], [ilp, succession], []);

        Assert.Equal(["English", "French", "German", "Italian", "Spanish"], inventory.Groups.Select(group => group.EnglishName));
        Assert.Equal(["Original", "ILP retranslation", "Succession (story rewrite)"], inventory.Groups[0].Offers.Select(offer => offer.Title));
        Assert.Equal("German (Deutsch)", inventory.Groups.Single(group => group.DisplayTag == "de").Label);
        GameLanguageOffer french = Assert.Single(inventory.Groups.Single(group => group.DisplayTag == "fr").Offers);
        Assert.Equal("2", french.Option);
        Assert.Contains("Junction · Download available", LauncherWindow.LanguageEditionSummary(french, false));
        Assert.DoesNotContain("size unknown", LauncherWindow.LanguageEditionSummary(french, false));
    }

    [Fact]
    public void MissingEnglishBaseKeepsRussianOfferVisibleAndExplainsItsRequirement()
    {
        GameLanguageInventory inventory = GameLanguageInventory.Create([German], German, [], [], [MakeRelease()]);

        Assert.True(Assert.Single(inventory.Groups.Single(group => group.DisplayTag == "de").Offers).IsOriginal);
        GameLanguageOffer russian = Assert.Single(inventory.Groups.Single(group => group.DisplayTag == "ru").Offers);
        Assert.False(russian.CanSelect);
        Assert.Contains("Unavailable", LauncherWindow.LanguageEditionSummary(russian, false));
        Assert.Contains("Requires Classic English", LauncherWindow.LanguageEditionSummary(russian, false));
    }

    [Fact]
    public void ConfirmationNamesTheBaseAndCountsOnlyMissingOrSelectedUpdates()
    {
        GameLanguageOffer offer = Assert.Single(GameLanguageInventory.Create([English], English, [], [], [MakeRelease()]).Groups.Single(group => group.DisplayTag == "ru").Offers);
        string install = LauncherWindow.LanguageConfirmation(offer, German, update: false);
        Assert.Contains("Native base: Classic English", install);
        Assert.Contains("Switches base from Classic German", install);
        Assert.Contains("Total download: 5 MiB", NormalizeFraction(install));
        Assert.Contains("Opening movie 3.20", install);

        ModPackage installed = new("Memoria/russian-ffrtt", "FFRTT", ModFormat.Memoria, null, false, ModEffectiveStatus.Disabled, "");
        GameLanguageOffer existing = offer with { InstalledPackage = installed, Version = "3.19", CanUpdate = true };
        string ordinarySelection = LauncherWindow.LanguageConfirmation(existing, English, update: false);
        Assert.Contains("FFRTT 3.19 · installed", ordinarySelection);
        Assert.Contains("Total download: 4 MiB", NormalizeFraction(ordinarySelection));

        string selectOutdated = LauncherWindow.LanguageConfirmation(existing with { RequiresAcquisition = false }, English, update: false);
        Assert.Contains("Total download: No download", selectOutdated);
        Assert.Contains("Opening movie · installed version in use", selectOutdated);

        GameLanguageOffer withMedia = existing with { DownloadMembers = [offer.DownloadMembers[0], offer.DownloadMembers[1] with { IsInstalled = true }] };
        string update = LauncherWindow.LanguageConfirmation(withMedia, English, update: true);
        Assert.Contains("Total download: 1 MiB", NormalizeFraction(update));
        Assert.Contains("Opening movie 3.20 · installed", update);
    }

    private static string NormalizeFraction(string value) => value.Replace(".0 MiB", " MiB", StringComparison.Ordinal).Replace(",0 MiB", " MiB", StringComparison.Ordinal);

    private static RemoteModEntry MakeRelease()
    {
        LanguageNativeVariant variant = new("Classic", "en");
        OfficialModComponent Component(string id, string name, long size) => new(id, "Memoria", "Memoria/" + id, id, name, "", "Language", "memoria-native", "download", false, new("FFVIII", ["Classic"], ["en"]),
            new("3.20", "[0.1.0,)", "v3.20", "", new("https://example.invalid/package.zip", size, size, new string('a', 64))));
        OfficialModComponent provider = Component("russian-ffrtt", "FFRTT", 1048576) with { LanguageOffers = [new("ru", "FFRTT", "ru", [variant])] };
        OfficialModComponent media = Component("russian-ffrtt-media", "Opening movie", 4194304) with { NativeVariants = [variant] };
        return new(OfficialModCatalog.SourceId, provider.CatalogId, provider.Name, "3.20") { Official = provider, RequiredLanguagePackages = [media] };
    }
}
