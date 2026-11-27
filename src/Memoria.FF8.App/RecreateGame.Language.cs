using System.Globalization;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private sealed class LanguageChange(GameLanguageOffer offer, CancellationTokenSource cancellation, Task<string?> package, bool fromConfig)
    {
        internal GameLanguageOffer Offer { get; } = offer;
        internal CancellationTokenSource Cancellation { get; } = cancellation;
        internal Task<string?> Package { get; } = package;
        internal bool FromConfig { get; } = fromConfig;
        internal bool Applying { get; set; }
        internal LanguageSelectionChange? Staged { get; set; }
        internal GameResourceHost? Candidate { get; set; }
        internal string? PreviousPackageId { get; set; }
    }

    private LanguageChange? languageChange;
    private Task<IReadOnlyList<GameLanguageProfile>>? discoveredLanguages;
    private GameLanguageInventory? languageInventory;
    private IReadOnlyList<RemoteModEntry>? languageInventoryEntries;
    private GameLanguageProfile? languageInventoryProfile;
    private IReadOnlyList<ModPackage>? languageInventoryPackages;
    private bool languageInventoryHasProfiles;
    private bool displayLanguageOffered;
    private bool displayLanguageCatalogRequested;
    private bool displayLanguageOfferDeclined;
    private bool displayLanguageDecisionRead;
    private bool hadExplicitNativeLanguage;
    private GameLanguageChoice? activeLanguageChoice;
    private GameLanguageInventory? offeredLanguageInventory;
    private DateTimeOffset displayLanguageCatalogRequestedAt;

    private string LanguageSettingsPath => PlayerStateLoader.LanguageSettingsPath(options);
    private bool CanManageLanguages() => !InFf7 && session.State.Scene == SceneKind.Title && !options.IsAchievementToolSession;

    private void ConnectLanguagePicker()
    {
        GameResourceHost host = resourceHost!;
        if (!displayLanguageDecisionRead)
        {
            LauncherSettings settings = LauncherSettings.Load(LanguageSettingsPath);
            activeLanguageChoice = settings.LanguageChoiceFor(options.GameDirectory);
            hadExplicitNativeLanguage = settings.LegacyResourceLanguageFor(options.GameDirectory) is not null;
            displayLanguageOfferDeclined = settings.LanguageOfferDeclined;
            displayLanguageDecisionRead = true;
        }
        if (host.Resources.Source.Edition == Resources.GameDataEdition.Remastered)
        {
            string edition = host.LanguageProfile.Edition;
            discoveredLanguages ??= Task.Run<IReadOnlyList<GameLanguageProfile>>(() => LocalizationSourceExporter.ProbeLanguages(options.GameDirectory)
                .Where(language => language.Available)
                .Select(language => GameResourceLanguages.Normalize(language.Language))
                .OfType<string>().Select(language => new GameLanguageProfile(edition, language)).ToArray());
        }
        gameMenus!.ConnectLanguages(ReadLanguageInventory, IsCurrentLanguage, CanManageLanguages, BeginLanguageChange, RemoveLanguagePackage, DeclineDisplayLanguage, () => languageChange?.Cancellation.Cancel());
    }

    private GameLanguageInventory ReadLanguageInventory()
    {
        GameResourceHost host = resourceHost!;
        ModLibrarySnapshot library = host.ModLibrary.View;
        GameLanguageProfile active = host.LanguageProfile;
        IReadOnlyList<ModPackage> packages = host.Mods.InstalledPackages;
        bool profilesReady = discoveredLanguages?.IsCompletedSuccessfully == true;
        if (languageInventory is not null && languageInventoryProfile == active && ReferenceEquals(languageInventoryEntries, library.LanguageEntries) && ReferenceEquals(languageInventoryPackages, packages) && languageInventoryHasProfiles == profilesReady)
        {
            return languageInventory;
        }
        GameLanguageProfile[] profiles = profilesReady ? discoveredLanguages!.Result.Prepend(active).Distinct().ToArray() : [active];
        languageInventoryProfile = active;
        languageInventoryEntries = library.LanguageEntries;
        languageInventoryPackages = packages;
        languageInventoryHasProfiles = profilesReady;
        return languageInventory = GameLanguageInventory.Create(profiles, active, packages, [], library.LanguageEntries);
    }

    private bool IsCurrentLanguage(GameLanguageOffer offer)
    {
        GameLanguageProfile active = resourceHost!.LanguageProfile;
        return offer.Profile == active && (activeLanguageChoice is not { } choice || choice.Profile != active ? offer.IsOriginal && LanguagePackages.DisplayTag(resourceHost.EffectiveContentLanguage) == offer.DisplayTag
            : offer.Id == choice.ProviderId && offer.DisplayTag == choice.DisplayTag && offer.Option == choice.Option && PublishedLanguageMatches(resourceHost, choice.PackageId, null));
    }

    private void OfferDisplayLanguageIfNeeded()
    {
        if (!CanManageLanguages() || displayLanguageOffered || displayLanguageOfferDeclined || gameMenus is not { IsOpen: false } || saveMenu.IsOpen || options.OpenModManager)
        {
            return;
        }
        if (activeLanguageChoice is not null || hadExplicitNativeLanguage)
        {
            displayLanguageOffered = true;
            return;
        }
        string effective = resourceHost!.EffectiveContentLanguage;
        CultureInfo display = CultureInfo.CurrentUICulture;
        if (display.Name.Length == 0 || LanguagePackages.DisplayTag(display.Name) == LanguagePackages.DisplayTag(effective))
        {
            displayLanguageOffered = true;
            return;
        }
        if (!displayLanguageCatalogRequested)
        {
            displayLanguageCatalogRequested = true;
            displayLanguageCatalogRequestedAt = DateTimeOffset.UtcNow;
            if (!resourceHost.ModLibrary.View.Catalog.IsRefreshing)
            {
                resourceHost.ModLibrary.RefreshCatalog();
            }
            return;
        }
        ModLibrarySnapshot library = resourceHost.ModLibrary.View;
        if (discoveredLanguages is { IsCompleted: false } || DateTimeOffset.UtcNow - displayLanguageCatalogRequestedAt < TimeSpan.FromMilliseconds(500)
            || library.Catalog.IsRefreshing && DateTimeOffset.UtcNow - displayLanguageCatalogRequestedAt < TimeSpan.FromSeconds(3))
        {
            return;
        }
        GameLanguageInventory inventory = ReadLanguageInventory();
        if (ReferenceEquals(offeredLanguageInventory, inventory))
        {
            return;
        }
        offeredLanguageInventory = inventory;
        if (inventory.OfferForDisplayCulture(display, effective) is { } group)
        {
            displayLanguageOffered = true;
            keyboard.SuppressHeldUiInput();
            gameMenus.OfferDisplayLanguage(group);
        }
    }

    private void DeclineDisplayLanguage()
    {
        displayLanguageOfferDeclined = true;
        try
        {
            (LauncherSettings.Load(LanguageSettingsPath) with { LanguageOfferDeclined = true }).Save(LanguageSettingsPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowSaveNotice(exception.Message);
        }
    }

    private void BeginLanguageChange(GameLanguageOffer offer, bool update, IProgress<LanguageAcquisitionProgress> progress)
    {
        if (!CanManageLanguages() || languageChange is not null || offer.Profile is null)
        {
            return;
        }
        CancellationTokenSource cancellation = new();
        try
        {
            GameResourceHost host = resourceHost!;
            Task<string?> package = offer.RequiresAcquisition || update
                ? host.ModLibrary.AcquireLanguageAsync(offer, host.LanguageInstaller(offer.Profile), cancellation.Token, update, progress)
                : Task.FromResult(offer.InstalledPackage?.Id);
            languageChange = new(offer, cancellation, package, gameMenus!.LanguageChangeFromConfig);
        }
        catch (Exception exception) when (IsLanguageFailure(exception))
        {
            cancellation.Dispose();
            Console.Error.WriteLine(exception);
            gameMenus!.FinishLanguageChange(exception.Message);
        }
    }

    private void RemoveLanguagePackage(GameLanguageOffer offer)
    {
        if (!CanManageLanguages() || IsCurrentLanguage(offer) || offer.Release is not { } release)
        {
            return;
        }
        try
        {
            resourceHost!.ModLibrary.Uninstall(release);
        }
        catch (Exception exception) when (IsLanguageFailure(exception))
        {
            Console.Error.WriteLine(exception);
            gameMenus!.ShowLanguageRemovalFailure(exception.Message);
        }
    }

    private void UpdateLanguageChange()
    {
        if (languageChange is not { } change || !change.Package.IsCompleted)
        {
            return;
        }
        try
        {
            change.Cancellation.Token.ThrowIfCancellationRequested();
            string? packageId = change.Package.GetAwaiter().GetResult();
            if (!change.Applying)
            {
                change.Applying = true;
                gameMenus!.BeginApplyingLanguage();
                return;
            }
            GameResourceHost host = resourceHost!;
            GameLanguageProfile profile = change.Offer.Profile!;
            if (change.Staged is null)
            {
                GameLanguageChoice? previous = LauncherSettings.Load(LanguageSettingsPath).LanguageChoiceFor(options.GameDirectory);
                change.PreviousPackageId = previous?.PackageId != packageId ? previous?.PackageId : null;
                change.Staged = host.Mods.StageLanguageSelection(change.Offer, packageId, previous);
                change.Candidate = host.OpenLanguageCandidateIfNeeded(options, change.Offer, previous, uiPreferences.Difficulty, change.Cancellation.Token);
            }

            GameResourceHost candidate = change.Candidate ?? host;
            bool prepared = candidate.Mods.PreparePending(force: true);
            if (!prepared && candidate.Mods.IsReloadPending && candidate.Mods.View.Outcome is not (ModUpdateOutcome.Failed or ModUpdateOutcome.Blocked or ModUpdateOutcome.ReviewRequired))
            {
                return;
            }
            change.Cancellation.Token.ThrowIfCancellationRequested();
            GameSession? newSession = null;
            bool separateHost = change.Candidate is not null;
            if (separateHost)
            {
                if (prepared && !candidate.Mods.CommitPending())
                {
                    throw LanguagePreparationFailure(candidate);
                }
                if (candidate.LanguageProfile != profile || !PublishedLanguageMatches(candidate, packageId, change.PreviousPackageId))
                {
                    throw LanguagePreparationFailure(candidate);
                }
                candidate.FinishLanguageStartup(change.Cancellation.Token);
                if (!PublishedLanguageMatches(candidate, packageId, change.PreviousPackageId))
                {
                    throw LanguagePreparationFailure(candidate);
                }
                newSession = candidate.CreateColdSession();
            }
            else if (prepared ? !candidate.Mods.PendingLanguageMatches(packageId, change.PreviousPackageId) : !PublishedLanguageMatches(candidate, packageId, change.PreviousPackageId))
            {
                throw LanguagePreparationFailure(candidate);
            }

            change.Cancellation.Token.ThrowIfCancellationRequested();
            LauncherSettings preferences = LauncherSettings.Load(LanguageSettingsPath);
            GameLanguageChoice choice = change.Offer.ToChoice(packageId);
            LauncherSettings selected = preferences.WithLanguageChoice(choice, options.GameDirectory);
            if (separateHost)
            {
                GameSession previousSession = session;
                LaunchOptions previousOptions = options;
                try
                {
                    BindLanguageHost(candidate, options with { ResourceLanguage = profile.ResourceLanguage }, newSession!);
                    selected.Save(LanguageSettingsPath);
                }
                catch (Exception exception) when (IsLanguageFailure(exception))
                {
                    BindLanguageHost(host, previousOptions, previousSession);
                    gameMenus!.Open("language");
                    throw;
                }
                change.Candidate = null;
            }
            else
            {
                selected.Save(LanguageSettingsPath);
                if (prepared && !candidate.Mods.CommitPending())
                {
                    preferences.Save(LanguageSettingsPath);
                    throw LanguagePreparationFailure(candidate);
                }
            }

            change.Staged.Commit();
            activeLanguageChoice = choice;
            if (separateHost)
            {
                host.Dispose();
                if (change.FromConfig)
                {
                    gameMenus!.Open("config");
                    gameMenus.SelectEntry("config", "config-language");
                }
            }
            CompleteLanguageChange(change);
            drainUiButtons = true;
            if (!change.FromConfig)
            {
                titleSelection = Array.FindIndex(titleActions, action => action.Action == TitleAction.Language);
                ShowSaveNotice(() => LocalizeAuthored("language.applied", "Language applied."));
            }
        }
        catch (OperationCanceledException)
        {
            CompleteLanguageChange(change, cancelled: true);
        }
        catch (Exception exception) when (IsLanguageFailure(exception))
        {
            CompleteLanguageChange(change, exception.Message);
        }
    }

    private static bool PublishedLanguageMatches(GameResourceHost host, string? selectedId, string? deactivatedId)
    {
        if (host.Mods.View.Outcome is ModUpdateOutcome.Failed or ModUpdateOutcome.Blocked or ModUpdateOutcome.ReviewRequired)
        {
            return false;
        }
        var active = host.Mods.Catalog.Packages.Where(package => package.Status == ModEffectiveStatus.Active);
        return (selectedId is null || active.Any(package => package.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase)))
            && (deactivatedId is null || !active.Any(package => package.Id.Equals(deactivatedId, StringComparison.OrdinalIgnoreCase)));
    }

    private static InvalidDataException LanguagePreparationFailure(GameResourceHost host)
    {
        string details = string.Join(" ", host.Mods.View.Diagnostics.TakeLast(3).Select(diagnostic => diagnostic.Message));
        return new InvalidDataException(("Could not prepare the selected language resources. " + details).TrimEnd());
    }

    private static bool IsLanguageFailure(Exception exception)
    {
        return exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or HttpRequestException or System.Text.Json.JsonException;
    }

    private void BindLanguageHost(GameResourceHost candidate, LaunchOptions launch, GameSession coldSession)
    {
        gameMenus?.DisconnectMods();
        candidate.ConfigureGlobalSceneClock(globalSettings);
        resourceHost = candidate;
        options = launch;
        candidate.PrepareTexturePublication = PrepareTexturePublication;
        uiCanvas?.Dispose();
        uiCanvas = new MonoGameUiCanvas(GraphicsDevice, batch, pixel, text, options.GameDirectory, candidate.Resources, itemId => FindInventoryItemOverride(itemId)?.Use?.Image);
        liveMenuCatalog = null;
        nativeRefinementDefinitions = null;
        presentationBattleCatalog = null;
        visibleLocalizationFingerprint = null;
        promptedLocalizationStack = null;
        nativeTextFont?.Dispose();
        nativeTextFont = null;
        languageInventory = null;
        ReplacePlayerSession(coldSession);
    }

    private void CompleteLanguageChange(LanguageChange change, string? error = null, bool cancelled = false)
    {
        languageChange = null;
        try
        {
            try
            {
                change.Staged?.Dispose();
            }
            finally
            {
                change.Candidate?.Dispose();
            }
        }
        catch (Exception exception) when (IsLanguageFailure(exception))
        {
            error = string.IsNullOrEmpty(error) ? exception.Message : error + "\n" + exception.Message;
            cancelled = false;
        }
        finally
        {
            change.Cancellation.Dispose();
        }
        if (error is not null)
        {
            Console.Error.WriteLine(error);
        }
        gameMenus?.FinishLanguageChange(error, cancelled);
    }

    private void DisposeLanguageChange()
    {
        if (languageChange is { } change)
        {
            change.Cancellation.Cancel();
            change.Candidate?.Dispose();
            change.Staged?.Dispose();
            change.Cancellation.Dispose();
            languageChange = null;
        }
    }
}
