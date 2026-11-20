using System.Collections.Concurrent;

namespace Memoria.FF8.Mods;

/// <summary>Serializes library commands on a worker; the owner thread reads immutable presentation state.</summary>
public sealed class ModLibraryService : IModLibraryService, IDisposable
{
    private sealed class Inspection
    {
        public Task<ModPackageInstallPlan>? Preparation;
        public ModPackageInstallPlan? Plan;
        public Task<ModPackageInstallResult>? Installation;
        public ModPackageInstallResult? Result;
        public string? Error;
        public CancellationTokenSource? Cancellation;
    }

    private readonly RemoteModCatalogCoordinator catalog;
    private readonly ModDownloadCoordinator downloads;
    private readonly ModDownloadCoordinator? officialDownloads;
    private readonly ModPackageInstaller installer;
    private readonly IReadOnlyList<ModPackageInstaller> installers;
    private readonly IModService mods;
    private readonly string targetProfile;
    private readonly string targetLabel;
    private readonly bool supported;
    private readonly string? officialCacheDirectory;
    private readonly bool officialSupported;
    private readonly HttpClient? client;
    private readonly Func<CancellationToken, Task<bool>>? applyPendingChanges;
    private DateTimeOffset nextApplyAttempt;
    private OfficialCatalogSnapshot? official;
    private RemoteCatalogSnapshot? lastJunctionCatalog;
    private RemoteCatalogSnapshot? mergedCatalog;
    private bool catalogRefreshRequested;
    private readonly HashSet<Guid> refreshedMissingArtifacts = [];
    private readonly HashSet<Guid> pendingRetries = [];

    private readonly Action<Uri>? openDownloadPage;
    private readonly ConcurrentQueue<Action> commands = new();
    private readonly Dictionary<Guid, Inspection> inspections = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task worker;
    private Task? refresh;
    private ModLibrarySnapshot view;
    private string? error;
    private string? applyError;
    private bool initialized;
    private bool downloadsAvailable;
    private bool officialDownloadsAvailable;
    private IReadOnlyList<ModLibraryInstallation> installations = Array.Empty<ModLibraryInstallation>();
    private RemoteCatalogSnapshot? reconciledCatalog;
    private IReadOnlyList<ModPackage>? reconciledPackages;
    private int disposed;

    /// <param name="applyPendingChanges">Optional owner callback that applies staged files only while holding an exclusive resource-directory lease. Return false while readers prevent application; full runtime validation remains the game host's responsibility.</param>
    public ModLibraryService(string cacheDirectory, ModPackageInstaller installer, IModService mods, string targetProfile, bool targetSupported, HttpClient? client = null, string? unavailableReason = null, Action<Uri>? openDownloadPage = null, string? officialCacheDirectory = null, bool officialSupported = false, Func<CancellationToken, Task<bool>>? applyPendingChanges = null, IReadOnlyList<ModPackageInstaller>? previousInstallers = null, string? officialDownloadsDirectory = null, bool refreshCatalogOnStartup = false)
    {
        this.installer = installer;
        installers = (previousInstallers ?? []).Prepend(installer).Distinct().ToArray();
        this.mods = mods;
        this.targetProfile = targetProfile;
        string[] targetParts = targetProfile.Split('/');
        targetLabel = string.Join(" · ", targetParts.Take(2)).Replace(" · en", " · English", StringComparison.Ordinal);
        supported = targetSupported;
        error = unavailableReason;
        this.openDownloadPage = openDownloadPage;
        this.officialCacheDirectory = officialCacheDirectory;
        this.officialSupported = officialSupported;
        this.client = client;
        this.applyPendingChanges = applyPendingChanges;
        catalog = new RemoteModCatalogCoordinator(Path.Combine(cacheDirectory, "catalog"), client);
        downloads = new ModDownloadCoordinator(Path.Combine(cacheDirectory, "downloads"), client);
        if (officialSupported && officialCacheDirectory is not null)
        {
            officialDownloads = new ModDownloadCoordinator(officialDownloadsDirectory ?? installer.OfficialDownloadsDirectory, client);
        }
        view = new(0, catalog.Snapshot, [], targetProfile, supported) { TargetLabel = targetLabel };
        worker = Task.Run(() => RunAsync(refreshCatalogOnStartup));
    }

