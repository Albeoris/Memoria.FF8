using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Platform;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly Button gameLanguage = new() { Content = "Choose language…", MinHeight = 32 };

    private GameLanguageProfile? LauncherLanguageProfile => EffectiveResourceLanguage is { } language ? new(selectedResourceLanguages.Edition.ToString(), language) : null;

    private void UpdateGameLanguageControl()
    {
        gameLanguage.IsEnabled = !busy && LauncherLanguageProfile is not null;
        string? tag = selectedLanguageChoice?.Profile == LauncherLanguageProfile ? selectedLanguageChoice?.DisplayTag : LauncherLanguageProfile?.ResourceLanguage;
        gameLanguage.Content = tag is not null ? LanguageName(tag) + "…" : "Choose language…";
    }

    private async Task OpenGameLanguagesAsync()
    {
        if (busy || installations.SelectedItem is not GameInstallation installation || LauncherLanguageProfile is null)
        {
            return;
        }
        ReadSelectedLanguage();
        await InitializeLauncherModsAsync();
        if (busy || !Equals(installation, installations.SelectedItem) || LauncherLanguageProfile is not { } profile)
        {
            return;
        }
        Window dialog = new() { Title = "Game language", Width = 690, Height = 560, MinWidth = 410, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        StackPanel branches = new() { Spacing = 8 };
        TextBlock notice = LauncherDescription("");
        Button refresh = LauncherModButton("Refresh languages");
        Button close = LauncherModButton("Close");
        close.IsCancel = true;
        close.Click += (_, _) => dialog.Close();
        refresh.Click += (_, _) => launcherModLibrary?.RefreshCatalog();
        DockPanel layout = new() { Margin = new Thickness(20) };
        DockPanel.SetDock(notice, Dock.Top);
        layout.Children.Add(notice);
        StackPanel actions = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 12, 0, 0), Children = { refresh, close } };
        DockPanel.SetDock(actions, Dock.Bottom);
        layout.Children.Add(actions);
        layout.Children.Add(new ScrollViewer { Content = branches });
        dialog.Content = layout;

        IReadOnlyList<RemoteModEntry>? lastEntries = null;
        long lastGeneration = -1;
        bool interacting = false;
        bool changed = false;
        void Render()
        {
            if (interacting)
            {
                return;
            }
            ModLibrarySnapshot? snapshot = launcherModLibrary?.View;
            refresh.IsEnabled = snapshot is not null && !snapshot.Catalog.IsRefreshing;
            notice.Text = !selectedInstallationPlayable ? "This installation can be inspected, but the engine cannot play it."
                : snapshot?.Catalog.IsRefreshing == true ? "Refreshing language listings…"
                : snapshot?.Catalog.Error is not null ? "Catalog refresh was incomplete. Showing saved listings and local packages."
                : launcherModInitializationError ?? "";
            notice.IsVisible = notice.Text.Length > 0;
            IReadOnlyList<RemoteModEntry> entries = snapshot?.LanguageEntries ?? [];
            long generation = launcherMods?.Catalog.Generation ?? 0;
            if (ReferenceEquals(lastEntries, entries) && generation == lastGeneration)
            {
                return;
            }
            var expanded = branches.Children.OfType<Expander>().Where(branch => branch.IsExpanded).Select(branch => branch.Tag as string).ToHashSet();
            bool initial = lastEntries is null;
            lastEntries = entries;
            lastGeneration = generation;
            branches.Children.Clear();
            GameLanguageInventory inventory = GameLanguageInventory.Create(selectedResourceLanguages.NativeProfiles, profile, launcherMods?.InstalledPackages ?? [], [], entries);
            foreach (GameLanguageGroup group in inventory.Groups)
            {
                StackPanel editions = new() { Spacing = 8, Margin = new Thickness(12, 4, 4, 4) };
                Expander branch = new() { Header = group.Label, Tag = group.DisplayTag, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = editions };
                branch.IsExpanded = expanded.Contains(group.DisplayTag) || initial && group.Offers.Any(IsCurrentGameLanguage);
                foreach (GameLanguageOffer offer in group.Offers)
                {
                    bool active = IsCurrentGameLanguage(offer);
                    bool canApply = selectedInstallationPlayable && offer.CanSelect && launcherMods is not null;
                    StackPanel row = new() { Spacing = 5, Children = { LauncherDescription(LanguageEditionSummary(offer, active)) } };
                    StackPanel commands = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
                    Button select = LauncherModButton(offer.RequiresAcquisition ? "Download and select…" : "Select…");
                    select.IsEnabled = canApply && (!active || selectedLanguageChoice is null);
                    select.Click += async (_, _) => await ReviewAndApplyAsync(offer, update: false);
                    commands.Children.Add(select);
                    if (offer.CanUpdate)
                    {
                        Button update = LauncherModButton("Update and select…");
                        update.IsEnabled = canApply;
                        update.Click += async (_, _) => await ReviewAndApplyAsync(offer, update: true);
                        commands.Children.Add(update);
                    }
                    if (offer.InstalledPackage is { Status: not ModEffectiveStatus.Active } package && package.Id != selectedLanguageChoice?.PackageId
                        && offer.Release is { } release && snapshot?.LanguageInstallations.Any(item => item.SourceId == release.SourceId && item.Id == release.Id && item.CanUninstall) == true)
                    {
                        Button remove = LauncherModButton("Uninstall…");
                        remove.Click += async (_, _) =>
                        {
                            if (await LauncherDialogs.ConfirmAsync(dialog, "Uninstall language package", $"Remove {release.Name}? This removes every language edition supplied by the package."))
                            {
                                launcherModLibrary?.Uninstall(release);
                            }
                        };
                        commands.Children.Add(remove);
                    }
                    row.Children.Add(commands);
                    editions.Children.Add(row);
                }
                branches.Children.Add(branch);
            }
        }

        async Task ReviewAndApplyAsync(GameLanguageOffer offer, bool update)
        {
            interacting = true;
            try
            {
                if (!await LauncherDialogs.ConfirmAsync(dialog, "Review language change", LanguageConfirmation(offer, profile, update)))
                {
                    return;
                }
                using CancellationTokenSource cancellation = new();
                Window progress = new() { Title = "Changing game language", Width = 450, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
                Button stop = LauncherModButton("Cancel");
                TextBlock detail = LauncherDescription("Preparing language and required packages…");
                progress.Content = new StackPanel { Margin = new Thickness(20), Spacing = 10, Children = { detail, new ProgressBar { IsIndeterminate = true }, stop } };
                bool finished = false;
                void RequestCancellation()
                {
                    stop.IsEnabled = false;
                    detail.Text = "Cancelling language change…";
                    cancellation.Cancel();
                }
                stop.Click += (_, _) => RequestCancellation();
                progress.Closing += (_, args) =>
                {
                    if (!finished)
                    {
                        args.Cancel = true;
                        RequestCancellation();
                    }
                };
                Task progressDialog = progress.ShowDialog(dialog);
                SetBusy(true);
                string? error = null;
                try
                {
                    await ApplyGameLanguageAsync(installation, offer, update, cancellation.Token);
                    changed = true;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException or System.Text.Json.JsonException or FormatException or OverflowException)
                {
                    error = exception.Message;
                }
                finally
                {
                    SetBusy(false);
                    finished = true;
                    progress.Close();
                    await progressDialog;
                }
                if (error is not null)
                {
                    await LauncherDialogs.ShowAsync(dialog, "Could not change game language", error);
                }
                if (changed)
                {
                    dialog.Close();
                }
            }
            finally
            {
                interacting = false;
                lastGeneration = -1;
                if (dialog.IsVisible)
                {
                    Render();
                }
            }
        }

        Render();
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) => Render();
        timer.Start();
        try
        {
            await dialog.ShowDialog(this);
        }
        finally
        {
            timer.Stop();
        }
        if (changed)
        {
            await RecheckOfficialModSelectionAsync();
        }
    }

    private async Task ApplyGameLanguageAsync(GameInstallation installation, GameLanguageOffer offer, bool update, CancellationToken cancellationToken)
    {
        if (!selectedInstallationPlayable || !offer.CanSelect || offer.Profile is not { } profile || launcherMods is not { } mods || launcherModLibrary is not { } library)
        {
            throw new InvalidOperationException(offer.Requirement ?? "The language is unavailable for this installation.");
        }
        if (await Task.Run(IsAnotherGameRunning, cancellationToken))
        {
            throw new InvalidOperationException("Choose Language in the game menu or close the game before changing its language.");
        }
        bool junction = profile.Edition == "Classic" && profile.ResourceLanguage == "en";
        ModPackageInstaller installer = ModLibraryProfile.CreateInstaller(mods.PrimaryModsDirectory, installation.Directory, profile.Edition, profile.ResourceLanguage, junction, mods.PreferencesDirectory);
        string? packageId = await library.AcquireLanguageAsync(offer, installer, cancellationToken, update);
        cancellationToken.ThrowIfCancellationRequested();
        string settingsPath = LauncherSettings.DefaultPath;
        LauncherSettings settings = LauncherSettings.Load(settingsPath);
        GameLanguageChoice? previous = settings.LanguageChoiceFor(installation.Directory);
        ApplicationPaths paths = ApplicationPaths.Current;
        ModDirectories locations = ApplicationInstallation.ResolveModDirectories(AppContext.BaseDirectory, installation.Directory, paths, settings);
        launcherModTimer.Stop();
        try
        {
            using LanguageSelectionChange staged = mods.StageLanguageSelection(offer, packageId, previous);
            await Task.Run(() => ValidateGameLanguage(installation.Directory, locations, profile, packageId, previous?.PackageId, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            LauncherSettings.Load(settingsPath).WithLanguageChoice(offer.ToChoice(packageId), installation.Directory).Save(settingsPath);
            staged.Commit();
            ReadSelectedLanguage();
        }
        finally
        {
            launcherModTimer.Start();
        }
    }

    private static void ValidateGameLanguage(string directory, ModDirectories locations, GameLanguageProfile profile, string? packageId, string? previousId, CancellationToken cancellationToken)
    {
        using IResourceSource source = GameDataSourceFactory.Open(directory, null, profile.ResourceLanguage);
        if (new GameLanguageProfile(source.Descriptor.Edition.ToString(), GameResourceLanguages.Normalize(source.Descriptor.Language) ?? source.Descriptor.Language) != profile)
        {
            throw new InvalidDataException("The selected game profile could not be opened.");
        }
        using ModRuntime validation = new(ApplicationInstallation.DataDirectory, format => ModCompatibility.IsCompatible(source.Descriptor.Edition, format), watch: false, edition: profile.Edition, language: profile.ResourceLanguage, modLocations: locations);
        bool prepared = validation.PreparePending(force: true);
        if (!validation.PendingLanguageMatches(packageId, previousId) || prepared && !validation.CommitPending())
        {
            throw new InvalidOperationException(validation.View.Diagnostics.FirstOrDefault(diagnostic => diagnostic.PackageId == packageId)?.Message ?? "The language package did not prepare for the selected game resources.");
        }
        ModPackage? selected = validation.Catalog.Packages.FirstOrDefault(package => package.Id == packageId);
        if (selected?.Format == ModFormat.Junction)
        {
            if (!JunctionEncodedResources.TryPrepare(validation.Catalog, out _, out IReadOnlyList<ModDiagnostic> encodedIssues))
            {
                throw new InvalidDataException(encodedIssues.First().Message);
            }
            if (!JunctionTextResources.TryPrepare(validation.Catalog, out _, out IReadOnlyList<ModDiagnostic> textIssues))
            {
                throw new InvalidDataException(textIssues.First().Message);
            }
        }
        if (selected?.HasLocalization == true)
        {
            LocalizationSourceCatalog? native = null;
            LocalizationPreparation localization = LocalizationRuntime.Prepare(validation.Catalog, validation.LocalizationOrder, source.Descriptor, cancellationToken, watchForPublication: true,
                nativeCatalog: () => native ??= LocalizationSourceExporter.Read(directory, source.Descriptor.Language, cancellationToken));
            using LocalizationInputRevision? inputs = localization.InputRevision;
            if (localization.RequiresSelection || !localization.RequestedOrder.Contains(selected.Id, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The language could not be applied to the active localization stack.");
            }
            var knownEntries = localization.Layers.Any(layer => layer.Project.Entries.Any(row => !string.IsNullOrEmpty(row.Target) && row.Category is not ("Fields" or "System")))
                ? (native ?? LocalizationSourceExporter.ReadNonFields(directory, source.Descriptor.Language, cancellationToken)).Entries.Where(row => row.Category != "Fields").ToDictionary(row => row.Key, StringComparer.Ordinal) : null;
            LocalizationBindingValidation.Validate(localization, source, cancellationToken, knownEntries);
            if (localization.Catalog.Artwork.Count > 0)
            {
                var artwork = LocalizationArtworkExporter.Read(directory, source.Descriptor.Language, cancellationToken).ToDictionary(image => image.Key);
                foreach (var image in localization.Catalog.Artwork.Values)
                {
                    if (!artwork.TryGetValue(image.Key, out var original) || !original.SourceSha256.Equals(image.SourceSha256, StringComparison.OrdinalIgnoreCase) || original.Width != image.LogicalWidth || original.Height != image.LogicalHeight)
                    {
                        throw new InvalidDataException($"{image.Key}: artwork does not match a supported original image in this resource profile.");
                    }
                }
            }
            inputs?.ValidatePublication();
        }
    }

    private bool IsCurrentGameLanguage(GameLanguageOffer offer)
    {
        return offer.Profile == LauncherLanguageProfile && (selectedLanguageChoice is { } choice && choice.Profile == offer.Profile
            ? choice.DisplayTag == offer.DisplayTag && choice.ProviderId == offer.Id && choice.Option == offer.Option
                && (offer.IsOriginal || !offer.RequiresAcquisition && offer.InstalledPackage?.Id == choice.PackageId && launcherMods?.PendingLanguageMatches(choice.PackageId, null) == true)
            : offer.IsOriginal);
    }

    internal static string LanguageEditionSummary(GameLanguageOffer offer, bool active)
    {
        string version = offer.Version is { Length: > 0 } value ? " " + value : "";
        string availability = !offer.CanSelect ? "Unavailable" : offer.IsInstalled ? "Installed" : "Download available";
        string download = offer.RequiresAcquisition || offer.CanUpdate ? " · " + LanguageDownloadSize(offer, offer.CanUpdate) : "";
        string update = offer.CanUpdate ? " · Update available" : "";
        string selection = active ? " · Current" : "";
        return $"{offer.Title}{version} · {offer.Source} · {availability}{update}{download}{selection}" + (offer.Requirement is { } required ? "\n" + required : "");
    }

    internal static string LanguageConfirmation(GameLanguageOffer offer, GameLanguageProfile current, bool update)
    {
        string nativeBase = offer.Profile is { } next ? $"{next.Edition} {LanguageName(next.ResourceLanguage)}" : "Unavailable";
        string baseChange = offer.Profile != current ? $"\nSwitches base from {current.Edition} {LanguageName(current.ResourceLanguage)}." : "";
        string version = offer.Version is { Length: > 0 } value ? " " + value : "";
        string status = offer.IsInstalled ? "Installed" : "Download available";
        List<string> lines = [$"{LanguageName(offer.DisplayTag)} · {offer.Title}{version} · {offer.Source} · {status}", "Native base: " + nativeBase + baseChange, "Total download: " + LanguageDownloadSize(offer, update)];
        foreach (GameLanguageDownloadMember member in offer.DownloadMembers)
        {
            bool download = DownloadLanguageMember(offer, member, update);
            bool provider = member == offer.DownloadMembers[0];
            string? memberVersion = provider && !download ? offer.Version : member.Version;
            string size = "installed";
            if (download)
            {
                size = member.Bytes is long bytes ? FormatLanguageBytes(bytes) : (provider ? PublishedLanguageSize(offer) : null) ?? "size unknown";
            }
            else if (!provider && !member.IsInstalled)
            {
                memberVersion = null;
                size = "installed version in use";
            }
            lines.Add($"• {member.Name}{(memberVersion is { Length: > 0 } ? " " + memberVersion : "")} · {size}");
        }
        return string.Join('\n', lines);
    }

    private static bool DownloadLanguageMember(GameLanguageOffer offer, GameLanguageDownloadMember member, bool update)
    {
        return !member.IsInstalled && (update || offer.RequiresAcquisition && (offer.InstalledPackage is null || member != offer.DownloadMembers[0]));
    }

    private static string LanguageDownloadSize(GameLanguageOffer offer, bool update)
    {
        GameLanguageDownloadMember[] downloads = offer.DownloadMembers.Where(member => DownloadLanguageMember(offer, member, update)).ToArray();
        if (downloads.Length == 0)
        {
            return "No download";
        }
        if (downloads.All(member => member.Bytes.HasValue))
        {
            return FormatLanguageBytes(downloads.Sum(member => member.Bytes!.Value));
        }
        return downloads.Length == 1 && downloads[0] == offer.DownloadMembers[0] ? PublishedLanguageSize(offer) ?? "Download size unknown" : "Download size unknown";
    }

    private static string? PublishedLanguageSize(GameLanguageOffer offer) => string.IsNullOrWhiteSpace(offer.PublishedSize) ? null : offer.PublishedSize;

    private static string FormatLanguageBytes(long bytes) => bytes < 1024 ? $"{bytes:N0} bytes" : bytes < 1048576 ? $"{bytes / 1024d:N1} KiB" : $"{bytes / 1048576d:N1} MiB";

    private static string LanguageName(string tag)
    {
        string? language = LanguagePackages.DisplayTag(tag);
        return language is null ? tag : CultureInfo.GetCultureInfo(language).EnglishName;
    }
}
