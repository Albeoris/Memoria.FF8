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
    private readonly IModService mods;
    private readonly string targetProfile;
    private readonly string targetLabel;
    private readonly bool supported;
    private readonly string? officialCacheDirectory;
    private readonly bool officialSupported;
    private readonly HttpClient? client;
    private OfficialCatalogSnapshot? official;
    private RemoteCatalogSnapshot? lastJunctionCatalog;
    private RemoteCatalogSnapshot? mergedCatalog;
    private bool catalogRefreshRequested;
    private readonly HashSet<Guid> refreshedMissingArtifacts = [];

    private readonly Action<Uri>? openDownloadPage;
    private readonly ConcurrentQueue<Action> commands = new();
    private readonly Dictionary<Guid, Inspection> inspections = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task worker;
    private Task? refresh;
    private ModLibrarySnapshot view;
    private string? error;
    private bool initialized;
    private bool downloadsAvailable;
    private bool officialDownloadsAvailable;
    private IReadOnlyList<ModLibraryInstallation> installations = Array.Empty<ModLibraryInstallation>();
    private RemoteCatalogSnapshot? reconciledCatalog;
    private IReadOnlyList<ModPackage>? reconciledPackages;
    private int disposed;

    public ModLibraryService(string cacheDirectory, ModPackageInstaller installer, IModService mods, string targetProfile, bool targetSupported, HttpClient? client = null, string? unavailableReason = null, Action<Uri>? openDownloadPage = null, string? officialCacheDirectory = null, bool officialSupported = false)
    {
        this.installer = installer;
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
        catalog = new RemoteModCatalogCoordinator(Path.Combine(cacheDirectory, "catalog"), client);
        downloads = new ModDownloadCoordinator(Path.Combine(cacheDirectory, "downloads"), client);
        if (officialSupported && officialCacheDirectory is not null)
        {
            officialDownloads = new ModDownloadCoordinator(installer.OfficialDownloadsDirectory, client);
        }
        view = new(0, catalog.Snapshot, [], targetProfile, supported) { TargetLabel = targetLabel };
        worker = Task.Run(RunAsync);
    }

    public ModLibrarySnapshot View => Volatile.Read(ref view);

    public void RefreshCatalog() => Post(() =>
    {
        catalogRefreshRequested = true;
    });

    public void Queue(RemoteModEntry entry) => Post(() => QueueCore(entry, false));

    public void DownloadAndEnable(RemoteModEntry entry) => Post(() => QueueCore(entry, true));

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
        ModPackageInstallResult? status = installer.GetStatus(entry);
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
                Observe(installer.DiscardAsync(oldPlan));
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
        installer.CancelPendingEnable(entry);
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
        if (inspections.TryGetValue(id, out Inspection? state) && state.Result?.RestartRequired == true)
        {
            throw new InvalidOperationException("The installation is already staged for startup. Its transaction must finish or recover before removal.");
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
                    await installer.DiscardAsync(completed).ConfigureAwait(false);
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
            Observe(installer.DiscardAsync(plan, lifetime.Token));
            inspections.Remove(id);
        }
        QueueForJob(id).Cancel(id);
    });

    public void Retry(Guid id) => Post(() =>
    {
        if (inspections.TryGetValue(id, out Inspection? state) && state.Error is not null)
        {
            if (state.Plan is { } oldPlan)
            {
                Observe(installer.DiscardAsync(oldPlan));
            }
            inspections.Remove(id);
        }
        else
        {
            ResumeJob(id);
        }
        error = null;
    });

    public void Install(Guid id) => Post(() => InstallCore(id, false));

    private void InstallCore(Guid id, bool enable)
    {
        if (!inspections.TryGetValue(id, out Inspection? state) || state.Plan is null || state.Installation is not null || state.Result is not null)
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
        ModPackageInstallResult? local = state.Plan.Entry.Official is not null ? installer.GetStatus(state.Plan.Entry) : null;
        bool repair = local is { RestartRequired: false, Installed: false } or { Version.Length: 0 };
        state.Installation = installer.RequestInstallAsync(state.Plan, enable || mods.AutoEnableNewMods, lifetime.Token, enable, repair);
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

    private async Task RunAsync()
    {
        try
        {
            await catalog.InitializeAsync(lifetime.Token).ConfigureAwait(false);
            if (officialCacheDirectory is not null)
            {
                SetOfficial(OfficialModCatalog.ReadCached(officialCacheDirectory));
                refresh = RefreshAsync(false, false);
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
                if (catalogRefreshRequested)
                {
                    catalogRefreshRequested = false;
                    if (refresh is null || refresh.IsCompleted)
                    {
                        refresh = RefreshAsync(true, true);
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
                            if (installer.GetStatus(entry) is { } status)
                            {
                                known.Add(new(entry.SourceId, entry.Id, status.Version, status.PackageId, status.RestartRequired) { Available = status.Installed || status.RestartRequired, Error = status.Error, PendingEnable = status.PendingEnable });
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
                            ModPackageInstallResult? installed = installer.GetStatus(job.Entry);
                            if (installed is not null && (installed.Installed || installed.RestartRequired) && job.Entry.CompareVersion(installed.Version) == 0)
                            {
                                state.Result = installed;
                                state.Error = installed.Error;
                            }
                            else if (job.ArchivePath is not null)
                            {
                                state.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                                state.Preparation = installer.InspectAsync(job.ArchivePath, job.Entry, state.Cancellation.Token);
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
                    if (job.InstallAfterDownload && state is { Plan: not null, Error: null, Result: null, Installation: null })
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
            foreach (Inspection state in inspections.Values)
            {
                Observe(state.Preparation);
                Observe(state.Installation);
            }
            Observe(refresh);
            officialDownloads?.Dispose();
            downloads.Dispose();
            catalog.Dispose();
        }
    }

    private void Publish(IReadOnlyList<DownloadJobSnapshot> jobs)
    {
        ModLibraryJob[] projected = jobs.Select(Project).ToArray();
        RemoteCatalogSnapshot current = CurrentCatalog();
        ModLibrarySnapshot previous = View;
        string? message = error ?? (!initialized ? "Preparing saved downloads…" : null);
        if (!ReferenceEquals(previous.Catalog, current) || !previous.Jobs.SequenceEqual(projected) || !previous.Installations.SequenceEqual(installations) || previous.Error != message)
        {
            Volatile.Write(ref view, new(previous.Revision + 1, current, Array.AsReadOnly(projected), targetProfile, supported, message) { Installations = installations, TargetLabel = targetLabel, OfficialSupported = officialSupported && (officialDownloads is null || officialDownloadsAvailable) });
        }
    }

    private ModLibraryJob Project(DownloadJobSnapshot job)
    {
        inspections.TryGetValue(job.JobId, out Inspection? state);
        bool pendingInstall = installations.Any(item => item.SourceId == job.Entry.SourceId && item.Id == job.Entry.Id && item.Pending && job.Entry.CompareVersion(item.Version) == 0);
        bool installedCache = state?.Error is null && (job.Phase is ModDownloadPhase.Paused or ModDownloadPhase.ReadyToInstall) && installations.Any(item => item.SourceId == job.Entry.SourceId && item.Id == job.Entry.Id && item.Available && !item.Pending && job.Entry.CompareVersion(item.Version) == 0);
        string phase = job.Phase switch
        {
            ModDownloadPhase.ReadyToInstall => state?.Error is not null ? "Needs attention" : state?.Result?.Installed == true ? "Installed" : state?.Result?.RestartRequired == true ? "Restart required" : state?.Installation is not null ? "Preparing installation" : state?.Plan is not null ? "Ready to install" : "Inspecting package",
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
            phase = "Restart required";
        }
        string? details = state?.Plan is { } plan ? $"Version {plan.Version} · {plan.PackageId}. Installation is applied before resource loading on the next launch. {(mods.AutoEnableNewMods ? "New installations will be enabled." : "New installations will remain disabled.")} Existing choices are preserved. Integrity: {(job.Entry.Official is null ? "local SHA-256" : "verified publisher SHA-256")}." : job.Details ?? job.Message;
        if (installedCache)
        {
            details = "The package is installed on disk. Removing this cached download reclaims download space and leaves the installed mod intact. Activation is shown in Installed.";
        }
        if (pendingInstall)
        {
            details = "The checked installation is already staged. Close all game instances and start the game again to apply it before resource loading. No network action is required.";
        }
        bool transferring = job.Phase is ModDownloadPhase.Queued or ModDownloadPhase.Resolving or ModDownloadPhase.Downloading or ModDownloadPhase.WaitingToRetry;
        string? failure = state?.Error ?? (job.Phase is ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction ? job.Message : null);
        return new(job.JobId, job.Entry, phase, job.CompletedBytes, job.TotalBytes, job.BytesPerSecond, failure, details,
            transferring,
            job.Phase == ModDownloadPhase.Paused && !installedCache && !pendingInstall,
            !pendingInstall && (installedCache || transferring || job.Phase is ModDownloadPhase.Verifying or ModDownloadPhase.Paused or ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction || job.Phase == ModDownloadPhase.ReadyToInstall && state?.Result is null && state?.Installation is null),
            !pendingInstall && !installedCache && state?.Plan is not null && state.Error is null && state.Result is null && state.Installation is null,
            state?.Error is not null || job.Phase is ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction)
        {
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            lifetime.Cancel();
            Observe(worker);
        }
    }
}
