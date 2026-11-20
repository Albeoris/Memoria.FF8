using System.Collections.Immutable;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods;

/// <summary>Owns mod discovery and publication. File watcher callbacks only request a later transaction.</summary>
public sealed partial class ModRuntime : IModService, IDisposable
{
    private static readonly IComparer<string> PathOrder = Comparer<string>.Create((left, right) =>
    {
        int insensitive = StringComparer.OrdinalIgnoreCase.Compare(left, right);
        return insensitive != 0 ? insensitive : StringComparer.Ordinal.Compare(left, right);
    });

    private readonly object gate = new();
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private readonly string modsRoot;
    private readonly string preferenceRoot;
    private readonly string legacyModsRoot;
    private readonly string bundledModsRoot;
    private readonly Func<ModFormat, bool> isCompatible;
    private readonly Func<string, bool>? junctionPackageAllowed;
    private readonly Func<string, string?>? nativeRootUnavailable;
    private readonly string edition;
    private readonly string language;
    private readonly Func<string, bool>? obsoleteBundledPackage;
    private readonly Func<ModCatalogSnapshot, ModPreparationResult> prepare;
    private readonly TimeSpan debounce;
    private readonly List<Action<ModCatalogSnapshot>> subscribers = [];
    private readonly Dictionary<string, FileEntry> fileEntries = new(StringComparer.Ordinal);
    private Dictionary<string, FileEntry> committedFileEntries = new(StringComparer.Ordinal);
    private readonly List<(string ModsDirectory, FileSystemWatcher Watcher)> watchers = [];
    private readonly Queue<string> watcherErrors = [];
    private ModCatalogSnapshot catalog = new(0, [], []);
    private IReadOnlyList<ModPackage> installedPackages = Array.Empty<ModPackage>();
    private ModUpdateOutcome updateOutcome = ModUpdateOutcome.Updating;
    private readonly HashSet<string> changedPackageIds = new(StringComparer.OrdinalIgnoreCase);
    private ModCatalogSnapshot? pending;
    private ModPreparationResult? pendingPreparation;
    private FieldLightingMods? pendingLighting;
    private FieldLightingMods lighting = FieldLightingMods.Empty;
    private (ModCatalogSnapshot Candidate, bool AutoEnable, string Identity)? deferredDiscovery;
    private ModPreparationResult? deferredPreparation;
    private IReadOnlyList<ModDiagnostic> diagnostics = Array.Empty<ModDiagnostic>();
    private DateTime requestedAt = DateTime.MinValue;
    private DateTime firstRequestedAt = DateTime.MinValue;
    private DateTime retryDue = DateTime.MinValue;
    private int retryAttempt;
    private bool forceRevalidate;
    private bool reloadRequested = true;
    private bool autoEnable = true;
    private bool disposed;
    private long fileRevision;
    private long localizationChangeEpoch;
    private string? lastIdentity;
    private string? pendingIdentity;

