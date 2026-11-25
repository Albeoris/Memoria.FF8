using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ClassicTitleLanguageSelectionTests
{
    [Fact]
    public void InstalledJunctionEditionReopensBothWaysAfterTheTitleConsumesNativeText()
    {
        string archive = TestResources.FixturePath("classic-title-language-junction");
        using IsolatedFieldGame game = new();
        string root = Path.Combine(TestResources.ArtifactDirectory, Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string junction = Path.Combine(mods, "Junction");
        Directory.CreateDirectory(junction);
        File.Copy(archive, Path.Combine(junction, "language.iroj"));
        var locations = new ModDirectories(mods, [mods], Path.Combine(root, "Settings"), mods);
        LaunchOptions options = new() { GameDirectory = game.GameDirectory, ResourceLanguage = "en", ModLocations = locations };
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using GameResourceHost current = new(game.GameDirectory, root, watch: false, resourceLanguage: "en", modLocations: locations, connectEncounterMods: true);
        GameLanguageProfile english = new("Classic", "en");
        Assert.Equal(english, current.LanguageProfile);
        Assert.Equal(SceneKind.Title, current.CreateColdSession().State.Scene);
        ResourceKey kernel = new("main/kernel.bin");
        byte[] originalKernel = current.Resources.Read(kernel);
        string originalMagic = Assert.IsType<string>(ReadMagicDescription(current, options));
        GameLanguageOffer[] offers = GameLanguageInventory.Create([english], english, current.Mods.InstalledPackages, [], []).Groups.SelectMany(group => group.Offers).ToArray();
        GameLanguageOffer italian = Assert.Single(offers, offer => offer.DisplayTag == "it");
        Assert.True(italian.CanSelect);
        Assert.Equal("5", italian.Option);
        Assert.Equal(english, italian.Profile);
        string packageId = italian.InstalledPackage!.Id;
        GameLanguageChoice selected = italian.ToChoice(packageId);

        using GameResourceHost translated = ApplyOnNewHost(current, options, italian, null, timeout.Token);
        ModPackage active = Assert.Single(translated.Mods.Catalog.Packages, package => package.Id == packageId && package.Status == ModEffectiveStatus.Active);
        Assert.Equal(5, active.JunctionMetadata!.Options["Language"]);
        Assert.Equal(english, translated.LanguageProfile);
        Assert.True(translated.Resources.HasEncodedReplacement(kernel));
        Assert.False(originalKernel.AsSpan().SequenceEqual(translated.Resources.Read(kernel)));
        Assert.NotEqual(originalMagic, ReadMagicDescription(translated, options));
        Assert.Equal(originalKernel, current.Resources.Read(kernel));

        GameLanguageOffer original = Assert.Single(offers, offer => offer.IsOriginal);
        using GameResourceHost restored = ApplyOnNewHost(translated, options, original, selected, timeout.Token);
        Assert.Equal(english, restored.LanguageProfile);
        Assert.DoesNotContain(restored.Mods.Catalog.Packages, package => package.Id == packageId && package.Status == ModEffectiveStatus.Active);
        Assert.False(restored.Resources.HasEncodedReplacement(kernel));
        Assert.Equal(originalKernel, restored.Resources.Read(kernel));
        Assert.Equal(originalMagic, ReadMagicDescription(restored, options));
    }

    [Fact]
    public void InstalledPlayerTranslationPublishesTitleTextAndRestoresOriginalInPlace()
    {
        string descriptor = TestResources.FixturePath("classic-title-player-package");
        string externalMods = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(descriptor)))!;
        using IsolatedFieldGame game = new();
        string root = Path.Combine(TestResources.ArtifactDirectory, Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        Directory.CreateDirectory(mods);
        var locations = new ModDirectories(mods, [mods, externalMods], Path.Combine(root, "Settings"), mods);
        LaunchOptions options = new() { GameDirectory = game.GameDirectory, ResourceLanguage = "en", ModLocations = locations };
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using GameResourceHost current = new(game.GameDirectory, root, watch: false, resourceLanguage: "en", modLocations: locations, connectEncounterMods: true);
        GameLanguageProfile english = new("Classic", "en");
        Assert.Equal(SceneKind.Title, current.CreateColdSession().State.Scene);
        Assert.Equal("Continue", ReadTitleContinue());
        GameLanguageOffer[] offers = GameLanguageInventory.Create([english], english, current.Mods.InstalledPackages, [], []).Groups.SelectMany(group => group.Offers).ToArray();
        GameLanguageOffer translated = Assert.Single(offers, offer => offer.DisplayTag == "ru");
        Assert.True(translated.CanSelect, translated.Requirement);
        string id = translated.InstalledPackage!.Id;

        ApplyOnCurrentHost(translated, null);
        Assert.Equal(english, current.LanguageProfile);
        Assert.Equal("ru", current.EffectiveContentLanguage);
        Assert.Equal("Продолжить", ReadTitleContinue());
        Assert.Contains(current.Mods.Catalog.Packages, package => package.Descriptor?.IsLanguageDependency == true && package.Status == ModEffectiveStatus.Active);

        ApplyOnCurrentHost(Assert.Single(offers, offer => offer.IsOriginal), translated.ToChoice(id));
        Assert.Equal("en", current.EffectiveContentLanguage);
        Assert.Equal("Continue", ReadTitleContinue());
        Assert.DoesNotContain(current.Mods.Catalog.Packages, package => (package.Id == id || package.Descriptor?.IsLanguageDependency == true) && package.Status == ModEffectiveStatus.Active);

        string ReadTitleContinue()
        {
            return SystemTextCatalog.Translate(current.Resources.Localize, "title.continue", "Continue");
        }

        void ApplyOnCurrentHost(GameLanguageOffer offer, GameLanguageChoice? previous)
        {
            using LanguageSelectionChange staged = current.Mods.StageLanguageSelection(offer, offer.InstalledPackage?.Id, previous);
            Assert.Null(current.OpenLanguageCandidateIfNeeded(options, offer, previous, BattleDifficulty.Normal, timeout.Token));
            Publish(current, timeout.Token);
            staged.Commit();
        }
    }

    private static GameResourceHost ApplyOnNewHost(GameResourceHost current, LaunchOptions options, GameLanguageOffer offer, GameLanguageChoice? previous, CancellationToken cancellationToken)
    {
        using LanguageSelectionChange staged = current.Mods.StageLanguageSelection(offer, offer.InstalledPackage?.Id, previous);
        GameResourceHost candidate = Assert.IsType<GameResourceHost>(current.OpenLanguageCandidateIfNeeded(options, offer, previous, BattleDifficulty.Normal, cancellationToken));
        try
        {
            Publish(candidate, cancellationToken);
            candidate.FinishLanguageStartup(cancellationToken);
            Assert.Equal(SceneKind.Title, candidate.CreateColdSession().State.Scene);
            staged.Commit();
            return candidate;
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

    private static void Publish(GameResourceHost host, CancellationToken cancellationToken)
    {
        while (host.Mods.IsReloadPending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            host.Pump(force: true);
            Assert.True(host.Mods.View.Outcome is ModUpdateOutcome.Updating or ModUpdateOutcome.Idle, string.Join(Environment.NewLine, host.Mods.View.Diagnostics.Select(diagnostic => diagnostic.Message)));
            if (host.Mods.IsReloadPending)
            {
                Thread.Sleep(5);
            }
        }
    }

    private static string? ReadMagicDescription(GameResourceHost host, LaunchOptions options)
    {
        using GameAssets assets = new(options.GameDirectory, resources: host.Resources);
        return LiveMenuCatalog.ReadInstalled(assets).MagicDescription(1);
    }
}