    public ModLibrarySnapshot View => Volatile.Read(ref view);

    public Task<ModPreviewDocument> OpenPreviewAsync(RemoteModEntry? entry, ModPackage? installed, CancellationToken cancellationToken = default)
    {
        ModPackageInstallPlan? plan = entry is null ? null : View.Jobs.LastOrDefault(job => job.CanPreview && job.Entry.SourceId == entry.SourceId && job.Entry.Id == entry.Id && job.Entry.Version == entry.Version)?.PreviewPlan;
        if (plan is not null)
        {
            return plan.Installer.OpenPreviewAsync(plan, cancellationToken);
        }
        if (installed is not null && ModPreviewDocument.CanOpenInstalled(installed))
        {
            ModCatalogSnapshot preferences = mods.Catalog;
            return Task.Run(() => ModPreviewDocument.Open(installed, cancellationToken, preferences: preferences), cancellationToken);
        }
        return Task.FromException<ModPreviewDocument>(new InvalidOperationException("Download and check this release to preview it."));
    }

    public void RefreshCatalog() => Post(() =>
    {
        catalogRefreshRequested = true;
    });

    public void Queue(RemoteModEntry entry) => Post(() => QueueCore(entry, false));

    public void DownloadAndEnable(RemoteModEntry entry) => Post(() => QueueCore(entry, true));

    private (ModPackageInstaller Installer, ModPackageInstallResult? Status) ResolveInstallation(RemoteModEntry entry)
    {
        ModPackageInstaller owner = installer;
        ModPackageInstallResult? status = null;
        foreach (ModPackageInstaller candidate in installers)
        {
            if (candidate.GetStatus(entry) is not { } found)
            {
                continue;
            }
            if (status is not null)
            {
                throw new InvalidOperationException("More than one mod directory has managed state for this package. Resolve the duplicate installations before making changes.");
            }
            owner = candidate;
            status = found;
        }
        return (owner, status);
    }

    private void QueueCore(RemoteModEntry entry, bool enable)
    {
        bool native = entry.SourceId == OfficialModCatalog.SourceId;
        ModDownloadCoordinator queue = native && officialDownloads is not null ? officialDownloads : downloads;
        if (native && officialDownloads is not null ? !officialDownloadsAvailable : !downloadsAvailable)
        {
            throw new InvalidOperationException("Downloads are unavailable. Check the library error and close other instances before restarting.");
        }
        if (entry.Official is null ? !supported : !officialSupported)
        {
            throw new NotSupportedException("Downloads cannot be installed for this target or mod directory.");
        }
        if (installer.CompatibilityError(entry) is { } unavailable)
        {
            throw new NotSupportedException(unavailable);
        }
        if (entry.Official is not null && !CurrentCatalog().Entries.Any(candidate => candidate.Id == entry.Id && candidate.SourceId == entry.SourceId && candidate.Version == entry.Version && candidate.Official?.Latest?.Artifact == entry.Official.Latest?.Artifact))
        {
            throw new InvalidOperationException("This release has changed. Review the new download in the catalog.");
        }
        ModPackageInstallResult? status = ResolveInstallation(entry).Status;
        if (status?.PendingRemoval == true)
        {
            throw new InvalidOperationException("This mod is waiting to be uninstalled. Wait for removal before downloading it again.");
        }
        if (mods.InstalledPackages.Any(package => package.JunctionMetadata?.Id == entry.Id))
        {
            if (status is null || mods.InstalledPackages.Any(package => package.JunctionMetadata?.Id == entry.Id && package.Id != status.PackageId))
            {
                throw new InvalidOperationException("This mod is already installed manually. View its installed settings; automatic replacement is unavailable.");
            }
        }
        Guid id = queue.Enqueue(entry, native ? OfficialModCatalog.SourceId : targetProfile, enable);
        if (status is not { Installed: true } and not { RestartRequired: true } && inspections.TryGetValue(id, out Inspection? oldState) && oldState.Preparation is null && oldState.Installation is null)
        {
            inspections.Remove(id);
            if (oldState.Plan is { } oldPlan)
            {
                Observe(oldPlan.Installer.DiscardAsync(oldPlan));
            }
        }
        if (queue.Snapshot.FirstOrDefault(job => job.JobId == id)?.Phase is ModDownloadPhase.Paused or ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction)
        {
            queue.Resume(id);
        }
        error = null;
    }

