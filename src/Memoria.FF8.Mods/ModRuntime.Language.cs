using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods;

public sealed class LanguageSelectionChange : IDisposable
{
    private readonly FileStream coordination;
    private Action? restore;

    internal LanguageSelectionChange(FileStream coordination, Action restore)
    {
        this.coordination = coordination;
        this.restore = restore;
    }

    public void Commit()
    {
        restore = null;
    }

    public void Dispose()
    {
        try
        {
            Interlocked.Exchange(ref restore, null)?.Invoke();
        }
        finally
        {
            coordination.Dispose();
        }
    }
}

public sealed partial class ModRuntime
{
    private static bool IsOptInLanguage(ModPackage package)
    {
        return package.Format == ModFormat.Junction ? LanguagePackages.IsLanguage(package)
            : LanguagePackages.IsDependency(package) || package.Descriptor?.LanguageOffers.Count > 0
                || package.Descriptor?.Group == "Localization" && package.HasLocalization || package.Official?.LanguageOffers?.Count > 0;
    }

    private (HashSet<string> Required, Dictionary<string, string> Unavailable) InspectLanguageDependencies(IEnumerable<string> roots, IReadOnlyDictionary<string, IReadOnlyDictionary<string, ModPackageInstallResult>> managed, MemoriaSettings settings)
    {
        List<ModPackage> declarations = [];
        foreach (string root in roots)
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 || !File.Exists(Path.Combine(root, NativeModDescriptor.FileName)))
            {
                continue;
            }
            try
            {
                string modRoot = Path.GetDirectoryName(Path.GetDirectoryName(root))!;
                NativeModDescriptor descriptor = NativeModDescriptor.Read(Path.Combine(root, NativeModDescriptor.FileName));
                if (descriptor.LanguageOffers.Count == 0 && !descriptor.IsLanguageDependency)
                {
                    continue;
                }
                ModPackageInstallResult? receipt = managed.GetValueOrDefault(modRoot)?.GetValueOrDefault(descriptor.RuntimePackageId);
                bool enabled = !descriptor.IsLanguageDependency && (MemoriaComponentPreference(settings, descriptor.RuntimePackageId, receipt?.Official, descriptor)
                    ?? ModConfiguration.ReadBoolean(Path.Combine(root, "mod.ini"), "enabled")) == true;
                declarations.Add(new(descriptor.RuntimePackageId, descriptor.Name, ModFormat.Memoria, enabled, enabled, ModEffectiveStatus.Disabled, root)
                {
                    Descriptor = descriptor,
                    Official = receipt?.Official,
                    OfficialAvailable = receipt?.Installed == true && nativeRootUnavailable?.Invoke(modRoot) is null
                });
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                // Main discovery records descriptor and settings diagnostics for the package.
            }
        }
        GameLanguageProfile profile = new(edition, language);
        HashSet<string> required = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> unavailable = new(ApplicationInstallation.ModPathComparer);
        foreach (ModPackage root in declarations.Where(package => package.RequestedEnabled && package.Descriptor!.LanguageOffers.Any(offer => offer.NativeVariants.Any(variant => variant.Matches(profile)))))
        {
            try
            {
                foreach (ModPackage member in LanguagePackages.ResolveInstalledDependencies(root, declarations, profile))
                {
                    required.Add(member.Id);
                }
            }
            catch (InvalidDataException exception)
            {
                unavailable[root.RootDirectory] = exception.Message;
            }
        }
        return (required, unavailable);
    }

    /// <summary>Stages the chosen provider and the provider previously owned by the picker. Dispose restores preferences until committed.</summary>
    public LanguageSelectionChange StageLanguageSelection(GameLanguageOffer offer, string? packageId, GameLanguageChoice? previous)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (!offer.CanSelect || offer.IsOriginal != (packageId is null))
            {
                throw new InvalidOperationException(offer.Requirement ?? "The selected language has no installed provider.");
            }
            string? previousId = previous?.PackageId ?? languagePackageId;
            if (previousId is null && previous is null)
            {
                ModPackage[] legacy = Catalog.Packages.Where(package => package.Status == ModEffectiveStatus.Active && package.ExplicitEnabled == true && IsOptInLanguage(package)).ToArray();
                if (legacy.Length == 1)
                {
                    previousId = legacy[0].Id;
                }
            }
            string path = LocalizationPreferencesPath;
            ValidatePreferencePath(path);
            FileStream coordination = ModConfiguration.LockMemoriaSettings(path);
            Dictionary<string, byte[]?> originals = new(ApplicationInstallation.ModPathComparer);
            HashSet<string> editedComponents = new(StringComparer.Ordinal);
            string? previousManagedPackage = languagePackageId;
            string? previousManagedTarget = languagePrimaryTarget;
            void Backup(string file)
            {
                ValidatePreferencePath(file);
                if (!originals.ContainsKey(file))
                {
                    originals.Add(file, File.Exists(file) ? File.ReadAllBytes(file) : null);
                }
            }
            void Restore()
            {
                lock (gate)
                {
                    using FileStream? settingsLock = originals.ContainsKey(MemoriaSettingsPath) ? ModConfiguration.LockMemoriaSettings(MemoriaSettingsPath) : null;
                    foreach ((string file, byte[]? content) in originals.Reverse())
                    {
                        if (ApplicationInstallation.ModPathComparer.Equals(file, MemoriaSettingsPath))
                        {
                            MemoriaSettings current = ReadMemoriaSettings();
                            MemoriaSettings prior = content is null ? new() : ParseMemoriaSettings(content);
                            foreach (string component in editedComponents)
                            {
                                if (prior.Components.TryGetValue(component, out bool enabled))
                                {
                                    current.Components[component] = enabled;
                                }
                                else
                                {
                                    current.Components.Remove(component);
                                }
                            }
                            if (content is null && current.Components.Count == 0 && current.StatusSettings.Count == 0)
                            {
                                File.Delete(file);
                            }
                            else
                            {
                                WriteMemoriaSettings(current);
                            }
                        }
                        else if (content is null)
                        {
                            File.Delete(file);
                        }
                        else
                        {
                            ModConfiguration.WriteAtomic(file, content);
                        }
                    }
                    languagePackageId = previousManagedPackage;
                    languagePrimaryTarget = previousManagedTarget;
                    if (!disposed)
                    {
                        pendingPreparation?.Discard?.Invoke();
                        pendingPreparation = null;
                        pending = null;
                        pendingLighting = null;
                        pendingFieldScenes3D = null;
                        ReconcilePreferences();
                        RequestRefreshCore(DateTime.MinValue);
                    }
                }
            }
            try
            {
                IReadOnlyList<ModPackage> available = installedPackages;
                if (packageId is not null && (!available.Any(package => package.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase))
                    || offer.RequiresAcquisition || offer.CanUpdate))
                {
                    available = Discover(out _, out _).Packages;
                }
                ModPackage? selected = packageId is null ? null : available.FirstOrDefault(package => package.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
                if (packageId is not null && (selected is null || !LanguagePackages.IsLanguage(selected, offer.Release is null ? [] : [offer.Release])))
                {
                    throw new InvalidOperationException("The installed language provider could not be discovered.");
                }
                ModPackage? old = previousId is null || string.Equals(previousId, packageId, StringComparison.OrdinalIgnoreCase)
                    ? null : available.FirstOrDefault(package => package.Id.Equals(previousId, StringComparison.OrdinalIgnoreCase));
                if (old is not null && (LanguagePackages.IsLanguage(old) || previous?.ProviderId == GameLanguageInventory.ProviderIdentity(old, null)))
                {
                    SetProvider(old, false, null, Backup, editedComponents);
                }
                string? target = null;
                if (selected is not null)
                {
                    LanguagePackages.ResolveInstalledDependencies(selected, available, offer.Profile!);
                    if (selected.HasLocalization)
                    {
                        target = LanguagePackages.ReadNativeTarget(selected);
                        if (LanguagePackages.DisplayTag(target) != offer.DisplayTag)
                        {
                            throw new InvalidDataException("The localization manifest target does not match the chosen display language.");
                        }
                    }
                    SetProvider(selected, true, offer.Metadata, Backup, editedComponents);
                }
                Backup(path);
                languagePackageId = selected?.HasLocalization == true ? selected.Id : null;
                languagePrimaryTarget = target;
                ModConfiguration.WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(new LocalizationPreferences(localizationOrder, authoredPrimaryTarget, languagePackageId, languagePrimaryTarget)));
                ReconcilePreferences();
                RequestRefreshCore(DateTime.MinValue);
                return new(coordination, Restore);
            }
            catch
            {
                try
                {
                    Restore();
                }
                finally
                {
                    coordination.Dispose();
                }
                throw;
            }
        }
    }

    private void SetProvider(ModPackage package, bool enabled, LanguageOfferMetadata? selection, Action<string> backup, HashSet<string> editedComponents)
    {
        ValidatePreferenceTarget(package);
        if (MemoriaComponentSettingsKey(package.Id, package.Official, package.Descriptor) is string component)
        {
            using FileStream settingsLock = ModConfiguration.LockMemoriaSettings(MemoriaSettingsPath);
            backup(MemoriaSettingsPath);
            MemoriaSettings settings = ReadMemoriaSettings();
            editedComponents.Add(component);
            settings.Components[component] = enabled;
            WriteMemoriaSettings(settings);
            return;
        }
        string path = PreferencePath(package);
        backup(path);
        if (package.Format == ModFormat.Junction && selection is not null && (selection.Value is not null || selection.Option is not null))
        {
            JunctionOptionValue value = package.JunctionMetadata is { } manifest ? LanguagePackages.JunctionOption(manifest, selection)
                ?? throw new InvalidDataException("This release does not declare the selected Junction language option.")
                : throw new InvalidDataException("The installed Junction manifest is unavailable.");
            ModConfiguration.WriteJunctionOption(path, value.Setting, value.Value);
        }
        ModConfiguration.WriteBoolean(path, "enabled", enabled);
    }

    public bool PendingLanguageMatches(string? packageId, string? deactivatedPackageId)
    {
        lock (gate)
        {
            if (pending is null && (reloadRequested || updateOutcome is ModUpdateOutcome.Failed or ModUpdateOutcome.Blocked or ModUpdateOutcome.ReviewRequired))
            {
                return false;
            }
            ModCatalogSnapshot candidate = pending ?? catalog;
            ModPackage? selected = candidate.Packages.FirstOrDefault(package => package.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            return (packageId is null || selected?.Status == ModEffectiveStatus.Active && LanguageDependenciesActive(selected, candidate.Packages))
                && (deactivatedPackageId is null || string.Equals(packageId, deactivatedPackageId, StringComparison.OrdinalIgnoreCase)
                    || candidate.Packages.All(package => !package.Id.Equals(deactivatedPackageId, StringComparison.OrdinalIgnoreCase) || package.Status != ModEffectiveStatus.Active));
        }
    }

    private bool LanguageDependenciesActive(ModPackage selected, IReadOnlyList<ModPackage> packages)
    {
        try
        {
            return LanguagePackages.ResolveInstalledDependencies(selected, packages, new(edition, language)).All(package => package.Status == ModEffectiveStatus.Active);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
