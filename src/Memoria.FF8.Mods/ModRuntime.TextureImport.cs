namespace Memoria.FF8.Mods;

public enum TextureImportApplyResult
{
    Deferred,
    Applied,
    Failed
}

public sealed partial class ModRuntime
{
    private sealed record ExplicitTextureImport(TextureModInstallTransaction Transaction, ModPackage Package, ModCatalogSnapshot Candidate, long Epoch)
    {
        public ModPreparationResult? Preparation { get; set; }
    }

    private ExplicitTextureImport? explicitTextureImport;

    public void CancelTextureImport(TextureModInstallTransaction transaction)
    {
        if (Environment.CurrentManagedThreadId != ownerThread)
        {
            throw new InvalidOperationException("Texture imports must cancel on the runtime's owning thread.");
        }
        lock (gate)
        {
            if (explicitTextureImport is { } active && ReferenceEquals(active.Transaction, transaction))
            {
                active.Preparation?.Discard?.Invoke();
                explicitTextureImport = null;
            }
        }
    }

    public bool ApplyTextureImport(TextureModInstallTransaction transaction, ModPackage candidatePackage)
    {
        TextureImportApplyResult result = TryApplyTextureImport(transaction, candidatePackage);
        if (result == TextureImportApplyResult.Deferred)
        {
            CancelTextureImport(transaction);
        }
        return result == TextureImportApplyResult.Applied;
    }

    /// <summary>Prepares an immutable texture overlay before touching live files, then publishes on the owner thread.</summary>
    public TextureImportApplyResult TryApplyTextureImport(TextureModInstallTransaction transaction, ModPackage candidatePackage, Action? validateBeforeApply = null)
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
            if (explicitTextureImport is { } stale && (!ReferenceEquals(stale.Transaction, transaction) || stale.Epoch != localizationChangeEpoch))
            {
                stale.Preparation?.Discard?.Invoke();
                explicitTextureImport = null;
                diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("texture-import-stale", "Mod inputs changed during preparation. Review the import again.", candidatePackage.Id) });
                return TextureImportApplyResult.Failed;
            }
            ModPackage? existing = installedPackages.FirstOrDefault(package => package.Id.Equals(candidatePackage.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null && !Path.GetFullPath(existing.RootDirectory).Equals(transaction.Destination, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Another package root owns this native mod identity.");
            }
            candidatePackage = existing ?? candidatePackage;
            if (!candidatePackage.RequestedEnabled)
            {
                validateBeforeApply?.Invoke();
                transaction.Apply();
                transaction.Commit();
                RequestRefreshCore(DateTime.MinValue);
                return TextureImportApplyResult.Applied;
            }
            if (existing?.Status == ModEffectiveStatus.IncompatibleEdition)
            {
                diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("texture-import-incompatible", "This existing package is incompatible with the selected edition. Import into a separate texture-only native package instead.", candidatePackage.Id) });
                return TextureImportApplyResult.Failed;
            }
            if (explicitTextureImport is null)
            {
                pendingPreparation?.Discard?.Invoke();
                deferredPreparation?.Discard?.Invoke();
                deferredPreparation = null;
                pending = null;
                pendingPreparation = null;
                pendingLighting = null;
                deferredDiscovery = null;
                HashSet<string> replaced = transaction.FrozenFiles.Select(file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                IEnumerable<string> preservedPaths = catalog.Files.Concat(fileEntries.Values.Select(entry => entry.File)).Where(file => file.PackageId.Equals(candidatePackage.Id, StringComparison.OrdinalIgnoreCase) && !replaced.Contains(file.RelativePath)).Select(file => file.RelativePath);
                bool textureContentOnly = TexturePackageCompatibility.IsTextureContentOnly(preservedPaths.Concat(transaction.FrozenFiles.Select(file => file.RelativePath)), candidatePackage.HasLocalization);
                if (!isCompatible(ModFormat.Memoria) && !textureContentOnly && !candidatePackage.LocalizationOnly)
                {
                    diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("texture-import-incompatible", "This package contains gameplay or unsupported contributions for the selected edition. Choose a separate texture-only native package.", candidatePackage.Id) });
                    return TextureImportApplyResult.Failed;
                }
                candidatePackage = candidatePackage with { TextureContentOnly = textureContentOnly };
                List<ModPackage> packages = catalog.Packages.Where(package => !package.Id.Equals(candidatePackage.Id, StringComparison.OrdinalIgnoreCase)).Append(candidatePackage with { Status = ModEffectiveStatus.Active }).ToList();
                Dictionary<string, ModPackage> byId = packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
                List<ModFile> files = catalog.Files.Where(file => !file.PackageId.Equals(candidatePackage.Id, StringComparison.OrdinalIgnoreCase) || !replaced.Contains(file.RelativePath)).ToList();
                foreach (TextureModOutput output in transaction.FrozenFiles)
                {
                    if (output.RelativePath is not (NativeModDescriptor.FileName or "mod.ini"))
                    {
                        byte[] frozen = output.Content;
                        files.Add(new(candidatePackage.Id, ModFormat.Memoria, output.RelativePath, ++fileRevision, () => frozen));
                    }
                }
                ModCatalogSnapshot initialCandidate = new(catalog.Generation + 1, packages, files.OrderBy(file => file.Format).ThenByDescending(file => byId[file.PackageId].Priority).ThenBy(file => byId[file.PackageId].Descriptor?.Id ?? string.Empty, StringComparer.Ordinal), catalog.Diagnostics)
                {
                    Hardcore = catalog.Hardcore,
                    StatusPreferences = catalog.StatusPreferences,
                    PreparedStatusMods = catalog.PreparedStatusMods
                };
                explicitTextureImport = new(transaction, candidatePackage, initialCandidate, localizationChangeEpoch);
            }
            ExplicitTextureImport active = explicitTextureImport;
            ModCatalogSnapshot candidate = active.Candidate;
            ModPreparationResult result;
            try
            {
                result = prepare(candidate);
                active.Preparation = result;
            }
            catch (Exception exception) when (IsPreparationError(exception))
            {
                diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("texture-import-failed", exception.Message, candidatePackage.Id) });
                active.Preparation?.Discard?.Invoke();
                explicitTextureImport = null;
                return TextureImportApplyResult.Failed;
            }
            if (result.Deferred)
            {
                updateOutcome = ModUpdateOutcome.Updating;
                return TextureImportApplyResult.Deferred;
            }
            if (!result.Success)
            {
                result.Discard?.Invoke();
                explicitTextureImport = null;
                diagnostics = Array.AsReadOnly((result.Diagnostics ?? []).Append(new ModDiagnostic("texture-import-failed", "Texture preparation failed; destination and live generation remain unchanged.", candidatePackage.Id)).ToArray());
                return TextureImportApplyResult.Failed;
            }
            try
            {
                validateBeforeApply?.Invoke();
                transaction.Apply();
                transaction.Commit(result.Commit);
            }
            catch (Exception exception) when (IsPreparationError(exception))
            {
                result.Discard?.Invoke();
                explicitTextureImport = null;
                transaction.Dispose();
                diagnostics = Array.AsReadOnly(new[] { new ModDiagnostic("texture-import-failed", exception.Message, candidatePackage.Id) });
                return TextureImportApplyResult.Failed;
            }
            explicitTextureImport = null;
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
        return TextureImportApplyResult.Applied;
    }
}