    public ModRuntime(string? applicationBaseDirectory = null, Func<ModFormat, bool>? isCompatible = null, Func<ModCatalogSnapshot, ModPreparationResult>? prepare = null, bool watch = true, TimeSpan? debounce = null, IEnumerable<string>? additionalBaseDirectories = null, string? modsDirectory = null, Func<string, bool>? junctionPackageAllowed = null, Func<string, string?>? nativeRootUnavailable = null, string edition = "Classic", string language = "en", Func<string, bool>? obsoleteBundledPackage = null, ModDirectories? modLocations = null)
    {
        modsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modLocations?.Primary ?? modsDirectory ?? DefaultModsDirectory(applicationBaseDirectory ?? AppContext.BaseDirectory)));
        preferenceRoot = modLocations?.Preferences ?? modsRoot;
        legacyModsRoot = modLocations?.LegacyDefault ?? modsRoot;
        if (modLocations is not null)
        {
            ImportLocationPreferences(modLocations);
        }
        IEnumerable<string> roots = (additionalBaseDirectories ?? []).Select(directory => Path.Combine(directory, "Mods")).Concat(modLocations?.Discovery ?? []).Append(modsRoot);
        bundledModsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(applicationBaseDirectory ?? AppContext.BaseDirectory, "BundledMods")));
        if (Directory.Exists(bundledModsRoot))
        {
            roots = roots.Append(bundledModsRoot);
        }
        ModsDirectories = Array.AsReadOnly(roots.Select(directory => Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))).Distinct(ApplicationInstallation.ModPathComparer).ToArray());
        ReadLocalizationPreferences();
        this.isCompatible = isCompatible ?? (_ => true);
        this.junctionPackageAllowed = junctionPackageAllowed;
        this.nativeRootUnavailable = nativeRootUnavailable;
        this.edition = edition;
        this.language = language;
        this.obsoleteBundledPackage = obsoleteBundledPackage;
        this.prepare = prepare ?? (_ => ModPreparationResult.Accepted);
        this.debounce = debounce ?? TimeSpan.FromMilliseconds(250);
        if (watch)
        {
            foreach (string directory in ModsDirectories.Append(preferenceRoot).Distinct(ApplicationInstallation.ModPathComparer))
            {
                FileSystemWatcher watcher = new(ExistingWatchDirectory(directory))
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Deleted += OnFileChanged;
                watcher.Renamed += OnFileChanged;
                watcher.Error += OnWatcherError;
                watchers.Add((directory, watcher));
            }
            ConfigureWatcher();
        }
    }

    private static string DefaultModsDirectory(string applicationBaseDirectory)
    {
        string applicationDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(applicationBaseDirectory, ".")));
        string? packageDirectory = Path.GetDirectoryName(applicationDirectory);
        string? contentsDirectory = packageDirectory is null ? null : Path.GetDirectoryName(packageDirectory);
        string? bundleDirectory = contentsDirectory is null ? null : Path.GetDirectoryName(contentsDirectory);
        if (HostPlatforms.Current == HostPlatform.MacOS && Path.GetFileName(applicationDirectory) == "Data" && Path.GetFileName(packageDirectory) is ("MacOS" or "Resources") && Path.GetFileName(contentsDirectory) == "Contents" && bundleDirectory?.EndsWith(".app", StringComparison.Ordinal) == true)
        {
            return ApplicationInstallation.ResolveModsDirectory(packageDirectory ?? applicationDirectory, HostPlatform.MacOS, ApplicationPaths.Current.Configuration);
        }
        string launcher = HostPlatforms.Current.ExecutableName("Memoria.Launcher");
        if (Path.GetFileName(applicationDirectory).Equals("Data", StringComparison.OrdinalIgnoreCase) && packageDirectory is not null && File.Exists(Path.Combine(packageDirectory, launcher)) && (Directory.Exists(Path.Combine(packageDirectory, "Mods")) || File.Exists(Path.Combine(applicationDirectory, "distribution.json"))))
        {
            return Path.Combine(packageDirectory, "Mods");
        }
        return Path.Combine(applicationDirectory, "Mods");
    }

    public IReadOnlyList<string> ModsDirectories { get; }
    public string PrimaryModsDirectory => modsRoot;
    public string PreferencesDirectory => preferenceRoot;
    public string LegacyModsDirectory => legacyModsRoot;

    public static void ImportLocationPreferences(ModDirectories locations)
    {
        if (ApplicationInstallation.ModPathComparer.Equals(locations.Preferences, locations.LegacyDefault))
        {
            return;
        }
        foreach (string relative in new[] { "mods.ini", "localization-stack.json", Path.Combine("Memoria", MemoriaSettingsFile) })
        {
            string previous = Path.Combine(locations.LegacyDefault, relative);
            string destination = Path.Combine(locations.Preferences, relative);
            if (File.Exists(destination) || !File.Exists(previous))
            {
                continue;
            }
            ModPackageInstaller.EnsureNoLinks(previous);
            ModPackageInstaller.EnsureNoLinks(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(previous, temporary);
                File.Move(temporary, destination);
            }
            catch (IOException) when (File.Exists(destination))
            {
                // Another process imported these settings or wrote a newer preference.
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }

    public FieldLightingMods Lighting
    {
        get
        {
            lock (gate)
            {
                return lighting;
            }
        }
    }

    public IReadOnlyList<ModPackage> InstalledPackages
    {
        get
        {
            lock (gate)
            {
                return installedPackages;
            }
        }
    }

    public ModServiceView View
    {
        get
        {
            lock (gate)
            {
                ModServiceView view = ModServiceView.Create(catalog, installedPackages, autoEnable, reloadRequested || pending is not null, diagnostics, updateOutcome, changedPackageIds);
                view = view with { Packages = Array.AsReadOnly(view.Packages.Select(package => RequiredPackageReason(package.Package.Id) is not null ? package with { CanToggle = false } : package).ToArray()) };
                return GroupMemoriaComponents(view);
            }
        }
    }

    /// <summary>Returns discovered files for inspection, including disabled packages, without activating them.</summary>
    public IReadOnlyList<ModFile> ReadInstalledFiles(ModFormat format)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return fileEntries.Values.Select(entry => entry.File).Where(file => file.Format == format).OrderBy(file => file.PackageId, StringComparer.OrdinalIgnoreCase).ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public ModCatalogSnapshot Catalog
    {
        get
        {
            lock (gate)
            {
                return catalog;
            }
        }
    }

    public bool AutoEnableNewMods
    {
        get
        {
            lock (gate)
            {
                return autoEnable;
            }
        }
    }

    public bool IsReloadPending
    {
        get
        {
            lock (gate)
            {
                return reloadRequested || pending is not null;
            }
        }
    }

    public IReadOnlyList<ModDiagnostic> Diagnostics
    {
        get
        {
            lock (gate)
            {
                return diagnostics;
            }
        }
    }

    public ModOperationResult SetEnabled(string packageId, bool enabled)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            ModPackage? package = installedPackages.FirstOrDefault(item => item.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (package is null)
            {
                return new(false, $"Unknown mod package: {packageId}.");
            }
            if (GetPackageLockReason(packageId) is string lockReason)
            {
                return new(false, lockReason);
            }
            if (!isCompatible(package.Format) && !package.LocalizationOnly && !package.TextureContentOnly || enabled && package.Status == ModEffectiveStatus.IncompatibleEdition)
            {
                return new(false, $"{package.Name} is incompatible with the selected game-data edition.");
            }
            if (enabled && package.HasLocalization && !installedPackages.Any(other => other.Id != package.Id && other.HasLocalization && other.RequestedEnabled))
            {
                return SetLocalizationSelection([package.Id], LocalizationPrimaryTarget);
            }
            try
            {
                if (IsMemoriaComponent(package) && (package.Descriptor is not null || MemoriaComponentPreference(ReadMemoriaSettings(), package.Id, package.Official, package.Descriptor) is not null))
                {
                    string component = MemoriaComponentSettingsKey(package.Id, package.Official, package.Descriptor)!;
                    return SaveMemoriaSettings(ComponentGroupId(ComponentGroup(package)), [(ComponentBehavior, component, enabled ? 1 : 0)]);
                }
                ValidatePreferenceTarget(package);
                string preferencePath = PreferencePath(package);
                ValidatePreferencePath(preferencePath);
                ModConfiguration.WriteBoolean(preferencePath, "enabled", enabled);
                installedPackages = Array.AsReadOnly(installedPackages.Select(item => item.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase) ? item with { ExplicitEnabled = enabled, RequestedEnabled = enabled } : item).ToArray());
                RequestRefreshCore(DateTime.MinValue);
                return ModOperationResult.Completed;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                return OperationFailed(exception, packageId);
            }
        }
    }

    public ModOperationResult SetAutoEnableNewMods(bool enabled)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            try
            {
                ModConfiguration.WriteBoolean(Path.Combine(preferenceRoot, "mods.ini"), "auto_enable_new_mods", enabled);
                autoEnable = enabled;
                ReconcilePreferences();
                RequestRefreshCore(DateTime.MinValue);
                return ModOperationResult.Completed;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                List<ModDiagnostic> errors = [new("configuration-write-failed", exception.Message)];
                updateOutcome = ModUpdateOutcome.Failed;
                ReconcilePreferences(errors);
                diagnostics = Array.AsReadOnly(errors.ToArray());
                return new(false, exception.Message);
            }
        }
    }

    public IDisposable Subscribe(Action<ModCatalogSnapshot> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        lock (gate)
        {
            ThrowIfDisposed();
            subscribers.Add(changed);
            return new Subscription(this, changed);
        }
    }

    public void RequestRefresh()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            RequestRefreshCore(DateTime.MinValue);
        }
    }

    public void Refresh()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            forceRevalidate = true;
            RequestRefreshCore(DateTime.MinValue);
        }
    }

    public void ReportFailure(ModDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (Environment.CurrentManagedThreadId != ownerThread)
        {
            throw new InvalidOperationException("Mod failures must be reported on the runtime's owning thread.");
        }
        lock (gate)
        {
            ThrowIfDisposed();
            diagnostics = Array.AsReadOnly(diagnostics.Append(diagnostic).ToArray());
            updateOutcome = ModUpdateOutcome.Failed;
            ScheduleRetry();
        }
    }

    /// <summary>Records a rejected resource without rejecting its package or scheduling a catalog retry.</summary>
    public void ReportResourceRejection(ModDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        lock (gate)
        {
            ThrowIfDisposed();
            if (diagnostics.Any(existing => existing.Code == diagnostic.Code && string.Equals(existing.PackageId, diagnostic.PackageId, StringComparison.OrdinalIgnoreCase) && string.Equals(existing.RelativePath, diagnostic.RelativePath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            diagnostics = Array.AsReadOnly(diagnostics.Append(diagnostic).ToArray());
        }
    }

    /// <summary>Reconciles every file revision, including unchanged timestamps, after a lost watcher event batch.</summary>
    public void NotifyWatcherOverflow()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            forceRevalidate = true;
            RequestRefreshCore(DateTime.MinValue);
        }
    }

    /// <summary>May run on a worker thread. Does not publish catalog, resolver, resource or GPU state.</summary>
    public bool PreparePending(bool force = false)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (explicitTextureImport is not null)
            {
                return false;
            }
            DateTime now = DateTime.UtcNow;
            if (!reloadRequested || (!force && (now < retryDue || now - requestedAt < debounce && now - firstRequestedAt < TimeSpan.FromSeconds(2))))
            {
                return false;
            }
            reloadRequested = false;
            if (deferredDiscovery is null)
            {
                deferredPreparation?.Discard?.Invoke();
                deferredPreparation = null;
            }
            pendingPreparation?.Discard?.Invoke();
            pending = null;
            pendingPreparation = null;
            pendingLighting = null;
            try
            {
                ModCatalogSnapshot candidate;
                bool nextAutoEnable;
                string identity;
                if (deferredDiscovery is { } deferred)
                {
                    candidate = deferred.Candidate;
                    nextAutoEnable = deferred.AutoEnable;
                    identity = deferred.Identity;
                }
                else
                {
                    ConfigureWatcher();
                    candidate = Discover(out nextAutoEnable, out identity);
                    changedPackageIds.Clear();
                    foreach (string id in candidate.Packages.Select(package => package.Id).Concat(catalog.Packages.Select(package => package.Id)).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var before = committedFileEntries.Where(entry => entry.Value.File.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (entry.Key, entry.Value.Stamp, entry.Value.LocalizationEpoch, entry.Value.TextureLayoutIdentity));
                        var after = fileEntries.Where(entry => entry.Value.File.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (entry.Key, entry.Value.Stamp, entry.Value.LocalizationEpoch, entry.Value.TextureLayoutIdentity));
                        if (!before.SequenceEqual(after) || candidate.Packages.SingleOrDefault(package => package.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Priority != catalog.Packages.SingleOrDefault(package => package.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Priority)
                        {
                            changedPackageIds.Add(id);
                        }
                    }
                    installedPackages = Array.AsReadOnly(candidate.Packages.Select(package => package.Status == ModEffectiveStatus.Active ? package with { Status = ModEffectiveStatus.ReloadFailure } : package).ToArray());
                }
                deferredDiscovery = null;
                autoEnable = nextAutoEnable;
                if (identity == lastIdentity)
                {
                    installedPackages = catalog.Packages;
                    diagnostics = Array.AsReadOnly(candidate.Diagnostics.Concat(diagnostics.Where(diagnostic => diagnostic.Code is "subscriber-failed" or "resource-rejected")).Concat(catalog.Diagnostics.Where(diagnostic => diagnostic.Code is "Junction.FieldGroupConflict" or "Junction.DynamicFieldTextReference" or "Junction.MissingFieldColors")).Distinct().ToArray());
                    updateOutcome = ModUpdateOutcome.Idle;
                    changedPackageIds.Clear();
                    return false;
                }
                if (!StatusMods.TryPrepare(candidate, out StatusMods statuses, out IReadOnlyList<ModDiagnostic> statusErrors))
                {
                    diagnostics = Array.AsReadOnly(candidate.Diagnostics.Concat(statusErrors).Append(new ModDiagnostic("reload-failed", "Mod preparation failed; the last committed generation remains active.")).ToArray());
                    updateOutcome = ModUpdateOutcome.Failed;
                    ScheduleRetry();
                    return false;
                }
                if (!GuardianForceRules.TryPrepare(candidate, out GuardianForceRules guardianForceRules, out IReadOnlyList<ModDiagnostic> guardianForceErrors))
                {
                    diagnostics = Array.AsReadOnly(candidate.Diagnostics.Concat(guardianForceErrors).Append(new ModDiagnostic("reload-failed", "GF rule preparation failed; the last committed generation remains active.")).ToArray());
                    updateOutcome = ModUpdateOutcome.Failed;
                    ScheduleRetry();
                    return false;
                }
                if (!FieldLightingMods.TryPrepare(candidate, out FieldLightingMods candidateLighting, out IReadOnlyList<ModDiagnostic> lightingDiagnostics))
                {
                    diagnostics = Array.AsReadOnly(candidate.Diagnostics.Concat(lightingDiagnostics).Append(new ModDiagnostic("reload-failed", "Lighting preparation failed; the last committed generation remains active.")).ToArray());
                    updateOutcome = ModUpdateOutcome.Failed;
                    ScheduleRetry();
                    return false;
                }
                candidate.PreparedStatusMods = statuses;
                candidate.PreparedGuardianForceRules = guardianForceRules;
                ModPreparationResult result = prepare(candidate);
                diagnostics = Array.AsReadOnly(candidate.Diagnostics.Concat(result.Diagnostics ?? []).ToArray());
                if (result.Deferred)
                {
                    deferredPreparation = result;
                    deferredDiscovery = (candidate, nextAutoEnable, identity);
                    reloadRequested = true;
                    requestedAt = DateTime.MinValue;
                    retryDue = now + TimeSpan.FromMilliseconds(25);
                    updateOutcome = ModUpdateOutcome.Updating;
                    return false;
                }
                deferredPreparation = null;
                if (!result.Success)
                {
                    result.Discard?.Invoke();
                    diagnostics = Array.AsReadOnly(diagnostics.Append(new ModDiagnostic("reload-failed", "Mod preparation failed; the last committed generation remains active.")).ToArray());
                    updateOutcome = ModUpdateOutcome.Failed;
                    ScheduleRetry();
                    return false;
                }
                IEnumerable<ModPackage> preparedPackages = candidate.Packages;
                if (result.UsablePackageIds is not null)
                {
                    HashSet<string> usable = new(result.UsablePackageIds.Concat(candidateLighting.PackageIds).Concat(guardianForceRules.PackageIds), StringComparer.OrdinalIgnoreCase);
                    preparedPackages = preparedPackages.Select(package => package.Status == ModEffectiveStatus.Active && !usable.Contains(package.Id) ? package with { Status = ModEffectiveStatus.NoUsableEntries } : package);
                }
                candidate = new(candidate.Generation, preparedPackages, candidate.Files, diagnostics)
                {
                    Hardcore = candidate.Hardcore,
                    StatusPreferences = candidate.StatusPreferences,
                    PreparedStatusMods = statuses,
                    PreparedGuardianForceRules = guardianForceRules
                };
                autoEnable = nextAutoEnable;
                installedPackages = candidate.Packages;
                updateOutcome = ModUpdateOutcome.Updating;
                pending = candidate;
                pendingPreparation = result;
                pendingLighting = candidateLighting;
                pendingIdentity = identity;
                return true;
            }
            catch (Exception exception) when (IsPreparationError(exception))
            {
                ModTextureLoadException? textureFailure = exception as ModTextureLoadException;
                PackagePriorityException? priorityFailure = exception as PackagePriorityException;
                PackageTextureLayoutException? layoutFailure = exception as PackageTextureLayoutException;
                PackageDescriptorException? descriptorFailure = exception as PackageDescriptorException;
                List<ModDiagnostic> errors = [new("reload-failed", $"{exception.Message} The last committed generation remains active.", textureFailure?.PackageId ?? priorityFailure?.PackageId ?? layoutFailure?.PackageId ?? descriptorFailure?.PackageId, textureFailure?.RelativePath ?? priorityFailure?.RelativePath ?? layoutFailure?.RelativePath ?? descriptorFailure?.RelativePath)];
                if (priorityFailure is not null)
                {
                    errors.Add(new("invalid-package-priority", priorityFailure.Message, priorityFailure.PackageId, priorityFailure.RelativePath));
                }
                if (layoutFailure is not null)
                {
                    errors.Add(new("invalid-texture-layouts", layoutFailure.Message, layoutFailure.PackageId, layoutFailure.RelativePath));
                }
                if (descriptorFailure is not null)
                {
                    errors.Add(new("native-descriptor", descriptorFailure.Message, descriptorFailure.PackageId, descriptorFailure.RelativePath));
                }
                diagnostics = Array.AsReadOnly(errors.ToArray());
                updateOutcome = ModUpdateOutcome.Failed;
                ScheduleRetry();
                return false;
            }
        }
    }

    /// <summary>Publishes only on the thread which constructed this runtime, after all preparation succeeded.</summary>
    public bool CommitPending()
    {
        if (Environment.CurrentManagedThreadId != ownerThread)
        {
            throw new InvalidOperationException("Mod generations must be committed on the runtime's owning thread.");
        }
        Action<ModCatalogSnapshot>[] listeners;
        Action? afterCommit;
        ModCatalogSnapshot committed;
        lock (gate)
        {
            ThrowIfDisposed();
            if (explicitTextureImport is not null || pending is null || reloadRequested)
            {
                return false;
            }
            try
            {
                pendingPreparation?.Commit?.Invoke();
            }
            catch (Exception exception) when (IsPreparationError(exception))
            {
                diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("reload-failed", exception.Message) });
                pendingPreparation?.Discard?.Invoke();
                pending = null;
                pendingPreparation = null;
                updateOutcome = ModUpdateOutcome.Failed;
                ScheduleRetry();
                return false;
            }
            catalog = pending;
            lighting = pendingLighting ?? FieldLightingMods.Empty;
            pendingLighting = null;
            committedFileEntries = new(fileEntries, StringComparer.Ordinal);
            updateOutcome = ModUpdateOutcome.Idle;
            changedPackageIds.Clear();
            installedPackages = catalog.Packages;
            memoriaSettingsViews.Clear();
            committed = catalog;
            afterCommit = pendingPreparation?.AfterCommit;
            pending = null;
            pendingPreparation = null;
            lastIdentity = pendingIdentity;
            retryAttempt = 0;
            retryDue = DateTime.MinValue;
            listeners = subscribers.ToArray();
        }
        try
        {
            afterCommit?.Invoke();
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                diagnostics = Array.AsReadOnly(diagnostics.Append(new ModDiagnostic("subscriber-failed", exception.Message)).ToArray());
            }
        }
        foreach (Action<ModCatalogSnapshot> listener in listeners)
        {
            try
            {
                listener(committed);
            }
            catch (Exception exception)
            {
                lock (gate)
                {
                    diagnostics = Array.AsReadOnly(diagnostics.Append(new ModDiagnostic("subscriber-failed", exception.Message)).ToArray());
                }
            }
        }
        return true;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            deferredPreparation?.Discard?.Invoke();
            deferredPreparation = null;
            explicitTextureImport?.Preparation?.Discard?.Invoke();
            explicitTextureImport = null;
            foreach ((_, FileSystemWatcher watcher) in watchers)
            {
                watcher.Dispose();
            }
            subscribers.Clear();
            pendingPreparation?.Discard?.Invoke();
            pending = null;
            pendingPreparation = null;
        }
    }

    private ModCatalogSnapshot Discover(out bool nextAutoEnable, out string identity)
    {
        memoriaSettingsViews.Clear();
        nextAutoEnable = ModConfiguration.ReadBoolean(Path.Combine(preferenceRoot, "mods.ini"), "auto_enable_new_mods") ?? true;
        MemoriaSettings memoriaSettings = ImportStatusPreferences();
        List<ModPackage> packages = [];
        List<ModFile> files = [];
        List<ModDiagnostic> notices = [];
        Dictionary<string, ModPackage> describedPackages = catalog.Packages.Where(package => package.Descriptor is not null).ToDictionary(package => package.RootDirectory, StringComparer.Ordinal);
        StringComparison pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        while (watcherErrors.TryDequeue(out string? error))
        {
            notices.Add(new("watcher-error", error));
        }
        List<string> identities = [nextAutoEnable.ToString(), hardcore.ToString(), forceFf7EnemyIntegration.ToString(), string.Join(";", localizationOrder ?? []), LocalizationPrimaryTarget ?? ""];
        identities.AddRange(memoriaSettings.StatusSettings.OrderBy(setting => setting.Key, StringComparer.Ordinal).Select(setting => $"status-setting\0{setting.Key}\0{setting.Value}"));
        HashSet<string> visitedFiles = new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyDictionary<string, ModPackageInstallResult>> managed = new(ApplicationInstallation.ModPathComparer);
        foreach (string modRoot in ModsDirectories.Where(directory => Path.GetDirectoryName(directory) is not null && directory != bundledModsRoot))
        {
            try
            {
                managed.Add(modRoot, ModPackageInstaller.ReadOfficialInventory(modRoot));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                notices.Add(new("native-receipt", exception.Message));
            }
        }
        foreach (ModFormat format in Enum.GetValues<ModFormat>())
        {
            if (format == ModFormat.Junction)
            {
                DiscoverJunction(nextAutoEnable, packages, files, notices, identities, visitedFiles);
                continue;
            }
            HashSet<string> packageIds = new(StringComparer.OrdinalIgnoreCase);
            string[] roots = ModsDirectories.Select(directory => Path.Combine(directory, format.ToString())).Where(Directory.Exists).SelectMany(directory => Directory.GetDirectories(directory).OrderBy(path => Path.GetFileName(path), PathOrder)).OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToArray();
            Dictionary<string, string> bundledPackages = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> bundledDescriptors = new(StringComparer.Ordinal);
            if (format == ModFormat.Memoria)
            {
                foreach (string root in roots)
                {
                    if (obsoleteBundledPackage?.Invoke(root) == true || !string.Equals(Path.GetDirectoryName(root), Path.Combine(bundledModsRoot, format.ToString()), StringComparison.OrdinalIgnoreCase) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                    if (Directory.EnumerateFileSystemEntries(root).Any(path => Path.GetFileName(path).Equals(NativeModDescriptor.FileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        bundledDescriptors.Add(root);
                    }
                    else
                    {
                        bundledPackages.TryAdd(Path.GetFileName(root), root);
                    }
                }
                // Installed descriptors own their stable IDs before the bundled fallback is considered.
                roots = roots.OrderBy(bundledDescriptors.Contains).ToArray();
            }
            foreach (string root in roots)
            {
                string name = Path.GetFileName(root);
                string id = $"{format}/{name}";
                string modRoot = Path.GetDirectoryName(Path.GetDirectoryName(root))!;
                if (format == ModFormat.Memoria && (modRoot == bundledModsRoot && obsoleteBundledPackage?.Invoke(root) == true || IsLegacyStatusPreferenceDirectory(root)))
                {
                    continue;
                }
                ModPackageInstallResult? native = format == ModFormat.Memoria
                    ? managed.GetValueOrDefault(modRoot)?.Values.FirstOrDefault(entry => string.Equals(entry.NativeDestinationName, name, pathComparison))
                    : null;
                if (native is not null && nativeRootUnavailable?.Invoke(modRoot) is { } leaseError)
                {
                    notices.Add(new("native-resource-lease", leaseError, native.PackageId));
                    continue;
                }
                bundledPackages.TryGetValue(name, out string? bundledRoot);
                if (native?.Official is { } installedOfficial && OfficialModCatalog.HasDescriptorIdentity(installedOfficial))
                {
                    id = installedOfficial.RuntimePackageId;
                    bundledRoot = null;
                }
                if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    notices.Add(new("package-link-rejected", "Package roots must be unpacked directories, not links.", id));
                    continue;
                }
                NativeModDescriptor? descriptor = null;
                string? descriptorFailure = null;
                ModPackage? describedPackage = describedPackages.GetValueOrDefault(root);
                NativeModDescriptor? committedDescriptor = describedPackage?.Descriptor;
                if (native is { Installed: true, Official: { } restored } && describedPackage?.Official is { } replaced
                    && OfficialModCatalog.HasLegacyGirlNextDoorIdentity(restored) && OfficialModCatalog.CanUpgradeIdentity(restored, replaced))
                {
                    // Startup rollback restored the verified legacy payload at this receipt-owned path.
                    committedDescriptor = null;
                }
                if (format == ModFormat.Memoria)
                {
                    if (TextureModInstaller.ActiveTransactions.ContainsKey(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))))
                    {
                        throw new IOException("A texture installation is preparing this package; discovery will retry after publication.");
                    }
                    try
                    {
                        TextureModInstaller.Recover(Path.GetDirectoryName(root)!, root);
                    }
                    catch (Exception exception) when (IsPreparationError(exception))
                    {
                        throw new PackageDescriptorException(id, "Texture installation recovery failed: " + exception.Message);
                    }
                    try
                    {
                        descriptor = ReadPackageDescriptor(root, native?.Official, committedDescriptor is not null);
                    }
                    catch (Exception exception) when (IsFileError(exception))
                    {
                        descriptor = committedDescriptor;
                        descriptorFailure = exception.Message;
                    }
                }
                if (descriptor is not null)
                {
                    id = descriptor.RuntimePackageId;
                    bundledRoot = null;
                }
                if (!packageIds.Add(id))
                {
                    if (string.Equals(root, bundledRoot, StringComparison.OrdinalIgnoreCase)
                        || bundledDescriptors.Contains(root) && packages.Any(package => package.Id == id && package.Descriptor is not null && !package.IsBundled))
                    {
                        continue;
                    }
                    identities.Add($"duplicate\0{root}");
                    notices.Add(new("duplicate-package", $"A preceding mod directory owns {id}; ignoring duplicate at {root}.", id));
                    continue;
                }
                if (descriptor is not null)
                {
                    identities.Add($"descriptor\0{id}\0{descriptor.Name}\0{descriptor.Group}\0{System.Text.Json.JsonSerializer.Serialize(descriptor.Descriptions.OrderBy(entry => entry.Key, StringComparer.Ordinal))}");
                }
                bool? explicitEnabled = ModConfiguration.ReadBoolean(Path.Combine(root, "mod.ini"), "enabled");
                if (explicitEnabled is null && bundledRoot is not null && !root.Equals(bundledRoot, StringComparison.OrdinalIgnoreCase))
                {
                    explicitEnabled = ModConfiguration.ReadBoolean(Path.Combine(bundledRoot, "mod.ini"), "enabled");
                }
                int priority = 0;
                if (format is ModFormat.Tonberry or ModFormat.Demaster || descriptor is not null)
                {
                    try
                    {
                        priority = ModConfiguration.ReadPriority(Path.Combine(root, "mod.ini"));
                    }
                    catch (InvalidDataException exception)
                    {
                        throw new PackagePriorityException(id, exception.Message);
                    }
                }
                if (bundledRoot is not null || native?.Official is not null || descriptor is not null)
                {
                    explicitEnabled = MemoriaComponentPreference(memoriaSettings, id, native?.Official, descriptor) ?? explicitEnabled;
                }
                if (native?.Official is { } official && ModPackageInstaller.HasPendingEnable(modRoot, official, preferenceRoot))
                {
                    explicitEnabled = true;
                }
                bool requested = RequiredPackageReason(id) is not null || (explicitEnabled ?? nextAutoEnable);
                bool hasLocalization = format == ModFormat.Memoria && File.Exists(Path.Combine(root, "Localization", "localization.manifest.json"));
                bool hasLighting = format == ModFormat.Memoria && (File.Exists(Path.Combine(root, FieldLightingSceneIO.ComponentPath)) || bundledRoot is not null && File.Exists(Path.Combine(bundledRoot, FieldLightingSceneIO.ComponentPath)));
                bool localizationOnly = hasLocalization && Directory.EnumerateFileSystemEntries(root).All(path => Path.GetFileName(path).Equals("mod.ini", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals("Localization", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals(NativeModDescriptor.FileName, StringComparison.OrdinalIgnoreCase));
                string? nativeError = native?.Error;
                if (native?.Official is { } nativeComponent)
                {
                    if (OfficialModCatalog.HasLegacyGirlNextDoorIdentity(nativeComponent))
                    {
                        nativeError ??= "Update this component to use it with the current engine.";
                    }
                    if (!OfficialModCatalog.TargetMatches(nativeComponent.Target, edition, language))
                    {
                        nativeError ??= "This component is unavailable for the selected game edition or language.";
                    }
                    if (native.EngineVersionRange is { } range)
                    {
                        nativeError ??= OfficialModCatalog.EngineError(range, EngineBuildMetadata.CompatibilityVersion);
                    }
                }
                if (nativeError is not null)
                {
                    notices.Add(new("native-availability", nativeError, id));
                }
                if (descriptorFailure is not null)
                {
                    if (requested && committedDescriptor is not null)
                    {
                        throw new PackageDescriptorException(id, descriptorFailure);
                    }
                    notices.Add(new("native-descriptor", descriptorFailure, id, NativeModDescriptor.FileName));
                }
                string[] packageRoots = bundledRoot is not null && !string.Equals(root, bundledRoot, StringComparison.OrdinalIgnoreCase) ? [root, bundledRoot] : [root];
                List<(string Root, string Physical, string Relative)> packageFiles = [];
                foreach (string packageRoot in packageRoots)
                {
                    List<(string Physical, string Relative)> sourceFiles = [];
                    EnumerateFiles(packageRoot, packageRoot, string.Empty, id, new HashSet<string>(StringComparer.OrdinalIgnoreCase), sourceFiles, notices, hasLocalization);
                    packageFiles.AddRange(sourceFiles.OrderBy(item => item.Relative, PathOrder).Select(file => (packageRoot, file.Physical, file.Relative)));
                }
                bool textureContentOnly = format == ModFormat.Memoria && TexturePackageCompatibility.IsTextureContentOnly(packageFiles.Select(file => file.Relative), hasLocalization);
                bool compatible = ((native?.Official is not null && nativeError is null) || isCompatible(format) || localizationOnly || textureContentOnly) && nativeError is null && descriptorFailure is null;
                ModEffectiveStatus status = !compatible ? ModEffectiveStatus.IncompatibleEdition : !requested ? ModEffectiveStatus.Disabled : ModEffectiveStatus.Active;
                identities.Add($"{id}\0{root}\0{explicitEnabled}\0{requested}\0{compatible}\0{priority}");
                Dictionary<string, string> normalizedPaths = new(StringComparer.OrdinalIgnoreCase);
                List<(string Root, string Physical, string Relative)> selectedFiles = [];
                foreach ((string packageRoot, string physical, string relative) in packageFiles)
                {
                    if (relative.Equals("mod.ini", StringComparison.OrdinalIgnoreCase) || format == ModFormat.Memoria && relative.Equals(NativeModDescriptor.FileName, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(relative).StartsWith("~$", StringComparison.Ordinal) || relative.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!normalizedPaths.TryAdd(relative, packageRoot))
                    {
                        if (string.Equals(normalizedPaths[relative], packageRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            notices.Add(new("normalized-path-conflict", "A preceding path with equivalent casing owns this package address.", id, relative));
                        }
                        continue;
                    }
                    selectedFiles.Add((packageRoot, physical, relative));
                }
                PackageTextureLayouts textureLayouts = ReadTextureLayouts(id, format, selectedFiles);
                if (textureLayouts.Identity is not null)
                {
                    identities.Add($"texture-layouts\0{id}\0{textureLayouts.Identity}");
                }
                foreach ((string packageRoot, string physical, string relative) in selectedFiles.OrderBy(item => item.Relative, PathOrder))
                {
                    if (format == ModFormat.Demaster && relative.Equals(TextureLayoutManifest.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    FileInfo info = new(physical);
                    string key = id + "/" + relative + "\0" + physical;
                    visitedFiles.Add(key);
                    FileStamp stamp = new(info.Length, info.LastWriteTimeUtc.Ticks);
                    bool localizationManifest = hasLocalization && relative.Equals("Localization/localization.manifest.json", StringComparison.OrdinalIgnoreCase);
                    long localizationEpoch = localizationManifest ? localizationChangeEpoch : 0;
                    textureLayouts.Entries.TryGetValue(relative, out TextureLayoutMetadata? textureLayout);
                    if (!fileEntries.TryGetValue(key, out FileEntry? previous) || previous.Stamp != stamp || previous.LocalizationEpoch != localizationEpoch || previous.TextureLayoutIdentity != textureLayouts.Identity || forceRevalidate)
                    {
                        long revision = ++fileRevision;
                        previous = new(stamp, new ModFile(id, format, relative, revision, () => ReadVersion(packageRoot, physical, stamp)) { ContentStamp = localizationManifest ? (localizationEpoch, 0) : (stamp.Length, stamp.LastWriteTicks), TextureLayout = textureLayout, AbsolutePath = Path.GetFullPath(physical) }, localizationEpoch, textureLayouts.Identity);
                        fileEntries[key] = previous;
                    }
                    identities.Add($"{key}\0{previous.File.Revision}");
                    if (requested && compatible)
                    {
                        files.Add(previous.File);
                    }
                }
                if (status == ModEffectiveStatus.Active && !files.Any(file => file.PackageId == id))
                {
                    status = ModEffectiveStatus.NoUsableEntries;
                }
                packages.Add(new(id, descriptor?.Name ?? native?.Official?.Name ?? name, format, explicitEnabled, requested, status, root) { IsBundled = format == ModFormat.Memoria && (bundledRoot is not null || modRoot == bundledModsRoot), Official = native?.Official, Descriptor = descriptor, Priority = priority, HasLocalization = hasLocalization, HasLighting = hasLighting, LocalizationOnly = localizationOnly, TextureContentOnly = textureContentOnly });
            }
        }
        foreach (string key in fileEntries.Keys.Where(key => !visitedFiles.Contains(key)).ToArray())
        {
            fileEntries.Remove(key);
        }
        identity = string.Join('\n', identities);
        forceRevalidate = false;
        Dictionary<string, ModPackage> packagesById = packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
        IEnumerable<ModFile> orderedFiles = files.OrderBy(file => file.Format)
            .ThenByDescending(file => packagesById[file.PackageId].Priority)
            .ThenBy(file => packagesById[file.PackageId].Descriptor?.Id ?? string.Empty, StringComparer.Ordinal);
        return new(catalog.Generation + 1, packages, orderedFiles, notices) { Hardcore = hardcore, StatusPreferences = memoriaSettings.StatusSettings.ToImmutableDictionary(StringComparer.Ordinal) };
    }

    private sealed class PackagePriorityException(string packageId, string message) : IOException(message)
    {
        public string PackageId { get; } = packageId;
        public string RelativePath => "mod.ini";
    }

    private sealed class PackageDescriptorException(string packageId, string message) : IOException(message)
    {
        public string PackageId { get; } = packageId;
        public string RelativePath => NativeModDescriptor.FileName;
    }

    internal static void EnumerateFiles(string packageRoot, string directory, string prefix, string id, HashSet<string> ancestors, List<(string Physical, string Relative)> files, List<ModDiagnostic> diagnostics, bool localizationPayload = false, CancellationToken cancellationToken = default)
    {
        string actualDirectory = Path.GetFullPath(directory);
        if (!ancestors.Add(actualDirectory))
        {
            diagnostics.Add(new("link-cycle", "A package directory link forms a cycle.", id, prefix));
            return;
        }
        foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos().OrderBy(item => item.Name, PathOrder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = ModCatalogSnapshot.NormalizePath(prefix + entry.Name);
            string physical = entry.FullName;
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                FileSystemInfo? target = entry.ResolveLinkTarget(true);
                if (target is null || !IsWithin(packageRoot, target.FullName))
                {
                    diagnostics.Add(new("escaping-link-rejected", "A package link resolves outside its package.", id, relative));
                    continue;
                }
                physical = target.FullName;
            }
            if ((entry.Attributes & FileAttributes.Directory) != 0)
            {
                if (localizationPayload && relative.Equals("Localization", StringComparison.OrdinalIgnoreCase))
                {
                    // Payload inventory and snapshots belong to the background localization reader.
                    string manifest = Path.Combine(physical, "localization.manifest.json");
                    if (File.Exists(manifest))
                    {
                        files.Add((manifest, relative + "/localization.manifest.json"));
                    }
                }
                else
                {
                    EnumerateFiles(packageRoot, physical, relative + "/", id, ancestors, files, diagnostics, localizationPayload, cancellationToken);
                }
            }
            else
            {
                files.Add((physical, relative));
            }
        }
        ancestors.Remove(actualDirectory);
    }

    private static bool IsWithin(string root, string path)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] ReadVersion(string packageRoot, string path, FileStamp expected)
    {
        ValidateContentPath(packageRoot, path);
        FileInfo before = new(path);
        if (!before.Exists || new FileStamp(before.Length, before.LastWriteTimeUtc.Ticks) != expected)
        {
            throw new IOException($"Mod content changed during preparation: {path}.");
        }
        byte[] bytes = File.ReadAllBytes(path);
        FileInfo after = new(path);
        if (!after.Exists || new FileStamp(after.Length, after.LastWriteTimeUtc.Ticks) != expected)
        {
            throw new IOException($"Mod content changed during preparation: {path}.");
        }
        return bytes;
    }

    internal static void ValidateContentPath(string packageRoot, string path)
    {
        string current = packageRoot;
        foreach (string segment in new[] { string.Empty }.Concat(Path.GetRelativePath(packageRoot, path).Split(Path.DirectorySeparatorChar)))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) == 0)
            {
                continue;
            }
            FileSystemInfo entry = (attributes & FileAttributes.Directory) != 0 ? new DirectoryInfo(current) : new FileInfo(current);
            FileSystemInfo? target = entry.ResolveLinkTarget(true);
            if (target is null || !IsWithin(packageRoot, target.FullName))
            {
                throw new IOException($"A mod content link now escapes its package: {current}.");
            }
        }
    }

    private void RequestRefreshCore(DateTime time)
    {
        deferredPreparation?.Discard?.Invoke();
        deferredPreparation = null;
        memoriaSettingsViews.Clear();
        deferredDiscovery = null;
        localizationChangeEpoch++;
        if (!reloadRequested)
        {
            firstRequestedAt = time;
        }
        reloadRequested = true;
        updateOutcome = ModUpdateOutcome.Updating;
        diagnostics = Array.AsReadOnly(diagnostics.Where(diagnostic => diagnostic.Code is not ("reload-failed" or "configuration-write-failed" or "configuration-rollback-failed" or "Memoria.GuardianForceReview" or "Memoria.GuardianForceBoundary")).ToArray());
        requestedAt = time;
        retryAttempt = 0;
        retryDue = DateTime.MinValue;
    }

    private void ScheduleRetry()
    {
        deferredPreparation?.Discard?.Invoke();
        deferredPreparation = null;
        deferredDiscovery = null;
        int[] delays = [100, 250, 500, 1000];
        if (retryAttempt >= delays.Length)
        {
            return;
        }
        retryDue = DateTime.UtcNow.AddMilliseconds(delays[retryAttempt++]);
        forceRevalidate = true;
        requestedAt = DateTime.MinValue;
        firstRequestedAt = DateTime.MinValue;
        reloadRequested = true;
    }

    private void ConfigureWatcher()
    {
        foreach ((string root, FileSystemWatcher watcher) in watchers)
        {
            bool exists = Directory.Exists(root);
            string path = ExistingWatchDirectory(root);
            bool recursive = exists || !string.Equals(Path.GetDirectoryName(root), path, StringComparison.OrdinalIgnoreCase);
            string filter = recursive ? "*" : Path.GetFileName(root);
            if (!watcher.EnableRaisingEvents || !watcher.Path.Equals(path, StringComparison.OrdinalIgnoreCase) || watcher.IncludeSubdirectories != recursive || watcher.Filter != filter)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Path = path;
                watcher.IncludeSubdirectories = recursive;
                watcher.Filter = filter;
                watcher.EnableRaisingEvents = true;
            }
        }
    }

    private static string ExistingWatchDirectory(string directory)
    {
        while (!Directory.Exists(directory))
        {
            directory = Path.GetDirectoryName(directory) ?? throw new DirectoryNotFoundException($"No existing parent directory for mod path: {directory}");
        }
        return directory;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs args)
    {
        if (args.FullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment.StartsWith(".texture-stage-", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        lock (gate)
        {
            if (!disposed)
            {
                RequestRefreshCore(DateTime.UtcNow);
            }
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs args)
    {
        lock (gate)
        {
            if (!disposed)
            {
                watcherErrors.Enqueue(args.GetException().Message);
                forceRevalidate = true;
                RequestRefreshCore(DateTime.UtcNow);
            }
        }
    }

    private void ReconcilePreferences(List<ModDiagnostic>? errors = null)
    {
        try
        {
            autoEnable = ModConfiguration.ReadBoolean(Path.Combine(preferenceRoot, "mods.ini"), "auto_enable_new_mods") ?? true;
            MemoriaSettings memoriaSettings = ReadMemoriaSettings();
            installedPackages = Array.AsReadOnly(installedPackages.Select(package =>
            {
                bool? enabled = ReadPackageEnabled(package);
                if (IsMemoriaComponent(package))
                {
                    enabled = MemoriaComponentPreference(memoriaSettings, package.Id, package.Official, package.Descriptor) ?? enabled;
                }
                return package with { ExplicitEnabled = enabled, RequestedEnabled = RequiredPackageReason(package.Id) is not null || (enabled ?? autoEnable) };
            }).ToArray());
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            errors?.Add(new("configuration-read-failed", exception.Message));
        }
    }

    private string PreferencePath(ModPackage package)
    {
        string root = package.Format == ModFormat.Memoria && IsWithin(bundledModsRoot, package.RootDirectory) ? Path.Combine(preferenceRoot, nameof(ModFormat.Memoria), Path.GetFileName(package.RootDirectory)) : package.RootDirectory;
        return ConfigurationPath(root, package.Format);
    }

    private bool? ReadPackageEnabled(ModPackage package)
    {
        if (package.Descriptor is not null)
        {
            return ModConfiguration.ReadBoolean(Path.Combine(package.RootDirectory, "mod.ini"), "enabled");
        }
        bool? enabled = ModConfiguration.ReadBoolean(PreferencePath(package), "enabled");
        if (enabled is null && package.Format == ModFormat.Memoria)
        {
            string bundled = Path.Combine(bundledModsRoot, nameof(ModFormat.Memoria), Path.GetFileName(package.RootDirectory), "mod.ini");
            enabled = ModConfiguration.ReadBoolean(bundled, "enabled");
        }
        return enabled;
    }

    private void ValidatePreferencePath(string path)
    {
        string? root = IsWithin(preferenceRoot, path) ? preferenceRoot : ModsDirectories.FirstOrDefault(directory => IsWithin(directory, path));
        if (root is null)
        {
            return;
        }
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Mod preference files must not be symbolic links.");
        }
        DirectoryInfo? directory = Directory.GetParent(path);
        while (directory is not null && (IsWithin(root, directory.FullName) || ApplicationInstallation.ModPathComparer.Equals(directory.FullName, root)))
        {
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Mod preference directories must not be symbolic links.");
            }
            directory = directory.Parent;
        }
    }

    private void ValidatePreferenceTarget(ModPackage package)
    {
        if ((!Directory.Exists(package.RootDirectory) && !File.Exists(package.RootDirectory)) || (File.GetAttributes(package.RootDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The discovered package directory changed. Refresh mods before changing its setting.");
        }
        if (catalog.Packages.Any(committed => committed.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase) && committed.RootDirectory.Equals(package.RootDirectory, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        foreach ((string key, FileEntry entry) in fileEntries.Where(item => item.Value.File.PackageId.Equals(package.Id, StringComparison.OrdinalIgnoreCase)))
        {
            string path = key.Split('\0')[1];
            FileInfo file = new(path);
            if (!file.Exists || new FileStamp(file.Length, file.LastWriteTimeUtc.Ticks) != entry.Stamp)
            {
                throw new IOException("The discovered package content changed. Refresh mods before changing its setting.");
            }
            if (package.Format == ModFormat.Junction)
            {
                ValidateJunctionVersion(package.RootDirectory, path, entry.Stamp);
            }
            else if (IsWithin(package.RootDirectory, path))
            {
                ValidateContentPath(package.RootDirectory, path);
            }
        }
    }

    private ModOperationResult OperationFailed(Exception exception, string? packageId = null)
    {
        updateOutcome = ModUpdateOutcome.Failed;
        diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("configuration-write-failed", exception.Message, packageId) });
        return new(false, exception.Message);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static bool IsFileError(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException;
    }

    private static bool IsPreparationError(Exception exception)
    {
        return exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
    }

    private sealed record FileEntry(FileStamp Stamp, ModFile File, long LocalizationEpoch = 0, string? TextureLayoutIdentity = null);
    private readonly record struct FileStamp(long Length, long LastWriteTicks);

    private sealed class Subscription(ModRuntime owner, Action<ModCatalogSnapshot> callback) : IDisposable
    {
        private ModRuntime? runtime = owner;

        public void Dispose()
        {
            ModRuntime? current = Interlocked.Exchange(ref runtime, null);
            if (current is not null)
            {
                lock (current.gate)
                {
                    current.subscribers.Remove(callback);
                }
            }
        }
    }
}
