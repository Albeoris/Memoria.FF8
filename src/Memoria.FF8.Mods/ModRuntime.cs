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
    private readonly string bundledModsRoot;
    private readonly Func<ModFormat, bool> isCompatible;
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
    private string? lastIdentity;
    private string? pendingIdentity;

    public ModRuntime(string? applicationBaseDirectory = null, Func<ModFormat, bool>? isCompatible = null, Func<ModCatalogSnapshot, ModPreparationResult>? prepare = null, bool watch = true, TimeSpan? debounce = null, IEnumerable<string>? additionalBaseDirectories = null, string? modsDirectory = null)
    {
        modsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modsDirectory ?? DefaultModsDirectory(applicationBaseDirectory ?? AppContext.BaseDirectory)));
        IEnumerable<string> roots = (additionalBaseDirectories ?? []).Select(directory => Path.Combine(directory, "Mods")).Append(modsRoot);
        bundledModsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(applicationBaseDirectory ?? AppContext.BaseDirectory, "BundledMods")));
        if (Directory.Exists(bundledModsRoot))
        {
            roots = roots.Append(bundledModsRoot);
        }
        ModsDirectories = Array.AsReadOnly(roots.Select(directory => Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        this.isCompatible = isCompatible ?? (_ => true);
        this.prepare = prepare ?? (_ => ModPreparationResult.Accepted);
        this.debounce = debounce ?? TimeSpan.FromMilliseconds(250);
        if (watch)
        {
            foreach (string directory in ModsDirectories)
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
        if (Path.GetFileName(applicationDirectory).Equals("Data", StringComparison.OrdinalIgnoreCase) && packageDirectory is not null && File.Exists(Path.Combine(packageDirectory, "Memoria.Launcher.exe")) && Directory.Exists(Path.Combine(packageDirectory, "Mods")))
        {
            return Path.Combine(packageDirectory, "Mods");
        }
        return Path.Combine(applicationDirectory, "Mods");
    }

    public IReadOnlyList<string> ModsDirectories { get; }
    public string PrimaryModsDirectory => modsRoot;

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
                return ModServiceView.Create(catalog, installedPackages, autoEnable, reloadRequested || pending is not null, diagnostics, updateOutcome, changedPackageIds);
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
            if (!isCompatible(package.Format))
            {
                return new(false, $"{package.Name} is incompatible with the selected game-data edition.");
            }
            try
            {
                ValidatePreferenceTarget(package);
                ModConfiguration.WriteBoolean(ConfigurationPath(package.RootDirectory, package.Format), "enabled", enabled);
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
            List<(string Path, byte[]? Original)> backups = [];
            try
            {
                bool current = ModConfiguration.ReadBoolean(Path.Combine(modsRoot, "mods.ini"), "auto_enable_new_mods") ?? true;
                if (current && !enabled)
                {
                    foreach (ModPackage package in installedPackages)
                    {
                        string path = ConfigurationPath(package.RootDirectory, package.Format);
                        if (ModConfiguration.ReadBoolean(path, "enabled") is null)
                        {
                            backups.Add((path, File.Exists(path) ? File.ReadAllBytes(path) : null));
                            ModConfiguration.WriteBoolean(path, "enabled", true);
                        }
                    }
                }
                ModConfiguration.WriteBoolean(Path.Combine(modsRoot, "mods.ini"), "auto_enable_new_mods", enabled);
                autoEnable = enabled;
                ReconcilePreferences();
                RequestRefreshCore(DateTime.MinValue);
                return ModOperationResult.Completed;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                List<ModDiagnostic> errors = [new("configuration-write-failed", exception.Message)];
                foreach ((string path, byte[]? original) in backups.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (original is null)
                        {
                            File.Delete(path);
                        }
                        else
                        {
                            ModConfiguration.WriteAtomic(path, original);
                        }
                    }
                    catch (Exception rollbackException) when (IsFileError(rollbackException))
                    {
                        errors.Add(new("configuration-rollback-failed", rollbackException.Message));
                    }
                }
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
            DateTime now = DateTime.UtcNow;
            if (!reloadRequested || (!force && (now < retryDue || now - requestedAt < debounce && now - firstRequestedAt < TimeSpan.FromSeconds(2))))
            {
                return false;
            }
            reloadRequested = false;
            pending = null;
            pendingPreparation = null;
            try
            {
                ConfigureWatcher();
                ModCatalogSnapshot candidate = Discover(out bool nextAutoEnable, out string identity);
                changedPackageIds.Clear();
                foreach (string id in candidate.Packages.Select(package => package.Id).Concat(catalog.Packages.Select(package => package.Id)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    IEnumerable<(string Path, FileStamp Stamp)> before = committedFileEntries.Where(entry => entry.Value.File.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (entry.Key, entry.Value.Stamp));
                    IEnumerable<(string Path, FileStamp Stamp)> after = fileEntries.Where(entry => entry.Value.File.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (entry.Key, entry.Value.Stamp));
                    if (!before.SequenceEqual(after) || candidate.Packages.SingleOrDefault(package => package.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Priority != catalog.Packages.SingleOrDefault(package => package.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Priority)
                    {
                        changedPackageIds.Add(id);
                    }
                }
                installedPackages = Array.AsReadOnly(candidate.Packages.Select(package => package.Status == ModEffectiveStatus.Active ? package with { Status = ModEffectiveStatus.ReloadFailure } : package).ToArray());
                autoEnable = nextAutoEnable;
                if (identity == lastIdentity)
                {
                    installedPackages = catalog.Packages;
                    diagnostics = Array.AsReadOnly(candidate.Diagnostics.Concat(diagnostics.Where(diagnostic => diagnostic.Code == "subscriber-failed")).Distinct().ToArray());
                    updateOutcome = ModUpdateOutcome.Idle;
                    changedPackageIds.Clear();
                    return false;
                }
                ModPreparationResult result = prepare(candidate);
                diagnostics = Array.AsReadOnly(candidate.Diagnostics.Concat(result.Diagnostics ?? []).ToArray());
                if (!result.Success)
                {
                    diagnostics = Array.AsReadOnly(diagnostics.Append(new ModDiagnostic("reload-failed", "Mod preparation failed; the last committed generation remains active.")).ToArray());
                    updateOutcome = ModUpdateOutcome.Failed;
                    ScheduleRetry();
                    return false;
                }
                if (result.UsablePackageIds is not null)
                {
                    HashSet<string> usable = new(result.UsablePackageIds, StringComparer.OrdinalIgnoreCase);
                    candidate = new(candidate.Generation, candidate.Packages.Select(package => package.Status == ModEffectiveStatus.Active && !usable.Contains(package.Id) ? package with { Status = ModEffectiveStatus.NoUsableEntries } : package), candidate.Files, diagnostics);
                }
                else
                {
                    candidate = new(candidate.Generation, candidate.Packages, candidate.Files, diagnostics);
                }
                autoEnable = nextAutoEnable;
                installedPackages = candidate.Packages;
                updateOutcome = ModUpdateOutcome.Updating;
                pending = candidate;
                pendingPreparation = result;
                pendingIdentity = identity;
                return true;
            }
            catch (Exception exception) when (IsPreparationError(exception))
            {
                ModTextureLoadException? textureFailure = exception as ModTextureLoadException;
                PackagePriorityException? priorityFailure = exception as PackagePriorityException;
                List<ModDiagnostic> errors = [new("reload-failed", $"{exception.Message} The last committed generation remains active.", textureFailure?.PackageId ?? priorityFailure?.PackageId, textureFailure?.RelativePath ?? priorityFailure?.RelativePath)];
                if (priorityFailure is not null)
                {
                    errors.Add(new("invalid-package-priority", priorityFailure.Message, priorityFailure.PackageId, priorityFailure.RelativePath));
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
            if (pending is null || reloadRequested)
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
                pending = null;
                pendingPreparation = null;
                updateOutcome = ModUpdateOutcome.Failed;
                ScheduleRetry();
                return false;
            }
            catalog = pending;
            committedFileEntries = new(fileEntries, StringComparer.Ordinal);
            updateOutcome = ModUpdateOutcome.Idle;
            changedPackageIds.Clear();
            installedPackages = catalog.Packages;
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
            foreach ((_, FileSystemWatcher watcher) in watchers)
            {
                watcher.Dispose();
            }
            subscribers.Clear();
            pending = null;
            pendingPreparation = null;
        }
    }

    private ModCatalogSnapshot Discover(out bool nextAutoEnable, out string identity)
    {
        nextAutoEnable = ModConfiguration.ReadBoolean(Path.Combine(modsRoot, "mods.ini"), "auto_enable_new_mods") ?? true;
        List<ModPackage> packages = [];
        List<ModFile> files = [];
        List<ModDiagnostic> notices = [];
        while (watcherErrors.TryDequeue(out string? error))
        {
            notices.Add(new("watcher-error", error));
        }
        List<string> identities = [nextAutoEnable.ToString()];
        HashSet<string> visitedFiles = new(StringComparer.Ordinal);
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
            if (format == ModFormat.Memoria)
            {
                foreach (string root in roots)
                {
                    if (string.Equals(Path.GetDirectoryName(root), Path.Combine(bundledModsRoot, format.ToString()), StringComparison.OrdinalIgnoreCase) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0)
                    {
                        bundledPackages.TryAdd(Path.GetFileName(root), root);
                    }
                }
            }
            foreach (string root in roots)
            {
                string name = Path.GetFileName(root);
                string id = $"{format}/{name}";
                bundledPackages.TryGetValue(name, out string? bundledRoot);
                if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    notices.Add(new("package-link-rejected", "Package roots must be unpacked directories, not links.", id));
                    continue;
                }
                if (!packageIds.Add(id))
                {
                    if (string.Equals(root, bundledRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    identities.Add($"duplicate\0{root}");
                    notices.Add(new("duplicate-package", $"A preceding mod directory owns {id}; ignoring duplicate at {root}.", id));
                    continue;
                }
                bool? explicitEnabled = ModConfiguration.ReadBoolean(Path.Combine(root, "mod.ini"), "enabled");
                int priority = 0;
                if (format is ModFormat.Tonberry or ModFormat.Demaster)
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
                bool requested = explicitEnabled ?? nextAutoEnable;
                bool compatible = isCompatible(format);
                ModEffectiveStatus status = !compatible ? ModEffectiveStatus.IncompatibleEdition : !requested ? ModEffectiveStatus.Disabled : ModEffectiveStatus.Active;
                identities.Add($"{id}\0{root}\0{explicitEnabled}\0{requested}\0{compatible}\0{priority}");
                string[] packageRoots = bundledRoot is not null && !string.Equals(root, bundledRoot, StringComparison.OrdinalIgnoreCase) ? [root, bundledRoot] : [root];
                List<(string Root, string Physical, string Relative)> packageFiles = [];
                foreach (string packageRoot in packageRoots)
                {
                    List<(string Physical, string Relative)> sourceFiles = [];
                    EnumerateFiles(packageRoot, packageRoot, string.Empty, id, new HashSet<string>(StringComparer.OrdinalIgnoreCase), sourceFiles, notices);
                    packageFiles.AddRange(sourceFiles.OrderBy(item => item.Relative, PathOrder).Select(file => (packageRoot, file.Physical, file.Relative)));
                }
                Dictionary<string, string> normalizedPaths = new(StringComparer.OrdinalIgnoreCase);
                List<(string Root, string Physical, string Relative)> selectedFiles = [];
                foreach ((string packageRoot, string physical, string relative) in packageFiles)
                {
                    if (relative.Equals("mod.ini", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(relative).StartsWith('.') && relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
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
                foreach ((string packageRoot, string physical, string relative) in selectedFiles.OrderBy(item => item.Relative, PathOrder))
                {
                    FileInfo info = new(physical);
                    string key = id + "/" + relative + "\0" + physical;
                    visitedFiles.Add(key);
                    FileStamp stamp = new(info.Length, info.LastWriteTimeUtc.Ticks);
                    if (!fileEntries.TryGetValue(key, out FileEntry? previous) || previous.Stamp != stamp || forceRevalidate)
                    {
                        long revision = ++fileRevision;
                        previous = new(stamp, new ModFile(id, format, relative, revision, () => ReadVersion(packageRoot, physical, stamp)));
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
                packages.Add(new(id, name, format, explicitEnabled, requested, status, root) { Priority = priority });
            }
        }
        foreach (string key in fileEntries.Keys.Where(key => !visitedFiles.Contains(key)).ToArray())
        {
            fileEntries.Remove(key);
        }
        identity = string.Join('\n', identities);
        forceRevalidate = false;
        Dictionary<string, int> priorities = packages.ToDictionary(package => package.Id, package => package.Priority, StringComparer.OrdinalIgnoreCase);
        IEnumerable<ModFile> orderedFiles = files.OrderBy(file => file.Format).ThenByDescending(file => file.Format is ModFormat.Tonberry or ModFormat.Demaster ? priorities[file.PackageId] : 0);
        return new(catalog.Generation + 1, packages, orderedFiles, notices);
    }

    private sealed class PackagePriorityException(string packageId, string message) : IOException(message)
    {
        public string PackageId { get; } = packageId;
        public string RelativePath => "mod.ini";
    }

    private static void EnumerateFiles(string packageRoot, string directory, string prefix, string id, HashSet<string> ancestors, List<(string Physical, string Relative)> files, List<ModDiagnostic> diagnostics)
    {
        string actualDirectory = Path.GetFullPath(directory);
        if (!ancestors.Add(actualDirectory))
        {
            diagnostics.Add(new("link-cycle", "A package directory link forms a cycle.", id, prefix));
            return;
        }
        foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos().OrderBy(item => item.Name, PathOrder))
        {
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
                EnumerateFiles(packageRoot, physical, relative + "/", id, ancestors, files, diagnostics);
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

    private static void ValidateContentPath(string packageRoot, string path)
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
            autoEnable = ModConfiguration.ReadBoolean(Path.Combine(modsRoot, "mods.ini"), "auto_enable_new_mods") ?? true;
            installedPackages = Array.AsReadOnly(installedPackages.Select(package =>
            {
                bool? enabled = ModConfiguration.ReadBoolean(ConfigurationPath(package.RootDirectory, package.Format), "enabled");
                return package with { ExplicitEnabled = enabled, RequestedEnabled = enabled ?? autoEnable };
            }).ToArray());
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            errors?.Add(new("configuration-read-failed", exception.Message));
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

    private sealed record FileEntry(FileStamp Stamp, ModFile File);
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