    public void CancelPendingEnable(RemoteModEntry entry) => Post(() =>
    {
        ResolveInstallation(entry).Installer.CancelPendingEnable(entry);
        reconciledCatalog = null;
    });

    private IReadOnlyList<DownloadJobSnapshot> Jobs => officialDownloadsAvailable ? downloads.Snapshot.Concat(officialDownloads!.Snapshot).ToArray() : downloads.Snapshot;

    private ModDownloadCoordinator QueueForJob(Guid id) => officialDownloadsAvailable && officialDownloads!.Snapshot.Any(job => job.JobId == id) ? officialDownloads : downloads;

    public void Pause(Guid id) => Post(() => QueueForJob(id).Pause(id));
    public void Resume(Guid id) => Post(() => ResumeJob(id));

    private void ResumeJob(Guid id)
    {
        ModDownloadCoordinator queue = QueueForJob(id);
        DownloadJobSnapshot? job = queue.Snapshot.FirstOrDefault(item => item.JobId == id);
        if (job?.Entry.Official is not null && installer.CompatibilityError(job.Entry) is { } reason)
        {
            throw new NotSupportedException(reason);
        }
        queue.Resume(id);
    }
    public void OpenDownloadPage(Guid id) => Post(() =>
    {
        DownloadJobSnapshot? job = Jobs.FirstOrDefault(candidate => candidate.JobId == id);
        if (job?.Phase != ModDownloadPhase.NeedsUserAction || openDownloadPage is null)
        {
            return;
        }
        foreach (string link in job.Entry.Links)
        {
            try
            {
                (Uri uri, bool _) = ModDownloadTransport.DecodeLink(link);
                openDownloadPage(uri);
                return;
            }
            catch (NotSupportedException)
            {
            }
        }
    });
    public void Cancel(Guid id) => Post(() =>
    {
        if (inspections.TryGetValue(id, out Inspection? state) && state.Result is { RestartRequired: true, PendingRemoval: false })
        {
            throw new InvalidOperationException("The installation is already staged. Its transaction must finish or recover before removal.");
        }
        if (state?.Installation is not null)
        {
            throw new InvalidOperationException("Installation preparation is finishing. Its transaction must finish before removal.");
        }
        if (state?.Preparation is { } preparation)
        {
            state.Cancellation?.Cancel();
            Observe(Task.Run(async () =>
            {
                try
                {
                    ModPackageInstallPlan completed = await preparation.ConfigureAwait(false);
                    await completed.Installer.DiscardAsync(completed).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    state.Cancellation?.Dispose();
                }
            }));
            inspections.Remove(id);
        }
        if (state?.Plan is { } plan)
        {
            Observe(plan.Installer.DiscardAsync(plan, lifetime.Token));
            inspections.Remove(id);
        }
        QueueForJob(id).Cancel(id);
    });

    public void DeleteDownload(Guid id) => Cancel(id);

    public void Uninstall(RemoteModEntry entry) => Post(() =>
    {
        if (Jobs.Where(job => job.Entry.SourceId == entry.SourceId && job.Entry.Id == entry.Id).Any(job => inspections.TryGetValue(job.JobId, out Inspection? inspection) && inspection.Installation is not null))
        {
            throw new InvalidOperationException("Installation is finishing. Wait before uninstalling this mod.");
        }
        ResolveInstallation(entry).Installer.RequestUninstall(entry);
        reconciledCatalog = null;
        error = null;
    });

    public void Retry(Guid id) => Post(() =>
    {
        if (View.Jobs.Any(job => job.Id == id && job.CanRetry) && (!inspections.TryGetValue(id, out Inspection? state) || state is { Installation: null, Result: null }))
        {
            pendingRetries.Add(id);
            catalogRefreshRequested = true;
            error = null;
        }
    });

