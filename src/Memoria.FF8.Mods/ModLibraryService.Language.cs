using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods;

public sealed partial class ModLibraryService
{
    private OfficialCatalogSnapshot? languageIndex;
    private OfficialCatalogSnapshot? mergedLanguageIndex;
    private RemoteCatalogSnapshot? lastPublishedFullCatalog;
    private RemoteCatalogSnapshot? publishedOrdinaryCatalog;
    private IReadOnlyList<RemoteModEntry> publishedLanguageEntries = [];
    private readonly SemaphoreSlim languageAcquisition = new(1, 1);

    private sealed class LanguageInstall(RemoteModEntry entry, ModPackageInstallResult? status, ModDownloadCoordinator? queue = null, bool countsDownload = false)
    {
        public RemoteModEntry Entry { get; } = entry;
        public ModPackageInstallResult? Status { get; } = status;
        public ModDownloadCoordinator? Queue { get; } = queue;
        public ModPackageInstallPlan? Plan { get; set; }
        public bool CountsDownload { get; set; } = countsDownload;
        public long DownloadedBytes { get; set; }
        public long? TotalBytes { get; set; } = entry.Official?.Latest?.Artifact.Size;
    }

    private async Task RefreshLanguageIndexAsync(bool force)
    {
        Volatile.Write(ref languageIndex, await OfficialModCatalog.RefreshAsync(officialCacheDirectory!, client, force, lifetime.Token, languageIndex: true).ConfigureAwait(false));
    }

    private static bool IsLanguageJob(RemoteModEntry entry, RemoteCatalogSnapshot current)
    {
        return LanguagePackages.IsLanguage(entry) || current.Entries.Any(candidate => candidate.SourceId == entry.SourceId && candidate.Id == entry.Id && LanguagePackages.IsLanguage(candidate));
    }

    public async Task<string?> AcquireLanguageAsync(GameLanguageOffer offer, ModPackageInstaller destination, CancellationToken cancellationToken = default, bool update = false, IProgress<LanguageAcquisitionProgress>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!offer.CanSelect || offer.Profile is not { } profile)
        {
            throw new NotSupportedException(offer.Requirement ?? "The edition cannot be selected.");
        }
        if (offer.IsOriginal)
        {
            return null;
        }
        if (offer.Release is not { } selected)
        {
            return offer.InstalledPackage?.Id ?? throw new InvalidOperationException("The language package is unavailable.");
        }
        if (offer.InstalledPackage is not null && !offer.RequiresAcquisition && !update)
        {
            return offer.InstalledPackage.Id;
        }
        using CancellationTokenSource activity = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        CancellationToken token = activity.Token;
        await languageAcquisition.WaitAsync(token).ConfigureAwait(false);
        List<LanguageInstall> prepared = [];
        LanguageAcquisitionProgress? lastProgress = null;
        void Report(LanguageAcquisitionPhase phase, RemoteModEntry entry)
        {
            if (progress is null)
            {
                return;
            }
            token.ThrowIfCancellationRequested();
            long downloaded = 0;
            long? total = 0;
            foreach (LanguageInstall member in prepared)
            {
                if (member.CountsDownload)
                {
                    downloaded += member.DownloadedBytes;
                    total += member.TotalBytes;
                }
            }
            LanguageAcquisitionProgress current = new(phase, entry.Name, downloaded, total) { OfficialMemberId = entry.Official?.Id };
            if (current != lastProgress)
            {
                lastProgress = current;
                progress.Report(current);
            }
        }

