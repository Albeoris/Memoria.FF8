using System.Globalization;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TitleLanguageMenuTests
{
    private static readonly GameLanguageProfile English = new("Remastered", "en");
    private static readonly GameLanguageProfile German = new("Remastered", "de");

    [Fact]
    public void BranchesExpandBeforeAProviderCanBeConfirmed()
    {
        GameLanguageInventory inventory = GameLanguageInventory.Create([English, German], English, [], [], []);
        List<GameLanguageOffer> applied = [];
        MenuController menus = Connect(inventory, offer => applied.Add(offer));
        menus.Open("language");

        Assert.Equal(new[] { "English", "German (Deutsch)" }, menus.Entries.Take(2).Select(entry => entry.Label));
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("language", menus.ScreenId);
        UiList tree = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        Assert.Equal((0, true), tree.TreeRow!(1));
        Assert.Equal((1, null), tree.TreeRow(2));
        Assert.Empty(applied);

        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("language-confirm", menus.ScreenId);
        Assert.Equal("cancel", menus.SelectedEntry?.Id);
        Assert.Empty(applied);
        menus.SelectEntry("language-confirm", "apply");
        menus.Navigate(MenuInput.Confirm);
        GameLanguageOffer chosen = Assert.Single(applied);
        Assert.Equal(German, chosen.Profile);
        Assert.Equal("de", chosen.DisplayTag);
    }

    [Fact]
    public void FirstRunDismissalAndLeavingItsEditionListRecordDecline()
    {
        GameLanguageInventory inventory = GameLanguageInventory.Create([English, German], English, [], [], []);
        GameLanguageGroup group = Assert.IsType<GameLanguageGroup>(inventory.OfferForDisplayCulture(CultureInfo.GetCultureInfo("de-DE"), "en"));
        List<GameLanguageOffer> applied = [];
        int declines = 0;
        MenuController menus = Connect(inventory, applied.Add, decline: () => declines++);

        menus.OfferDisplayLanguage(group);
        Assert.Equal("keep", menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(1, declines);
        Assert.False(menus.IsOpen);

        menus.OfferDisplayLanguage(group);
        menus.SelectEntry("language-offer", "view");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("language", menus.ScreenId);
        Assert.Equal("language-group-de", menus.SelectedEntry?.Id);
        Assert.Contains(menus.Entries, entry => entry.Label == "Original game");
        Assert.Empty(applied);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(2, declines);
    }

    [Fact]
    public void CancellingAnApplyLeavesItsEditionAvailableForRetry()
    {
        GameLanguageInventory inventory = GameLanguageInventory.Create([English, German], English, [], [], []);
        List<GameLanguageOffer> applied = [];
        int cancellations = 0;
        MenuController menus = Connect(inventory, applied.Add, cancel: () => cancellations++);
        menus.Open("language");
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("language-confirm", "apply");
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(1, cancellations);
        Assert.Equal("language-confirm", menus.ScreenId);

        menus.FinishLanguageChange(null, cancelled: true);
        menus.SelectEntry("language-confirm", "apply");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(2, applied.Count);
        Assert.All(applied, offer => Assert.Equal(German, offer.Profile));
    }

    [Fact]
    public async Task PendingLanguageChangeRendersLiveTransfersAndAcknowledgesCancellation()
    {
        const long miB = 1024 * 1024;
        GameLanguageInventory inventory = GameLanguageInventory.Create([English], English, [], [], [TranslationRelease(English)]);
        IProgress<LanguageAcquisitionProgress>? progress = null;
        int cancelRequests = 0;
        MenuController menus = new();
        menus.ConnectLanguages(() => inventory, _ => false, () => true, (_, _, report) => progress = report, _ => { }, () => { }, () => cancelRequests++);
        menus.Open("language");
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("language-confirm", "apply");
        menus.Navigate(MenuInput.Confirm);

        IProgress<LanguageAcquisitionProgress> transfer = Assert.IsAssignableFrom<IProgress<LanguageAcquisitionProgress>>(progress);
        Assert.Contains("Changing language", ViewText(menus.BuildView()));
        await Task.Run(() => transfer.Report(new(LanguageAcquisitionPhase.Downloading, "Opening movie", 5 * miB, 20 * miB)), TestContext.Current.CancellationToken);
        UiView view = menus.BuildView();
        string[] text = ViewText(view);
        Assert.Contains("Downloading language packages…", text);
        Assert.Contains("Package: Opening movie", text);
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, "{0:N1} / {1:N1} MiB · {2:P1}", 5, 20, 0.25), text);
        Assert.DoesNotContain("Use this language edition?", text);
        Assert.DoesNotContain("Unavailable", text);
        UiProgressBar bar = Assert.IsType<UiProgressBar>(Descendants(view.Root).Single(element => element.Id == "language-progress"));
        Assert.Equal(0.25f, bar.Value);
        Assert.False(bar.Bounds.IsEmpty);

        transfer.Report(new(LanguageAcquisitionPhase.Downloading, "Opening movie", 10 * miB, 20 * miB));
        bar = Assert.IsType<UiProgressBar>(Descendants(menus.BuildView().Root).Single(element => element.Id == "language-progress"));
        Assert.Equal(0.5f, bar.Value);
        transfer.Report(new(LanguageAcquisitionPhase.Downloading, "Translation", 15 * miB, null));
        view = menus.BuildView();
        Assert.Contains("Package: Translation", ViewText(view));
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, "{0:N1} MiB received", 15), ViewText(view));
        Assert.IsNotType<UiProgressBar>(Descendants(view.Root).Single(element => element.Id == "language-progress"));

        transfer.Report(new(LanguageAcquisitionPhase.Verifying, "Translation", 20 * miB, 20 * miB));
        Assert.Contains("Verifying language package…", ViewText(menus.BuildView()));
        transfer.Report(new(LanguageAcquisitionPhase.Installing, "Opening movie", 20 * miB, 20 * miB));
        Assert.Contains("Installing language package…", ViewText(menus.BuildView()));
        menus.BeginApplyingLanguage();
        Assert.Contains("Applying language…", ViewText(menus.BuildView()));

        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(1, cancelRequests);
        Assert.False(Assert.Single(menus.Entries).Enabled);
        Assert.Contains("Cancelling…", ViewText(menus.BuildView()));
        menus.FinishLanguageChange(null, cancelled: true);
        Assert.Equal("language-confirm", menus.ScreenId);
        Assert.Contains("Language change cancelled.", ViewText(menus.BuildView()));
        Assert.Contains(menus.Entries, entry => entry.Id == "apply" && entry.Enabled);
        menus.SelectEntry("language-confirm", "apply");
        menus.Navigate(MenuInput.Confirm);
        menus.FinishLanguageChange("Restart to finish installing the package.");
        Assert.Contains("Could not change the language.", ViewText(menus.BuildView()));
        Assert.Contains("Restart to finish installing the package.", ViewText(menus.BuildView()));
    }

    [Fact]
    public void AppliedEditionIsGreenAndCannotBeReappliedWhileOriginalRemainsSelectable()
    {
        GameLanguageInventory inventory = GameLanguageInventory.Create([English, German], English, [], [], [TranslationRelease(English, "en")]);
        GameLanguageGroup english = inventory.Groups.Single(group => group.DisplayTag == "en");
        GameLanguageOffer current = english.Offers.Single(offer => offer.IsOriginal);
        int applications = 0;
        MenuController menus = new();
        menus.ConnectLanguages(() => inventory, offer => offer == current, () => true, (offer, _, _) =>
        {
            current = offer;
            applications++;
        }, _ => { }, () => { }, () => { });
        menus.Open("language");
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("language-confirm", "apply");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("Translation", current.Title);
        menus.FinishLanguageChange(null);
        Assert.False(menus.IsOpen);
        Assert.Equal("Language applied.", menus.Message);

        menus.Open("language");
        menus.Navigate(MenuInput.Confirm);
        UiList tree = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        int collapsedEnglish = tree.Items.IndexOf("English");
        UiColor active = Assert.IsType<UiColor>(tree.ItemTextColor!(collapsedEnglish));
        Assert.True(active.G > active.R && active.G > active.B);
        menus.Navigate(MenuInput.Down);
        tree = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        Assert.Equal(active, tree.ItemTextColor!(collapsedEnglish));
        Assert.Null(tree.ItemTextColor(tree.SelectedIndex));

        menus.SelectEntry("language", "language-group-en");
        menus.Navigate(MenuInput.Confirm);
        tree = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        Assert.Equal(active, tree.ItemTextColor!(tree.Items.IndexOf("English")));
        Assert.Equal(active, tree.ItemTextColor(tree.Items.IndexOf("Translation")));
        Assert.Null(tree.ItemTextColor(tree.Items.IndexOf("Original game")));
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("language", menus.ScreenId);
        Assert.Equal(1, applications);

        menus.Open("config");
        menus.SelectEntry("config", "config-language");
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("language", "language-group-en");
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("language-confirm", "apply");
        menus.Navigate(MenuInput.Confirm);
        Assert.True(menus.LanguageChangeFromConfig);
        Assert.True(current.IsOriginal);
        menus.FinishLanguageChange(null);
        Assert.Equal("config", menus.ScreenId);
        Assert.Equal("Language applied.", menus.Message);
        Assert.Equal(2, applications);
    }

    [Fact]
    public void CurrentEditionOffersItsUpdateWithoutAnApplyAction()
    {
        GameLanguageProfile classic = new("Classic", "en");
        Guid packageId = Guid.Parse("bdd7d71d-8ffd-4aa5-93c1-bf5ae182ad66");
        var release = new RemoteModEntry(RemoteModCatalogAdapter.SourceId, packageId, "Succession", "1.11") { Tags = ["language"] };
        var installed = new ModPackage("Junction/Succession", "Succession", ModFormat.Junction, true, true, ModEffectiveStatus.Active, "")
        {
            IsLanguageProvider = true,
            JunctionMetadata = new JunctionManifestMetadata { Id = packageId, Version = "1.10" }
        };
        GameLanguageInventory inventory = GameLanguageInventory.Create([classic], classic, [installed], [release], []);
        GameLanguageOffer provider = inventory.Groups.SelectMany(group => group.Offers).Single(offer => !offer.IsOriginal);
        Assert.True(provider.CanUpdate);
        List<bool> updates = [];
        MenuController menus = new();
        menus.ConnectLanguages(() => inventory, offer => offer == provider, () => true, (_, update, _) => updates.Add(update), _ => { }, () => { }, () => { });
        menus.ConnectLocalizationText((_, original) => original);
        menus.Open("language");
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("Update this language edition?", menus.CurrentScreen.Title);
        Assert.DoesNotContain(menus.Entries, entry => entry.Id == "apply");
        Assert.Contains(menus.Entries, entry => entry.Id == "update" && entry.Enabled);
        menus.SelectEntry("language-confirm", "update");
        menus.Navigate(MenuInput.Confirm);
        Assert.True(Assert.Single(updates));
    }

    [Fact]
    public void InstalledRemasteredOriginalReopensOnTheSelectedNativeBase()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrEmpty(game) && File.Exists(Path.Combine(game, "main.zzz")), "Select installed Remastered resources with MEMORIA_FF8_REMASTERED_DIR.");
        var available = LocalizationSourceExporter.ProbeLanguages(game!, TestContext.Current.CancellationToken)
            .Where(item => item.Available).Select(item => new GameLanguageProfile("Remastered", GameResourceLanguages.Normalize(item.Language)!)).ToArray();
        Assert.SkipUnless(available.Contains(English) && available.Any(profile => profile != English), "The selected installation must include English and another native profile.");
        GameLanguageProfile destination = available.FirstOrDefault(profile => profile.ResourceLanguage == "it") ?? available.First(profile => profile != English);
        GameLanguageOffer offer = GameLanguageInventory.Create(available, English, [], [], []).Groups.SelectMany(group => group.Offers)
            .Single(offer => offer.IsOriginal && offer.Profile == destination);
        string isolated = Path.Combine(Path.GetTempPath(), "memoria-remastered-language-" + Guid.NewGuid().ToString("N"));
        try
        {
            string mods = Path.Combine(isolated, "Mods");
            var locations = new ModDirectories(mods, [mods], Path.Combine(isolated, "Settings"), mods);
            LaunchOptions options = new() { GameDirectory = game!, ResourceLanguage = "en", ModLocations = locations };
            using GameResourceHost current = new(game!, isolated, watch: false, resourceLanguage: "en", modLocations: locations, connectEncounterMods: true);
            using LanguageSelectionChange staged = current.Mods.StageLanguageSelection(offer, null, null);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using GameResourceHost selected = GameResourceHost.OpenLanguageCandidate(options, destination, BattleDifficulty.Normal, timeout.Token);
            while (selected.Mods.IsReloadPending)
            {
                timeout.Token.ThrowIfCancellationRequested();
                Assert.DoesNotContain(selected.Mods.View.Outcome, new[] { ModUpdateOutcome.Failed, ModUpdateOutcome.Blocked, ModUpdateOutcome.ReviewRequired });
                selected.Pump(force: true);
                if (selected.Mods.IsReloadPending)
                {
                    Thread.Sleep(5);
                }
            }
            selected.FinishLanguageStartup(timeout.Token);
            Assert.Equal(destination, selected.LanguageProfile);
            Assert.Equal(destination.ResourceLanguage, GameResourceLanguages.Normalize(selected.Resources.Source.Language));
            Assert.Equal(English, current.LanguageProfile);
            Assert.Equal(SceneKind.Title, selected.CreateColdSession().State.Scene);
            Assert.Equal("original", offer.ToChoice(null).ProviderId);
            staged.Commit();
        }
        finally
        {
            if (Directory.Exists(isolated))
            {
                Directory.Delete(isolated, recursive: true);
            }
        }
    }

    private static MenuController Connect(GameLanguageInventory inventory, Action<GameLanguageOffer> apply, Action? decline = null, Action? cancel = null)
    {
        MenuController menus = new();
        menus.ConnectLanguages(() => inventory, offer => offer.Profile == English, () => true, (offer, _, _) => apply(offer), _ => { }, decline ?? (() => { }), cancel ?? (() => { }));
        return menus;
    }

    private static string[] ViewText(UiView view) => Descendants(view.Root).OfType<UiLabel>().Select(label => label.Text).ToArray();

    private static RemoteModEntry TranslationRelease(GameLanguageProfile profile, string displayLanguage = "fr")
    {
        LanguageNativeVariant variant = new(profile.Edition, profile.ResourceLanguage);
        OfficialModComponent Component(string id, string name, long size) => new(id, "Memoria", "Memoria/" + id, id, name, "", "Language", "memoria-native", "download", false, new("FFVIII", [profile.Edition], [profile.ResourceLanguage]),
            new("1.0", "[0.1.0,)", "v1.0", "", new("https://example.invalid/release.zip", size, size, new string('a', 64))));
        OfficialModComponent provider = Component("translation", "Translation", 5 * 1024 * 1024) with { LanguageOffers = [new(displayLanguage, "Translation", displayLanguage, [variant])] };
        OfficialModComponent media = Component("opening-movie", "Opening movie", 15 * 1024 * 1024) with { NativeVariants = [variant] };
        return new(OfficialModCatalog.SourceId, provider.CatalogId, provider.Name, "1.0") { Official = provider, RequiredLanguagePackages = [media] };
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children.SelectMany(Descendants))
        {
            yield return child;
        }
    }
}