    private bool RetryAfterRefresh(Guid id)
    {
        ModDownloadCoordinator queue = QueueForJob(id);
        DownloadJobSnapshot? job = queue.Snapshot.FirstOrDefault(item => item.JobId == id);
        if (job is null || job.Phase == ModDownloadPhase.Cancelled)
        {
            return true;
        }
        RemoteModEntry entry = job.Entry;
        if (entry.SourceId == RemoteModCatalogAdapter.SourceId)
        {
            entry = catalog.Snapshot.Entries.FirstOrDefault(candidate => candidate.Id == entry.Id && !candidate.IsStale && candidate.Links.Count > 0) ?? entry;
        }
        if (entry.Official is not null && installer.CompatibilityError(entry) is { } reason)
        {
            throw new NotSupportedException(reason);
        }
        if (!queue.TryRetry(id, entry, recheckCompleted: true))
        {
            return false;
        }
        if (inspections.Remove(id, out Inspection? state) && state.Plan is { } oldPlan)
        {
            Observe(oldPlan.Installer.DiscardAsync(oldPlan));
        }
        return true;
    }

    public void Install(Guid id) => Install(id, false);

    public void Install(Guid id, bool allowVersionMismatch) => Post(() => InstallCore(id, false, allowVersionMismatch));

    private void InstallCore(Guid id, bool enable, bool allowVersionMismatch = false)
    {
        if (pendingRetries.Contains(id) || !inspections.TryGetValue(id, out Inspection? state) || state.Plan is null || state.Installation is not null || state.Result is not null)
        {
            return;
        }
        if (state.Plan.Entry.Official is null ? !supported : !officialSupported)
        {
            throw new InvalidOperationException("Installation is unavailable for this target or mod directory.");
        }
        StringComparison pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (mods.InstalledPackages.Any(package => package.JunctionMetadata?.Id == state.Plan.Entry.Id && (package.Id != state.Plan.PackageId || !Path.GetFullPath(package.RootDirectory).Equals(Path.GetFullPath(state.Plan.Destination), pathComparison))))
        {
            throw new InvalidOperationException("A manual installation or another variant already owns this mod identity. No files were replaced.");
        }
        if (state.Plan.Entry.Official is not null && mods.InstalledPackages.Any(package => package.Id.Equals(state.Plan.PackageId, StringComparison.OrdinalIgnoreCase) && !Path.GetFullPath(package.RootDirectory).Equals(Path.GetFullPath(state.Plan.Destination), pathComparison)))
        {
            throw new InvalidOperationException("Another mod directory owns this component. Resolve that installation before replacing it.");
        }
        (ModPackageInstaller owner, ModPackageInstallResult? local) = ResolveInstallation(state.Plan.Entry);
        if (!ReferenceEquals(owner, state.Plan.Installer))
        {
            throw new InvalidOperationException("The installation location changed. Retry to check the download again.");
        }
        bool repair = state.Plan.Entry.Official is not null && local is ({ RestartRequired: false, Installed: false } or { Version.Length: 0 });
        string? relocateTo = null;
        RemoteModEntry accepted = state.Plan.Entry with { Version = state.Plan.Version };
        if (!ReferenceEquals(owner, installer) && local is { Version.Length: > 0 } && accepted.CompareVersion(local.Version) > 0)
        {
            relocateTo = installer.ModsRoot;
        }
        state.Installation = owner.RequestInstallAsync(state.Plan, enable || mods.AutoEnableNewMods, lifetime.Token, enable, repair, relocateTo, allowVersionMismatch);
    }

    private async Task RefreshAsync(bool includeJunction, bool force)
    {
        Task junction = includeJunction ? catalog.RefreshAsync(lifetime.Token) : Task.CompletedTask;
        if (officialCacheDirectory is not null)
        {
            SetOfficial(await OfficialModCatalog.RefreshAsync(officialCacheDirectory, client, force, lifetime.Token).ConfigureAwait(false));
        }
        await junction.ConfigureAwait(false);
    }

    private void SetOfficial(OfficialCatalogSnapshot next)
    {
        Volatile.Write(ref official, next);
    }

    private OfficialCatalogSnapshot? mergedOfficial;
    private IReadOnlyList<ModPackage>? mergedPackages;

