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
        Assert.All(applied, offer => Assert.Equal(English, offer.Profile));
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
        menus.ConnectLanguages(() => inventory, offer => offer.Profile == English, () => true, (offer, _) => apply(offer), _ => { }, decline ?? (() => { }), cancel ?? (() => { }));
        return menus;
    }
}