        try
        {
            while (!Volatile.Read(ref initialized) || View.Revision == 0)
            {
                if (worker.IsCompleted)
                {
                    throw new InvalidOperationException(View.Error ?? "The mod library is unavailable.");
                }
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            RemoteModEntry release = View.LanguageEntries.FirstOrDefault(entry => SameLanguageRelease(entry, selected))
                ?? throw new InvalidOperationException("This language release has changed. Review the current edition in the language picker.");
            if (!destination.IsProfile(profile) || offer.Metadata is null || !offer.Metadata.NativeVariants.Any(variant => variant.Matches(profile)))
            {
                throw new NotSupportedException("The selected installer does not match the language's native base.");
            }
            foreach (OfficialModComponent dependency in release.RequiredLanguagePackages)
            {
                token.ThrowIfCancellationRequested();
                prepared.Add(PlanLanguageMember(dependency.ToEntry(), destination));
            }
            string? packageId = offer.InstalledPackage?.Id;
            if (packageId is null || update || offer.RequiresAcquisition && offer.Version is string version && release.CompareVersion(version) is > 0)
            {
                prepared.Add(PlanLanguageMember(release, destination));
            }
            foreach (LanguageInstall member in prepared)
            {
                token.ThrowIfCancellationRequested();
                if (member.Queue is not null)
                {
                    await PrepareLanguageMemberAsync(member, destination, Report, token).ConfigureAwait(false);
                }
            }
            token.ThrowIfCancellationRequested();
            if (!View.LanguageEntries.Any(entry => SameLanguageRelease(entry, release)))
            {
                throw new InvalidOperationException("The reviewed language or a dependency has changed. Review the current release before installing.");
            }
            foreach (LanguageInstall member in prepared)
            {
                bool alreadyApplied = member.Status is { Installed: true, RestartRequired: false } && member.Plan is null;
                if (!alreadyApplied)
                {
                    Report(LanguageAcquisitionPhase.Installing, member.Entry);
                }
                if (member.Plan is { } plan)
                {
                    await destination.RequestInstallAsync(plan, autoEnableNewMods: false, cancellationToken: token, repair: member.Status is { Installed: false, Error: not null } && member.Entry.Official is not null).ConfigureAwait(false);
                }
                string applied = alreadyApplied ? member.Status!.PackageId : ApplyLanguagePackage(destination, member.Entry, token);
                if (member.Entry.Id == release.Id && member.Entry.SourceId == release.SourceId)
                {
                    packageId = applied;
                }
            }
            return packageId ?? throw new IOException("The language provider did not install.");
        }
        finally
        {
            try
            {
                foreach (ModPackageInstallPlan plan in prepared.Select(member => member.Plan).OfType<ModPackageInstallPlan>())
                {
                    await destination.DiscardAsync(plan).ConfigureAwait(false);
                }
            }
            finally
            {
                languageAcquisition.Release();
            }
        }
    }

    private static bool SameLanguageRelease(RemoteModEntry candidate, RemoteModEntry reviewed)
    {
        return candidate.SourceId == reviewed.SourceId && candidate.Id == reviewed.Id && candidate.Version == reviewed.Version
            && candidate.Links.SequenceEqual(reviewed.Links) && candidate.Official?.Latest?.Artifact == reviewed.Official?.Latest?.Artifact
            && candidate.RequiredLanguagePackages.Select(component => (component.Id, component.Latest!.Version, component.Latest.Artifact))
                .SequenceEqual(reviewed.RequiredLanguagePackages.Select(component => (component.Id, component.Latest!.Version, component.Latest.Artifact)));
    }

    private LanguageInstall PlanLanguageMember(RemoteModEntry release, ModPackageInstaller destination)
    {
        if (destination.CompatibilityError(release) is string unavailable)
        {
            throw new NotSupportedException(unavailable);
        }
        bool native = release.SourceId == OfficialModCatalog.SourceId;
        (ModPackageInstaller owner, ModPackageInstallResult? installed) = ResolveInstallation(release);
        if (installed?.PendingRemoval == true)
        {
            throw new InvalidOperationException($"'{release.Name}' is waiting to be uninstalled.");
        }
        if (installed is { Installed: true } && release.CompareVersion(installed.Version) == 0)
        {
            if (native && installed.Official?.Latest?.Artifact != release.Official?.Latest?.Artifact)
            {
                throw new InvalidDataException($"Verified release metadata changed without a new version for '{release.Name}'.");
            }
            return new(release, installed);
        }
        if (installed is not null && !ApplicationInstallation.ModPathComparer.Equals(owner.ModsRoot, destination.ModsRoot))
        {
            throw new InvalidOperationException($"'{release.Name}' is managed in a different mod directory.");
        }
        if (mods.InstalledPackages.Any(package => (native ? package.Descriptor?.Id == release.Official?.Id : package.JunctionMetadata?.Id == release.Id) && package.Id != installed?.PackageId))
        {
            throw new InvalidOperationException($"'{release.Name}' is already installed manually. Automatic replacement is unavailable.");
        }
        if (installed is { RestartRequired: true, Installed: false })
        {
            if (release.CompareVersion(installed.Version) != 0)
            {
                throw new InvalidOperationException($"'{release.Name}' has a different version waiting to be applied.");
            }
            return new(release, installed);
        }
        if (native ? !officialDownloadsAvailable : !downloadsAvailable)
        {
            throw new InvalidOperationException("Language downloads are unavailable. Close other mod library instances and try again.");
        }
        ModDownloadCoordinator queue = native ? officialDownloads! : downloads;
        DownloadJobSnapshot? existing = queue.FindMatchingJob(release, DownloadTarget(release, destination));
        bool cached = existing is { Phase: ModDownloadPhase.ReadyToInstall, ArchivePath: not null }
            || existing is { ArchivePath: string archive, Sha256: not null } && File.Exists(archive);
        return new(release, installed, queue, countsDownload: !cached);
    }