    private RemoteCatalogSnapshot CurrentCatalog()
    {
        RemoteCatalogSnapshot junction = catalog.Snapshot;
        OfficialCatalogSnapshot? nextOfficial = Volatile.Read(ref official);
        if (nextOfficial is null)
        {
            return junction;
        }
        IReadOnlyList<ModPackage> localPackages = mods.InstalledPackages;
        if (ReferenceEquals(lastJunctionCatalog, junction) && ReferenceEquals(mergedOfficial, nextOfficial) && ReferenceEquals(mergedPackages, localPackages))
        {
            return mergedCatalog!;
        }
        lastJunctionCatalog = junction;
        mergedOfficial = nextOfficial;
        mergedPackages = localPackages;
        IEnumerable<OfficialModComponent> known = nextOfficial.Components.Concat(localPackages.Select(package => package.Official).OfType<OfficialModComponent>().Select(component => component with { Latest = null }))
            .DistinctBy(component => component.Id);
        RemoteModEntry[] native = known.Select(component => component.ToEntry()).Select(entry => entry with { CompatibilityError = installer.CompatibilityError(entry) }).ToArray();
        mergedCatalog = junction with
        {
            Entries = Array.AsReadOnly(junction.Entries.Concat(native).ToArray()),
            Error = junction.Error ?? nextOfficial.Error,
            FetchedAt = junction.FetchedAt ?? nextOfficial.ValidatedAt
        };
        return mergedCatalog;
    }

    private async Task RefreshPrunedArtifacts(IReadOnlyList<DownloadJobSnapshot> jobs)
    {
        if (officialCacheDirectory is null)
        {
            return;
        }
        DownloadJobSnapshot[] stale = jobs.Where(job => job.Entry.Official is not null && job.Phase == ModDownloadPhase.Failed && job.HttpStatus is 404 or 410 && refreshedMissingArtifacts.Add(job.JobId)).ToArray();
        if (stale.Length == 0)
        {
            return;
        }
        SetOfficial(await OfficialModCatalog.RefreshAsync(officialCacheDirectory, client, true, lifetime.Token, bypassDebounce: true).ConfigureAwait(false));
        foreach (DownloadJobSnapshot job in stale)
        {
            RemoteModEntry? next = CurrentCatalog().Entries.FirstOrDefault(entry => entry.SourceId == job.Entry.SourceId && entry.Id == job.Entry.Id && entry.CompareVersion(job.Entry.Version) is > 0);
            if (next is null || next.CompatibilityError is not null || next.Official?.Latest?.Artifact.Size > job.Entry.Official?.Latest?.Artifact.Size)
            {
                error = next?.CompatibilityError ?? "The release is unavailable. Review the current version and download size in the catalog.";
                continue;
            }
            ModDownloadCoordinator queue = QueueForJob(job.JobId);
            queue.Cancel(job.JobId);
            Guid newJob = queue.Enqueue(next, OfficialModCatalog.SourceId, job.InstallAfterDownload);
            refreshedMissingArtifacts.Add(newJob);
        }
    }

    private void Post(Action command)
    {
        if (Volatile.Read(ref disposed) == 0)
        {
            commands.Enqueue(command);
        }
    }

