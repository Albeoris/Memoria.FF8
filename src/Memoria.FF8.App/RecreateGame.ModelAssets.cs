using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private const string RetainedModelFingerprintPrefix = "retained-glb:";
    private IEnumerator<ModelAssetDiscoveryItem>? browserAssetReader;
    private readonly Dictionary<string, ModelAssetDiscoveryItem[]> browserDiscoveryCache = new(StringComparer.Ordinal);
    private readonly List<ModelAssetDiscoveryItem> browserPendingDiscovery = [];
    private string? browserDiscoveryKey;
    private readonly Dictionary<string, ModelAssetDiscoveryItem> browserAssets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EditableModel> browserAssetModels = new(StringComparer.Ordinal);
    private string? browserAssetIdentity;
    private string? browserAssemblyIdentity;
    private bool browserAssetsComplete;
    private bool previousBrowserSpace;
    private ModelAssetImportInventory? browserImport;
    private Task? browserExportWork;
    private CancellationTokenSource? browserExportCancellation;

    private void UpdateBrowserAssemblyPreview()
    {
        if (modelBrowser?.AssetsOpen != true)
        {
            return;
        }
        string identity = browserAssetIdentity + "\0" + modelBrowser.ActiveObject + "\0" + modelBrowser.ActiveClip + "\0" + (browserImport is null ? "export" : string.Join("|", modelBrowser.SelectedAssets.Order()));
        if (browserAssemblyIdentity == identity)
        {
            return;
        }
        ModelAssetSelection selection = browserImport is null ? ReadBrowserPreviewSelection() : BrowserImportSelection(preview: true);
        if (selection.Objects.Count == 0 || !selection.Objects.Any(item => item.GeometrySelected))
        {
            return;
        }
        modelPreview = ModelAssetExchange.Compose(selection);
        browserAssemblyIdentity = identity;
        float duration = modelPreview.Animations.Count == 0 ? 0 : modelPreview.Animations[0].Duration;
        modelBrowser.SetClipTiming(duration, selection.Clips.FirstOrDefault()?.SecondsPerFrame ?? 1f / ModelExchange.FramesPerSecond);
    }

    private void ResetBrowserAssets(ModelBrowserEntry entry, ModelBrowserVariant variant)
    {
        browserAssetReader?.Dispose();
        browserAssets.Clear();
        browserAssetModels.Clear();
        browserAssetIdentity = entry.Id + "\0" + variant.Id;
        browserAssemblyIdentity = null;
        browserAssetsComplete = false;
        browserImport = null;
        modelBrowser!.ResetAssets();
        browserDiscoveryKey = resourceHost!.Resources.Source.Edition + "\0" + browserAssetIdentity + "\0" + modelBrowserModGeneration;
        browserPendingDiscovery.Clear();
        browserAssetReader = browserDiscoveryCache.TryGetValue(browserDiscoveryKey, out ModelAssetDiscoveryItem[]? cached)
            ? ((IEnumerable<ModelAssetDiscoveryItem>)cached).GetEnumerator()
            : modelBrowserAssets!.DiscoverModelAssetsForExchange(modelCatalog[entry.Id]).GetEnumerator();
    }

    private void ReadMoreBrowserAssets()
    {
        if (browserAssetReader is null || modelBrowser is null)
        {
            return;
        }
        try
        {
            // One discovery unit per update permits cancellation between resource headers.
            if (!browserAssetReader.MoveNext())
            {
                browserAssetReader.Dispose();
                browserAssetReader = null;
                browserAssetsComplete = true;
                return;
            }
            ModelAssetDiscoveryItem item = browserAssetReader.Current;
            if (item.Kind != ModelAssetDiscoveryKind.Progress)
            {
                browserPendingDiscovery.Add(item);
            }
            if (item.Kind is ModelAssetDiscoveryKind.Progress or ModelAssetDiscoveryKind.Coverage)
            {
                modelBrowser.SetDiscoveryStatus(() => ModelDiscoveryText(item));
                browserAssetsComplete = item.Kind == ModelAssetDiscoveryKind.Coverage;
                if (browserAssetsComplete && browserDiscoveryKey is not null)
                {
                    browserDiscoveryCache[browserDiscoveryKey] = browserPendingDiscovery.ToArray();
                    if (browserDiscoveryCache.Count > 4)
                    {
                        browserDiscoveryCache.Remove(browserDiscoveryCache.Keys.First(key => key != browserDiscoveryKey));
                    }
                }
                return;
            }
            browserAssets[item.Id] = item;
            browserAssemblyIdentity = null;
            bool clip = item.Kind == ModelAssetDiscoveryKind.Clip;
            string group = clip ? item.GroupId ?? item.Bank : item.AlternativeGroup ?? "Objects";
            string actualSource = BrowserAssetSource(item);
            string label = clip && item.ResourcePath.StartsWith("battle/", StringComparison.OrdinalIgnoreCase) ? (browserAssets.GetValueOrDefault(ClipModelTarget(item.Id))?.Name ?? Path.GetFileName(ClipModelTarget(item.Id))) + " · " + item.Name : item.Name;
            bool selected = clip ? item.OwnerId == modelBrowser.Selected?.Id || ClipModelTarget(item.Id) == modelBrowser.Selected?.Id || modelBrowser.SelectedAssets.Contains(ClipModelTarget(item.Id)) || browserAssets.GetValueOrDefault(modelBrowser.Selected?.Id ?? "") is { UsesOwnerSkeleton: true } selectedWeapon && selectedWeapon.RigOwnerId == ClipModelTarget(item.Id) : item.Id == modelBrowser.Selected?.Id || item.AlternativeGroup is not null && !browserAssets.Values.Any(other => other.Id != item.Id && other.AlternativeGroup == item.AlternativeGroup);
            modelBrowser.AddAsset(new(item.Id, label, group, clip, item.Kind != ModelAssetDiscoveryKind.Diagnostic && item.Error is null, item.Error ?? item.Provenance + " · Source: " + actualSource + " · Locked dependency: rig, materials, textures", selected) { RigId = item.RigOwnerId ?? (item.Kind == ModelAssetDiscoveryKind.Clip ? ClipModelTarget(item.Id) : item.Id), ReadGroup = () => clip ? item.GroupId ?? item.Bank : item.AlternativeGroup ?? LocalizeAuthored("model.group.objects", "Objects"), ReadDetail = () => item.Error ?? FormatAuthored("model.asset.detail", "{0} · Source: {1} · Locked dependency: rig, materials, textures", item.Provenance, actualSource) });
            if (item.Kind == ModelAssetDiscoveryKind.Diagnostic)
            {
                modelBrowser.SetDiscoveryStatus(() => FormatAuthored("model-browser.discovery.incomplete", "Incomplete: {0}", item.Name));
            }
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            browserAssetReader?.Dispose();
            browserAssetReader = null;
            browserAssetsComplete = false;
            modelBrowser.SetDiscoveryStatus(() => FormatAuthored("model-browser.discovery.incomplete", "Incomplete: {0}", exception.Message));
        }
    }

    private string ModelDiscoveryText(ModelAssetDiscoveryItem item) => item.Progress?.Kind switch
    {
        ModelAssetProgressKind.Discovering => LocalizeAuthored("model.discovery.initial", "Discovering"),
        ModelAssetProgressKind.FieldReferences => FormatAuthored("model.discovery.field-references", "Discovering field references: {0}", item.Progress?.Subject),
        ModelAssetProgressKind.Incomplete => LocalizeAuthored("model.discovery.incomplete", "Incomplete: see source diagnostics"),
        ModelAssetProgressKind.Complete => LocalizeAuthored("model.discovery.complete", "Complete for supported sources"),
        _ => item.Name
    };

    private static string ClipModelTarget(string address)
    {
        int marker = address.IndexOf("/clips/", StringComparison.Ordinal);
        return marker > 0 ? address[..marker] : throw new InvalidDataException("A clip has no logical replacement target.");
    }

    private static string BrowserModelKey(ModelAssetDiscoveryItem item)
    {
        return item.Kind == ModelAssetDiscoveryKind.Clip ? ClipModelTarget(item.Id) + "/" + item.Bank : item.Id;
    }

    private static EditableModel ReadDiscoveredModel(ModelAssetDiscoveryItem item, string variant, ModelMods providers, Dictionary<string, EditableModel> models)
    {
        string key = BrowserModelKey(item);
        if (models.TryGetValue(key, out EditableModel? cached))
        {
            return cached;
        }

        EditableModel original = item.Load?.Invoke() ?? throw new NotSupportedException(item.Error ?? "This source has no independently decodable model.");
        string target = item.Kind == ModelAssetDiscoveryKind.Clip ? ClipModelTarget(item.Id) : item.Id;
        EditableModel model = variant == "original" ? original : providers.Resolve(target, original, variant == "resolved" ? null : variant, item.Bank);
        models.Add(key, model);
        return model;
    }

    private ModelMods BrowserModelProviders(string variant)
    {
        return variant switch
        {
            "original" => ModelMods.Empty,
            "resolved" => resourceHost!.ModelMods,
            _ => browserInstalledModelMods
        };
    }

    private EditableModel ResolveBrowserModel(string target, EditableModel original, ModelBrowserVariant variant)
    {
        return variant.Id == "original" ? original : BrowserModelProviders(variant.Id).Resolve(target, original, variant.Id == "resolved" ? null : variant.Id);
    }

    private string BrowserAssetSource(ModelAssetDiscoveryItem item)
    {
        string variant = modelBrowser?.CurrentVariant?.Id ?? "original";
        return BrowserAssetSource(item, variant, BrowserModelProviders(variant));
    }

    private static string BrowserAssetSource(ModelAssetDiscoveryItem item, string variant, ModelMods providers)
    {
        if (variant == "original")
        {
            return "Original";
        }

        string target = item.Kind == ModelAssetDiscoveryKind.Clip ? ClipModelTarget(item.Id) : item.Id;
        ModelModVariant? provider = providers.Variants.FirstOrDefault(candidate => candidate.ModelId.Equals(target, StringComparison.OrdinalIgnoreCase) && candidate.Bank == item.Bank && (variant == "resolved" || candidate.PackageId.Equals(variant, StringComparison.OrdinalIgnoreCase)) && (item.Kind != ModelAssetDiscoveryKind.Clip ? candidate.ClipSlot is null : !candidate.GeometryOnly && (candidate.ClipSlot is null || candidate.ClipSlot == item.ClipIndex)));
        return provider?.PackageId ?? "Original (fallback)";
    }

    private ModelAssetSelection ReadBrowserPreviewSelection()
    {
        ModelBrowserController browser = modelBrowser!;
        string variant = browser.CurrentVariant!.Id;
        ModelAssetDiscoveryItem? activeClip = browser.ActiveClip is { } active ? browserAssets.GetValueOrDefault(active) : null;
        string? activeWeapon = browser.ActiveObject is { } activeObject && browserAssets.GetValueOrDefault(activeObject)?.AlternativeGroup is not null ? activeObject : null;
        activeWeapon ??= browser.Assets.FirstOrDefault(row => !row.IsClip && row.Supported && browserAssets.GetValueOrDefault(row.Id)?.AlternativeGroup is not null)?.Id;
        IEnumerable<ModelAssetDiscoveryItem> objects = browserAssets.Values.Where(item => item.Kind == ModelAssetDiscoveryKind.Object && (item.Id == browser.Selected?.Id || item.Id == browser.ActiveObject || item.Id == activeWeapon));
        IEnumerable<ModelAssetDiscoveryItem> clips = browserAssets.Values.Where(item =>
        {
            if (item.Kind != ModelAssetDiscoveryKind.Clip || activeClip is null || item.GroupId != activeClip.GroupId)
            {
                return false;
            }
            string target = ClipModelTarget(item.Id);
            return browserAssets.GetValueOrDefault(target)?.AlternativeGroup is null || target == activeWeapon;
        });
        ModelAssetSelection selection = ReadBrowserAssetSelection(objects.Concat(clips), variant, BrowserModelProviders(variant), resourceHost!.Resources.Source.Edition.ToString(), browserAssetModels, includeFingerprints: false, CancellationToken.None);
        if (activeClip is not null && selection.Objects.Where(item => item.GeometrySelected && item.Model.Bones.Any(bone => bone.IsJoint)).Select(item => item.RigId).Distinct().Any(rig => !selection.Clips.Any(clip => clip.RigId == rig)))
        {
            browser.SetMessage(() => LocalizeAuthored("model-browser.clip.missing-pair", "Missing paired clip component. Its rig remains in the base pose."));
        }
        return selection;
    }

    private static ModelAssetSelection ReadBrowserAssetSelection(IEnumerable<ModelAssetDiscoveryItem> items, string variant, ModelMods providers, string edition, Dictionary<string, EditableModel> models, bool includeFingerprints, CancellationToken cancellationToken)
    {
        Dictionary<string, ModelAssetObject> objects = new(StringComparer.Ordinal);
        List<ModelAssetClip> clips = [];
        Dictionary<string, string> fingerprints = new(StringComparer.Ordinal);
        foreach (ModelAssetDiscoveryItem item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EditableModel source = ReadDiscoveredModel(item, variant, providers, models);
            string sourceVariant = BrowserAssetSource(item, variant, providers);
            string fingerprint = "";
            if (includeFingerprints)
            {
                string key = BrowserModelKey(item);
                if (!fingerprints.TryGetValue(key, out string? cachedFingerprint))
                {
                    cachedFingerprint = BrowserSourceFingerprint(source, retainedMetadata: true);
                    fingerprints.Add(key, cachedFingerprint);
                }
                fingerprint = cachedFingerprint;
            }

            if (item.Kind == ModelAssetDiscoveryKind.Object)
            {
                string objectRig = item.RigOwnerId ?? (item.UsesOwnerSkeleton ? item.OwnerId : item.Id);
                objects[item.Id] = new(item.Id, sourceVariant, fingerprint, objectRig, source with { Animations = [] }, true, item.AlternativeGroup, item.ReplacementPart);
                continue;
            }

            string target = ClipModelTarget(item.Id);
            if ((uint)item.ClipIndex >= source.Animations.Count)
            {
                throw new InvalidDataException("The selected clip is absent from its source bank.");
            }
            string rig = item.RigOwnerId ?? target;
            if (!objects.ContainsKey(target))
            {
                objects.Add(target, new(target, sourceVariant, fingerprint, rig, source with { Meshes = [], Textures = [], Animations = [] }, false));
            }
            EditableAnimation animation = source.Animations[item.ClipIndex];
            clips.Add(new(item.Id, target, rig, item.GroupId ?? item.Id, sourceVariant, fingerprint, animation, (int)MathF.Round(animation.Duration * source.NativeFramesPerSecond) + 1, 1f / source.NativeFramesPerSecond));
        }
        return new(objects.Values.ToArray(), clips) { Edition = edition };
    }

    internal static string BrowserSourceFingerprint(EditableModel source, bool retainedMetadata)
    {
        string prefix = "";
        if (retainedMetadata)
        {
            source = source with { NativeMetadata = ModelExchange.RetainedNativeMetadata(source.NativeMetadata) };
            prefix = RetainedModelFingerprintPrefix;
        }
        return prefix + Convert.ToHexString(SHA256.HashData(ModelExchange.ExportGlb(source))).ToLowerInvariant();
    }

    internal static Task ExportBrowserSelectionAsync(string path, ModelAssetDiscoveryItem[] selection, string variant, ModelMods providers, string edition, Dictionary<string, EditableModel> models, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            ModelAssetSelection assets = ReadBrowserAssetSelection(selection, variant, providers, edition, models, includeFingerprints: true, cancellationToken);
            models.Clear();
            ModelAssetExchange.ExportGlbAtomic(path, assets, cancellationToken);
        }, cancellationToken);
    }

    private async void CompleteBrowserExport()
    {
        if (browserExportCancellation is not null)
        {
            return;
        }

        ModelBrowserController browser = modelBrowser!;
        using CancellationTokenSource cancellation = new();
        browserExportCancellation = cancellation;
        try
        {
            if (!browserAssetsComplete && !await ModelBrowserDialogs.ConfirmAsync(Window.Handle, LocalizeAuthored("model.export.incomplete-discovery", "Discovery has not finished. Export only the currently discovered selection?"), LocalizeAuthored("model.export.selection", "Export selection"), LocalizeAuthored("common.continue-question", "Continue?"), LocalizeUiText))
            {
                return;
            }
            if (!ReferenceEquals(modelBrowser, browser) || cancellation.IsCancellationRequested)
            {
                return;
            }

            ModelAssetDiscoveryItem[] selection = browserAssets.Values
                .Where(item => (item.Kind is ModelAssetDiscoveryKind.Object or ModelAssetDiscoveryKind.Clip) && browser.SelectedAssets.Contains(item.Id))
                .OrderBy(item => item.Kind == ModelAssetDiscoveryKind.Clip)
                .ToArray();
            if (selection.Length == 0)
            {
                throw new InvalidDataException("Select geometry or at least one clip component.");
            }
            string variant = browser.CurrentVariant!.Id;
            ModelMods providers = BrowserModelProviders(variant);
            string edition = resourceHost!.Resources.Source.Edition.ToString();
            string? path = await ModelBrowserDialogs.ExportAsync(Window.Handle, browser.Selected!.Id.Replace('/', '_'), globalSettings, LocalizeUiText, cancellation.Token);
            if (path is null || cancellation.IsCancellationRequested)
            {
                return;
            }

            Dictionary<string, EditableModel> models = new(browserAssetModels, StringComparer.Ordinal);
            browser.SetMessage(() => LocalizeAuthored("model-browser.export.in-progress", "Exporting GLB…"));
            Console.WriteLine($"model-export started assets={selection.Length}");
            browserExportWork = ExportBrowserSelectionAsync(path, selection, variant, providers, edition, models, cancellation.Token);
            await browserExportWork;
            Console.WriteLine("model-export completed");
            browser.SetMessage(() => LocalizeAuthored("model-browser.export.completed", "Selection exported to one self-contained GLB."));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            browser.SetMessage(exception.Message);
        }
        finally
        {
            browserExportWork = null;
            browserExportCancellation = null;
            keyboard.SuppressHeldNameInput();
            modelBrowserDialogInputDrain = true;
        }
    }
}
