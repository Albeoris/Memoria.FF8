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
    private readonly ModPackageInstaller installer;
    private readonly IModService mods;
    private readonly string targetProfile;
    private readonly string targetLabel;
    private readonly bool supported;
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
    private IReadOnlyList<ModLibraryInstallation> installations = Array.Empty<ModLibraryInstallation>();
    private RemoteCatalogSnapshot? reconciledCatalog;
    private int disposed;

    public ModLibraryService(string cacheDirectory, ModPackageInstaller installer, IModService mods, string targetProfile, bool targetSupported, HttpClient? client = null, string? unavailableReason = null, Action<Uri>? openDownloadPage = null)
    {
        this.installer = installer;
        this.mods = mods;
        this.targetProfile = targetProfile;
        string[] targetParts = targetProfile.Split('/');
        targetLabel = string.Join(" · ", targetParts.Take(2)).Replace(" · en", " · English", StringComparison.Ordinal);
        supported = targetSupported;
        error = unavailableReason;
        this.openDownloadPage = openDownloadPage;
        catalog = new RemoteModCatalogCoordinator(Path.Combine(cacheDirectory, "catalog"), client);
        downloads = new ModDownloadCoordinator(Path.Combine(cacheDirectory, "downloads"), client);
        view = new(0, catalog.Snapshot, [], targetProfile, supported) { TargetLabel = targetLabel };
        worker = Task.Run(RunAsync);
    }

    public ModLibrarySnapshot View => Volatile.Read(ref view);

    public void RefreshCatalog() => Post(() =>
    {
        if (refresh is null || refresh.IsCompleted)
        {
            refresh = catalog.RefreshAsync(lifetime.Token);
        }
    });

    public void Queue(RemoteModEntry entry) => Post(() =>
    {
        if (!downloadsAvailable)
        {
            throw new InvalidOperationException("Downloads are unavailable. Check the library error and close other instances before restarting.");
        }
        if (!supported)
        {
            throw new NotSupportedException("Junction downloads cannot be installed for this edition or language.");
        }
        if (mods.InstalledPackages.Any(package => package.JunctionMetadata?.Id == entry.Id))
        {
            ModPackageInstallResult? status = installer.GetStatus(entry);
            if (status is null || !status.Installed)
            {
                throw new InvalidOperationException("This mod is already installed manually. View its installed settings; automatic replacement is unavailable.");
            }
        }
        downloads.Enqueue(entry, targetProfile);
        error = null;
    });

    public void Pause(Guid id) => Post(() => downloads.Pause(id));
    public void Resume(Guid id) => Post(() => downloads.Resume(id));
    public void OpenDownloadPage(Guid id) => Post(() =>
    {
        DownloadJobSnapshot? job = downloads.Snapshot.FirstOrDefault(candidate => candidate.JobId == id);
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
                    downloads.Cancel(id);
                }
            }));
            inspections.Remove(id);
        }
        if (state?.Plan is { } plan)
        {
            Observe(installer.DiscardAsync(plan, lifetime.Token));
            inspections.Remove(id);
        }
        downloads.Cancel(id);
    });

    public void Retry(Guid id) => Post(() =>
    {
        if (inspections.TryGetValue(id, out Inspection? state) && state.Error is not null)
        {
            inspections.Remove(id);
        }
        else
        {
            downloads.Retry(id);
        }
        error = null;
    });

    public void Install(Guid id) => Post(() =>
    {
        if (!supported)
        {
            throw new InvalidOperationException("Installation is unavailable for this target or while another game instance uses its mod directory.");
        }
        if (!inspections.TryGetValue(id, out Inspection? state) || state.Plan is null || state.Installation is not null || state.Result is not null)
        {
            return;
        }
        if (mods.InstalledPackages.Any(package => package.JunctionMetadata?.Id == state.Plan.Entry.Id && package.Id != state.Plan.PackageId))
        {
            throw new InvalidOperationException("A manual installation or another variant already owns this mod identity. No files were replaced.");
        }
        state.Installation = installer.RequestInstallAsync(state.Plan, mods.AutoEnableNewMods, lifetime.Token);
    });

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
            try
            {
                await downloads.InitializeAsync(lifetime.Token).ConfigureAwait(false);
                downloadsAvailable = true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                error = SafeError(exception);
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
                if (!ReferenceEquals(reconciledCatalog, catalog.Snapshot))
                {
                    reconciledCatalog = catalog.Snapshot;
                    List<ModLibraryInstallation> known = [];
                    foreach (RemoteModEntry entry in reconciledCatalog.Entries)
                    {
                        try
                        {
                            if (installer.GetStatus(entry) is { } status)
                            {
                                known.Add(new(entry.SourceId, entry.Id, status.Version, status.PackageId, status.RestartRequired));
                            }
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            error = SafeError(exception);
                        }
                    }
                    installations = known.AsReadOnly();
                }
                IReadOnlyList<DownloadJobSnapshot> jobs = downloads.Snapshot;
                foreach (DownloadJobSnapshot job in jobs.Where(job => job.Phase == ModDownloadPhase.ReadyToInstall))
                {
                    if (!inspections.TryGetValue(job.JobId, out Inspection? state))
                    {
                        state = new();
                        inspections.Add(job.JobId, state);
                        try
                        {
                            ModPackageInstallResult? installed = installer.GetStatus(job.Entry);
                            if (installed is not null && RemoteModEntry.CompareRelease(installed.Version, job.Entry.Version) == 0)
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
                    if (state.Installation is { IsCompleted: true })
                    {
                        try
                        {
                            state.Result = await state.Installation.ConfigureAwait(false);
                            state.Error = state.Result.Error;
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
            Publish(downloads.Snapshot);
        }
        finally
        {
            foreach (Inspection state in inspections.Values)
            {
                Observe(state.Preparation);
                Observe(state.Installation);
            }
            Observe(refresh);
            downloads.Dispose();
            catalog.Dispose();
        }
    }

    private void Publish(IReadOnlyList<DownloadJobSnapshot> jobs)
    {
        ModLibraryJob[] projected = jobs.Select(Project).ToArray();
        RemoteCatalogSnapshot current = catalog.Snapshot;
        ModLibrarySnapshot previous = View;
        string? message = error ?? (!initialized ? "Preparing saved downloads…" : null);
        if (!ReferenceEquals(previous.Catalog, current) || !previous.Jobs.SequenceEqual(projected) || previous.Error != message)
        {
            Volatile.Write(ref view, new(previous.Revision + 1, current, Array.AsReadOnly(projected), targetProfile, supported, message) { Installations = installations, TargetLabel = targetLabel });
        }
    }

    private ModLibraryJob Project(DownloadJobSnapshot job)
    {
        inspections.TryGetValue(job.JobId, out Inspection? state);
        string phase = job.Phase switch
        {
            ModDownloadPhase.ReadyToInstall => state?.Error is not null ? "Needs attention" : state?.Result?.Installed == true ? "Installed" : state?.Result?.RestartRequired == true ? "Restart required" : state?.Installation is not null ? "Preparing installation" : state?.Plan is not null ? "Ready to install" : "Inspecting package",
            ModDownloadPhase.Verifying => "Checking download",
            ModDownloadPhase.WaitingToRetry => "Waiting to retry",
            ModDownloadPhase.NeedsUserAction => "Provider action required",
            _ => job.Phase.ToString()
        };
        string? details = state?.Plan is { } plan ? $"Version {plan.Version} · {plan.PackageId}. Installation is applied before resource loading on the next launch. {(mods.AutoEnableNewMods ? "New installations will be enabled." : "New installations will remain disabled.")} Existing choices are preserved. Integrity: local SHA-256; no publisher checksum is supplied." : job.Details ?? job.Message;
        bool transferring = job.Phase is ModDownloadPhase.Queued or ModDownloadPhase.Resolving or ModDownloadPhase.Downloading or ModDownloadPhase.WaitingToRetry;
        string? failure = state?.Error ?? (job.Phase is ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction ? job.Message : null);
        return new(job.JobId, job.Entry, phase, job.CompletedBytes, job.TotalBytes, job.BytesPerSecond, failure, details,
            transferring,
            job.Phase == ModDownloadPhase.Paused,
            transferring || job.Phase is ModDownloadPhase.Verifying or ModDownloadPhase.Paused or ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction || job.Phase == ModDownloadPhase.ReadyToInstall && state?.Result is null && state?.Installation is null,
            state?.Plan is not null && state.Error is null && state.Result is null && state.Installation is null,
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