    private async Task RunAsync(bool refreshCatalogOnStartup)
    {
        try
        {
            await catalog.InitializeAsync(lifetime.Token).ConfigureAwait(false);
            if (officialCacheDirectory is not null)
            {
                SetOfficial(OfficialModCatalog.ReadCached(officialCacheDirectory));
            }
            if (refreshCatalogOnStartup || officialCacheDirectory is not null)
            {
                refresh = RefreshAsync(refreshCatalogOnStartup, force: refreshCatalogOnStartup);
            }
            try
            {
                await downloads.InitializeAsync(lifetime.Token).ConfigureAwait(false);
                downloadsAvailable = true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                error = SafeError(exception);
            }
            if (officialDownloads is not null)
            {
                try
                {
                    await officialDownloads.InitializeAsync(lifetime.Token).ConfigureAwait(false);
                    officialDownloadsAvailable = true;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    error = SafeError(exception);
                }
            }
            initialized = true;
            while (!lifetime.IsCancellationRequested)
            {
                while (commands.TryDequeue(out Action? command))
                {
                    try
                    {
                        command();
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        error = SafeError(exception);
                    }
                }
                if (refresh is { IsCompleted: true })
                {
                    try
                    {
                        await refresh.ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        error = SafeError(exception);
                    }
                    refresh = null;
                }
                if (refresh is null)
                {
                    if (catalogRefreshRequested)
                    {
                        catalogRefreshRequested = false;
                        refresh = RefreshAsync(true, true);
                    }
                    else if (pendingRetries.Count > 0)
                    {
                        foreach (Guid id in pendingRetries.ToArray())
                        {
                            try
                            {
                                if (RetryAfterRefresh(id))
                                {
                                    pendingRetries.Remove(id);
                                }
                            }
                            catch (Exception exception) when (exception is not OperationCanceledException)
                            {
                                error = SafeError(exception);
                                pendingRetries.Remove(id);
                            }
                        }
                    }
                }
                IReadOnlyList<ModPackage> localPackages = mods.InstalledPackages;
                RemoteCatalogSnapshot currentCatalog = CurrentCatalog();
                if (!ReferenceEquals(reconciledCatalog, currentCatalog) || !ReferenceEquals(reconciledPackages, localPackages))
                {
                    reconciledCatalog = currentCatalog;
                    reconciledPackages = localPackages;
                    List<ModLibraryInstallation> known = [];
                    foreach (RemoteModEntry entry in reconciledCatalog.Entries.Concat(Jobs.Select(job => job.Entry)).DistinctBy(entry => (entry.SourceId, entry.Id)))
                    {
                        try
                        {
                            if (ResolveInstallation(entry).Status is { } status)
                            {
                                known.Add(new(entry.SourceId, entry.Id, status.Version, status.PackageId, status.RestartRequired)
                                {
                                    Available = status.Installed || status.RestartRequired,
                                    Error = status.Error,
                                    PendingEnable = status.PendingEnable,
                                    PendingRemoval = status.PendingRemoval,
                                    CanUninstall = status.CanUninstall,
                                    NeedsFileApplication = status.RestartRequired && !status.Installed
                                });
                            }
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            error = SafeError(exception);
                        }
                    }
                    installations = known.AsReadOnly();
                    foreach (Inspection state in inspections.Values.Where(state => state.Result?.Installed == true))
                    {
                        if (known.FirstOrDefault(item => item.PackageId == state.Result!.PackageId) is { Available: false } missing)
                        {
                            state.Result = null;
                            state.Error = missing.Error;
                        }
                    }
                }
                IReadOnlyList<DownloadJobSnapshot> jobs = Jobs;
                await RefreshPrunedArtifacts(jobs).ConfigureAwait(false);
                foreach (DownloadJobSnapshot job in jobs.Where(job => job.Phase == ModDownloadPhase.ReadyToInstall))
                {
                    if (!inspections.TryGetValue(job.JobId, out Inspection? state))
                    {
                        state = new();
                        inspections.Add(job.JobId, state);
                        try
                        {
                            (ModPackageInstaller owner, ModPackageInstallResult? installed) = ResolveInstallation(job.Entry);
                            if (installed is not null && (installed.Installed || installed.RestartRequired) && job.Entry.CompareVersion(installed.Version) == 0)
                            {
                                state.Result = installed;
                                state.Error = installed.Error;
                            }
                            else if (job.ArchivePath is not null)
                            {
                                state.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                                state.Preparation = owner.InspectAsync(job.ArchivePath, job.Entry, state.Cancellation.Token);
                            }
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            state.Error = SafeError(exception);
                        }
                    }
                    if (state.Preparation is { IsCompleted: true })
                    {
                        try
                        {
                            state.Plan = await state.Preparation.ConfigureAwait(false);
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            state.Error = SafeError(exception);
                        }
                        state.Preparation = null;
                        state.Cancellation?.Dispose();
                        state.Cancellation = null;
                    }
                    if (job.InstallAfterDownload && state is { Plan: { VersionWarning: null, InstallationBlocker: null }, Error: null, Result: null, Installation: null })
                    {
                        try
                        {
                            InstallCore(job.JobId, true);
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            state.Error = SafeError(exception);
                        }
                    }
                    if (state.Installation is { IsCompleted: true })
                    {
                        try
                        {
                            state.Result = await state.Installation.ConfigureAwait(false);
                            state.Error = state.Result.Error;
                            reconciledCatalog = null;
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            state.Error = SafeError(exception);
                        }
                        state.Installation = null;
                    }
                }
                if (applyPendingChanges is not null && DateTimeOffset.UtcNow >= nextApplyAttempt && (installations.Any(item => item.NeedsFileApplication) || inspections.Values.Any(state => state.Result is { RestartRequired: true, Installed: false })))
                {
                    nextApplyAttempt = DateTimeOffset.UtcNow.AddSeconds(1);
                    try
                    {
                        if (await applyPendingChanges(lifetime.Token).ConfigureAwait(false))
                        {
                            applyError = null;
                            reconciledCatalog = null;
                            foreach (DownloadJobSnapshot job in jobs)
                            {
                                if (inspections.TryGetValue(job.JobId, out Inspection? state) && state.Result is not null)
                                {
                                    state.Result = ResolveInstallation(job.Entry).Status;
                                    state.Error = state.Result?.Error;
                                    if (state.Result is null)
                                    {
                                        inspections.Remove(job.JobId);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        applyError = SafeError(exception);
                    }
                }
                Publish(jobs);
                await Task.Delay(125, lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            error = SafeError(exception);
            Publish(Jobs);
        }
        finally
        {
            Task[] pending = inspections.Values.SelectMany(state => new Task?[] { state.Preparation, state.Installation }).Append(refresh).OfType<Task>().ToArray();
            try
            {
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Durable workers retain their own failed or cancelled outcome.
            }
            if (officialDownloads is not null)
            {
                await officialDownloads.StopAsync().ConfigureAwait(false);
            }
            await downloads.StopAsync().ConfigureAwait(false);
            catalog.Dispose();
        }
    }

    private void Publish(IReadOnlyList<DownloadJobSnapshot> jobs)
    {
        ModLibraryJob[] projected = jobs.Select(Project).ToArray();
        RemoteCatalogSnapshot current = CurrentCatalog();
        ModLibrarySnapshot previous = View;
        string? message = error ?? applyError ?? (!initialized ? "Preparing saved downloads…" : null);
        if (!ReferenceEquals(previous.Catalog, current) || !previous.Jobs.SequenceEqual(projected) || !previous.Installations.SequenceEqual(installations) || previous.Error != message)
        {
            Volatile.Write(ref view, new(previous.Revision + 1, current, Array.AsReadOnly(projected), targetProfile, supported, message) { Installations = installations, TargetLabel = targetLabel, OfficialSupported = officialSupported && (officialDownloads is null || officialDownloadsAvailable) });
        }
    }

    private ModLibraryJob Project(DownloadJobSnapshot job)
    {
        inspections.TryGetValue(job.JobId, out Inspection? state);
        RemoteModEntry accepted = job.Entry with { Version = state?.Plan?.Version ?? state?.Result?.Version ?? job.Entry.Version };
        ModLibraryInstallation? pendingInstallation = installations.FirstOrDefault(item => item.SourceId == job.Entry.SourceId && item.Id == job.Entry.Id && item.Pending && !item.PendingRemoval && accepted.CompareVersion(item.Version) == 0);
        bool pendingInstall = pendingInstallation is not null;
        bool pendingRemoval = installations.Any(item => item.SourceId == job.Entry.SourceId && item.Id == job.Entry.Id && item.PendingRemoval);
        bool installedCache = state?.Error is null && (job.Phase is ModDownloadPhase.Paused or ModDownloadPhase.ReadyToInstall) && installations.Any(item => item.SourceId == job.Entry.SourceId && item.Id == job.Entry.Id && item.Available && !item.Pending && accepted.CompareVersion(item.Version) == 0);
        bool retrying = pendingRetries.Contains(job.JobId);
        string? versionWarning = state?.Plan?.VersionWarning;
        string? inspectionProblem = state?.Error ?? state?.Plan?.InstallationBlocker;
        string phase = job.Phase switch
        {
            ModDownloadPhase.ReadyToInstall => inspectionProblem is not null ? "Needs attention" : state?.Result?.Installed == true ? "Installed" : state?.Result?.RestartRequired == true ? "Waiting to apply" : state?.Installation is not null ? "Preparing installation" : state?.Plan is not null ? "Ready to install" : "Inspecting package",
            ModDownloadPhase.Verifying => "Checking download",
            ModDownloadPhase.WaitingToRetry => "Waiting to retry",
            ModDownloadPhase.NeedsUserAction => "Provider action required",
            _ => job.Phase.ToString()
        };
        if (installedCache)
        {
            phase = "Installed";
        }
        if (pendingInstall)
        {
            phase = "Waiting to apply";
        }
        string? details = job.Details ?? job.Message;
        if (state?.Plan is { } plan)
        {
            details = $"Version {plan.Version} · {plan.PackageId}.";
            if (plan.InstallationBlocker is null)
            {
                details += $" Installation is applied when its resource directory is available. {(mods.AutoEnableNewMods ? "New installations will be enabled." : "New installations will remain disabled.")} Existing choices are preserved. Integrity: {(job.Entry.Official is null ? "local SHA-256" : "verified publisher SHA-256")}.";
            }
        }
        if (installedCache)
        {
            details = "The package is installed on disk. Removing this cached download reclaims download space and leaves the installed mod intact. Activation is shown in Installed.";
        }
        if (pendingInstall)
        {
            details = pendingInstallation!.NeedsFileApplication
                ? "The checked installation is staged and waiting for its resource directory to become available. The launcher applies it while no game is using that directory. No network action is required."
                : "Start the game to validate the update and finish moving the mod to the selected directory.";
        }
        if (pendingRemoval)
        {
            phase = "Uninstall pending";
            details = "This managed mod will be removed when no game is using its resource directory. Current game resources remain available until then.";
        }
        if (retrying)
        {
            phase = "Refreshing catalog";
        }
        bool transferring = job.Phase is ModDownloadPhase.Queued or ModDownloadPhase.Resolving or ModDownloadPhase.Downloading or ModDownloadPhase.WaitingToRetry;
        string? failure = inspectionProblem ?? (job.Phase is ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction ? job.Message : null);
        return new(job.JobId, job.Entry, phase, job.CompletedBytes, job.TotalBytes, job.BytesPerSecond, failure, details,
            transferring,
            job.Phase == ModDownloadPhase.Paused && !installedCache && !pendingInstall,
            !pendingInstall && (installedCache || transferring || job.Phase is ModDownloadPhase.Verifying or ModDownloadPhase.Paused or ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction || job.Phase == ModDownloadPhase.ReadyToInstall && state?.Result is null && state?.Installation is null),
            !retrying && !pendingRemoval && !pendingInstall && !installedCache && inspectionProblem is null && state is { Plan: not null, Result: null, Installation: null },
            !retrying && (inspectionProblem is not null || job.Phase is ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction || !pendingInstall && !installedCache && versionWarning is not null && state is { Installation: null, Result: null }))
        {
            VersionWarning = pendingInstall || installedCache || state?.Result is not null ? null : versionWarning,
            PreviewPlan = state is { Error: null, Installation: null, Result: null } ? state.Plan : null,
            CanDeleteDownload = !pendingInstall && state?.Installation is null && (job.Phase is ModDownloadPhase.ReadyToInstall or ModDownloadPhase.Paused) && job.ArchivePath is not null,
            CanOpenDownloadPage = job.Phase == ModDownloadPhase.NeedsUserAction && openDownloadPage is not null
        };
    }

    private static string SafeError(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => "Access was denied. Check permissions for the download and mod directories.",
            IOException => "The library could not complete a file operation. Check free space, permissions and other running instances.",
            _ => exception.Message
        };
    }

    private static void Observe(Task? task)
    {
        if (task is not null)
        {
            _ = task.ContinueWith(completed => _ = completed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    /// <summary>Cancels library work and waits until catalog and download queue ownership has been released.</summary>
    public Task StopAsync()
    {
        Dispose();
        return worker;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            lifetime.Cancel();
            Observe(worker);
        }
    }
}
