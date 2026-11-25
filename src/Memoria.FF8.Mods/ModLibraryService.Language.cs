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
    private sealed record LanguageInstall(RemoteModEntry Entry, ModPackageInstallResult? Status, ModPackageInstallPlan? Plan);

    private async Task RefreshLanguageIndexAsync(bool force)
    {
        Volatile.Write(ref languageIndex, await OfficialModCatalog.RefreshAsync(officialCacheDirectory!, client, force, lifetime.Token, languageIndex: true).ConfigureAwait(false));
    }

    private static bool IsLanguageJob(RemoteModEntry entry, RemoteCatalogSnapshot current)
    {
        return LanguagePackages.IsLanguage(entry) || current.Entries.Any(candidate => candidate.SourceId == entry.SourceId && candidate.Id == entry.Id && LanguagePackages.IsLanguage(candidate));
    }

    public async Task<string?> AcquireLanguageAsync(GameLanguageOffer offer, ModPackageInstaller destination, CancellationToken cancellationToken = default, bool update = false)
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
                prepared.Add(await PrepareLanguageMemberAsync(dependency.ToEntry(), destination, token).ConfigureAwait(false));
            }
            string? packageId = offer.InstalledPackage?.Id;
            if (packageId is null || update || offer.RequiresAcquisition && offer.Version is string version && release.CompareVersion(version) is > 0)
            {
                prepared.Add(await PrepareLanguageMemberAsync(release, destination, token).ConfigureAwait(false));
            }
            token.ThrowIfCancellationRequested();
            if (!View.LanguageEntries.Any(entry => SameLanguageRelease(entry, release)))
            {
                throw new InvalidOperationException("The reviewed language or a dependency has changed. Review the current release before installing.");
            }
            foreach (LanguageInstall member in prepared)
            {
                if (member.Plan is { } plan)
                {
                    await destination.RequestInstallAsync(plan, autoEnableNewMods: false, cancellationToken: token, repair: member.Status is { Installed: false, Error: not null } && member.Entry.Official is not null).ConfigureAwait(false);
                }
                string applied = member.Status is { Installed: true, RestartRequired: false } && member.Plan is null
                    ? member.Status.PackageId : ApplyLanguagePackage(destination, member.Entry, token);
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

    private async Task<LanguageInstall> PrepareLanguageMemberAsync(RemoteModEntry release, ModPackageInstaller destination, CancellationToken token)
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
            return new(release, installed, null);
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
            return new(release, installed, null);
        }
        if (native ? !officialDownloadsAvailable : !downloadsAvailable)
        {
            throw new InvalidOperationException("Language downloads are unavailable. Close other mod library instances and try again.");
        }
        ModDownloadCoordinator queue = native ? officialDownloads! : downloads;
        Guid id = queue.Enqueue(release, native ? OfficialModCatalog.SourceId : destination.TargetProfile);
        queue.Resume(id);
        try
        {
            string archive = await WaitForLanguageDownloadAsync(queue, id, token).ConfigureAwait(false);
            ModPackageInstallPlan plan = await destination.InspectAsync(archive, release, token).ConfigureAwait(false);
            return new(release, installed, plan);
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
            throw new IOException(result?.Error ?? "The language package is staged. Close any game using it and retry the installation.");
        }
        Post(() => reconciledCatalog = null);
        return result.PackageId;
    }

    private static async Task<string> WaitForLanguageDownloadAsync(ModDownloadCoordinator queue, Guid id, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            DownloadJobSnapshot job = queue.Snapshot.Single(item => item.JobId == id);
            switch (job.Phase)
            {
                case ModDownloadPhase.ReadyToInstall when job.ArchivePath is not null:
                    return job.ArchivePath;
                case ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction or ModDownloadPhase.Cancelled:
                    throw new IOException(job.Message ?? "The language download could not be completed.");
            }
            await Task.Delay(200, token).ConfigureAwait(false);
        }
    }
}