    private static string DownloadTarget(RemoteModEntry release, ModPackageInstaller destination)
    {
        return release.SourceId == OfficialModCatalog.SourceId ? OfficialModCatalog.SourceId : destination.TargetProfile;
    }

    private static async Task PrepareLanguageMemberAsync(LanguageInstall member, ModPackageInstaller destination, Action<LanguageAcquisitionPhase, RemoteModEntry> report, CancellationToken token)
    {
        ModDownloadCoordinator queue = member.Queue!;
        report(LanguageAcquisitionPhase.Preparing, member.Entry);
        token.ThrowIfCancellationRequested();
        Guid id = queue.Enqueue(member.Entry, DownloadTarget(member.Entry, destination));
        bool alreadyReady = queue.GetCompletedPath(id) is not null;
        queue.Resume(id);
        try
        {
            string archive = await WaitForLanguageDownloadAsync(queue, id, member, alreadyReady, report, token).ConfigureAwait(false);
            member.Plan = await destination.InspectAsync(archive, member.Entry, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (queue.GetCompletedPath(id) is null)
            {
                queue.Pause(id);
            }
            throw;
        }
    }

    private string ApplyLanguagePackage(ModPackageInstaller destination, RemoteModEntry entry, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ModPackageInstallResult? result = destination.ApplyLanguageInstallation(entry);
        if (result is not { Installed: true })
        {
            throw new IOException(result?.Error ?? $"The language package '{entry.Name}' could not be applied. Retry the installation.");
        }
        Post(() => reconciledCatalog = null);
        return result.PackageId;
    }

    private static async Task<string> WaitForLanguageDownloadAsync(ModDownloadCoordinator queue, Guid id, LanguageInstall member, bool alreadyReady, Action<LanguageAcquisitionPhase, RemoteModEntry> report, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            DownloadJobSnapshot job = queue.GetJobSnapshot(id);
            switch (job.Phase)
            {
                case ModDownloadPhase.Downloading:
                    member.CountsDownload = true;
                    member.DownloadedBytes = job.CompletedBytes;
                    member.TotalBytes = job.TotalBytes;
                    report(LanguageAcquisitionPhase.Downloading, member.Entry);
                    break;
                case ModDownloadPhase.Verifying or ModDownloadPhase.ReadyToInstall:
                    member.CountsDownload = !job.ReusedArchive && (!alreadyReady || member.CountsDownload);
                    member.DownloadedBytes = job.CompletedBytes;
                    member.TotalBytes = job.CompletedBytes;
                    report(LanguageAcquisitionPhase.Verifying, member.Entry);
                    if (job.Phase == ModDownloadPhase.ReadyToInstall && job.ArchivePath is not null)
                    {
                        return job.ArchivePath;
                    }
                    break;
                case ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction or ModDownloadPhase.Cancelled:
                    throw new IOException(job.Message ?? "The language download could not be completed.");
                default:
                    report(LanguageAcquisitionPhase.Preparing, member.Entry);
                    break;
            }
            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }
}
