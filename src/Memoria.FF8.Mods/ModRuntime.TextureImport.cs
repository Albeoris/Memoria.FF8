namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    /// <summary>Prepares an immutable texture overlay before touching live files, then publishes on the owner thread.</summary>
    public bool ApplyTextureImport(TextureModInstallTransaction transaction, ModPackage candidatePackage)
    {
        if (Environment.CurrentManagedThreadId != ownerThread)
        {
            throw new InvalidOperationException("Texture imports must publish on the runtime's owning thread.");
        }
        Action? afterCommit = null;
        Action<ModCatalogSnapshot>[] listeners = [];
        ModCatalogSnapshot? published = null;
        lock (gate)
        {
            ThrowIfDisposed();
            if (candidatePackage.Format != ModFormat.Memoria || !Path.GetFullPath(candidatePackage.RootDirectory).Equals(transaction.Destination, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The texture transaction does not match its destination package.");
            }
            ModPackage? existing = catalog.Packages.FirstOrDefault(package => package.Id.Equals(candidatePackage.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null && !Path.GetFullPath(existing.RootDirectory).Equals(transaction.Destination, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Another package root owns this native mod identity.");
            }
            if (!candidatePackage.RequestedEnabled)
            {
                transaction.Apply();
                transaction.Commit();
                RequestRefreshCore(DateTime.MinValue);
                return true;
            }
            HashSet<string> replaced = transaction.Files.Select(file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<ModPackage> packages = catalog.Packages.Where(package => !package.Id.Equals(candidatePackage.Id, StringComparison.OrdinalIgnoreCase)).Append(candidatePackage with { Status = ModEffectiveStatus.Active }).ToList();
            Dictionary<string, ModPackage> byId = packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
            List<ModFile> files = catalog.Files.Where(file => !file.PackageId.Equals(candidatePackage.Id, StringComparison.OrdinalIgnoreCase) || !replaced.Contains(file.RelativePath)).ToList();
            foreach (TextureModOutput output in transaction.Files)
            {
                if (output.RelativePath is not (NativeModDescriptor.FileName or "mod.ini"))
                {
                    byte[] frozen = output.Content.ToArray();
                    files.Add(new(candidatePackage.Id, ModFormat.Memoria, output.RelativePath, ++fileRevision, () => frozen));
                }
            }
            ModCatalogSnapshot candidate = new(catalog.Generation + 1, packages, files.OrderBy(file => file.Format).ThenByDescending(file => byId[file.PackageId].Priority).ThenBy(file => byId[file.PackageId].Descriptor?.Id ?? string.Empty, StringComparer.Ordinal), catalog.Diagnostics)
            {
                Hardcore = catalog.Hardcore,
                StatusPreferences = catalog.StatusPreferences,
                PreparedStatusMods = catalog.PreparedStatusMods
            };
            ModPreparationResult result;
            try
            {
                result = prepare(candidate);
            }
            catch (Exception exception) when (IsPreparationError(exception))
            {
                diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("texture-import-failed", exception.Message, candidatePackage.Id) });
                return false;
            }
            if (!result.Success || result.Deferred)
            {
                result.Discard?.Invoke();
                diagnostics = Array.AsReadOnly((result.Diagnostics ?? []).Append(new ModDiagnostic("texture-import-failed", result.Deferred ? "Texture preparation is still pending; retry the import when the renderer is ready." : "Texture preparation failed; destination and live generation remain unchanged.", candidatePackage.Id)).ToArray());
                return false;
            }
            try
            {
                transaction.Apply();
                transaction.Commit(result.Commit);
            }
            catch (Exception exception) when (IsPreparationError(exception))
            {
                result.Discard?.Invoke();
                transaction.Dispose();
                diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("texture-import-failed", exception.Message, candidatePackage.Id) });
                return false;
            }
            diagnostics = Array.AsReadOnly(candidate.Diagnostics.Concat(result.Diagnostics ?? []).ToArray());
            catalog = new(candidate.Generation, candidate.Packages, candidate.Files, diagnostics)
            {
                Hardcore = candidate.Hardcore,
                StatusPreferences = candidate.StatusPreferences,
                PreparedStatusMods = candidate.PreparedStatusMods
            };
            installedPackages = catalog.Packages;
            memoriaSettingsViews.Clear();
            pendingPreparation?.Discard?.Invoke();
            pending = null;
            pendingPreparation = null;
            pendingLighting = null;
            deferredDiscovery = null;
            lastIdentity = null;
            forceRevalidate = true;
            RequestRefreshCore(DateTime.MinValue);
            updateOutcome = ModUpdateOutcome.Idle;
            afterCommit = result.AfterCommit;
            listeners = subscribers.ToArray();
            published = catalog;
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
                listener(published!);
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
}
