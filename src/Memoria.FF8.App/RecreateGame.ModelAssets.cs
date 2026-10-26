using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private IEnumerator<ModelAssetDiscoveryItem>? browserAssetReader;
    private readonly Dictionary<string, ModelAssetDiscoveryItem[]> browserDiscoveryCache = new(StringComparer.Ordinal);
    private readonly List<ModelAssetDiscoveryItem> browserPendingDiscovery = [];
    private string? browserDiscoveryKey;
    private readonly Dictionary<string, ModelAssetDiscoveryItem> browserAssets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EditableModel> browserAssetModels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> browserAssetFingerprints = new(StringComparer.Ordinal);
    private string? browserAssetIdentity;
    private string? browserAssemblyIdentity;
    private bool browserAssetsComplete;
    private bool previousBrowserSpace;
    private ModelAssetImportInventory? browserImport;

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
        ModelAssetSelection selection = browserImport is null ? BrowserAssetSelection(preview: true) : BrowserImportSelection(preview: true);
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
        browserAssetFingerprints.Clear();
        browserAssetIdentity = entry.Id + "\0" + variant.Id;
        browserAssemblyIdentity = null;
        browserAssetsComplete = false;
        browserImport = null;
        modelBrowser!.ResetAssets();
        browserDiscoveryKey = resourceHost!.Resources.Source.Edition + "\0" + browserAssetIdentity + "\0" + modelBrowserModGeneration;
        browserPendingDiscovery.Clear();
        browserAssetReader = browserDiscoveryCache.TryGetValue(browserDiscoveryKey, out ModelAssetDiscoveryItem[]? cached)
            ? ((IEnumerable<ModelAssetDiscoveryItem>)cached).GetEnumerator()
            : modelBrowserAssets!.DiscoverModelAssets(modelCatalog[entry.Id]).GetEnumerator();
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

    private EditableModel ReadDiscoveredModel(ModelAssetDiscoveryItem item)
    {
        string key = item.Kind == ModelAssetDiscoveryKind.Clip ? ClipModelTarget(item.Id) + "/" + item.Bank : item.Id;
        if (browserAssetModels.TryGetValue(key, out EditableModel? cached))
        {
            return cached;
        }
        EditableModel original = item.Load?.Invoke() ?? throw new NotSupportedException(item.Error ?? "This source has no independently decodable model.");
        string target = item.Kind == ModelAssetDiscoveryKind.Clip ? ClipModelTarget(item.Id) : item.Id;
        EditableModel model = ResolveBrowserModel(target, original, modelBrowser!.CurrentVariant!, item.Bank);
        browserAssetModels.Add(key, model);
        return model;
    }

    private EditableModel ResolveBrowserModel(string target, EditableModel original, ModelBrowserVariant variant, string bank = "embedded")
    {
        if (variant.Id == "original")
        {
            return original;
        }
        return (variant.Id == "resolved" ? resourceHost!.ModelMods : browserInstalledModelMods).Resolve(target, original, variant.Id == "resolved" ? null : variant.Id, bank);
    }

    private string BrowserAssetSource(ModelAssetDiscoveryItem item)
    {
        ModelBrowserVariant? variant = modelBrowser?.CurrentVariant;
        if (variant is null || variant.Id == "original")
        {
            return "Original";
        }
        string target = item.Kind == ModelAssetDiscoveryKind.Clip ? ClipModelTarget(item.Id) : item.Id;
        IEnumerable<ModelModVariant> variants = (variant.Id == "resolved" ? resourceHost!.ModelMods : browserInstalledModelMods).Variants;
        ModelModVariant? provider = variants.FirstOrDefault(candidate => candidate.ModelId.Equals(target, StringComparison.OrdinalIgnoreCase) && candidate.Bank == item.Bank && (variant.Id == "resolved" || candidate.PackageId.Equals(variant.Id, StringComparison.OrdinalIgnoreCase)) && (item.Kind != ModelAssetDiscoveryKind.Clip ? candidate.ClipSlot is null : !candidate.GeometryOnly && (candidate.ClipSlot is null || candidate.ClipSlot == item.ClipIndex)));
        return provider?.PackageId ?? "Original (fallback)";
    }

    private string BrowserFingerprint(ModelAssetDiscoveryItem item, EditableModel model)
    {
        string key = item.Kind == ModelAssetDiscoveryKind.Clip ? ClipModelTarget(item.Id) + "/" + item.Bank : item.Id;
        if (!browserAssetFingerprints.TryGetValue(key, out string? fingerprint))
        {
            fingerprint = Convert.ToHexString(SHA256.HashData(ModelExchange.ExportGlb(model))).ToLowerInvariant();
            browserAssetFingerprints.Add(key, fingerprint);
        }
        return fingerprint;
    }

    private ModelAssetSelection BrowserAssetSelection(bool preview = false)
    {
        ModelBrowserController browser = modelBrowser!;
        Dictionary<string, ModelAssetObject> objects = new(StringComparer.Ordinal);
        List<ModelAssetClip> clips = [];
        ModelAssetDiscoveryItem? activeClip = browser.ActiveClip is { } active ? browserAssets.GetValueOrDefault(active) : null;
        string? activeWeapon = browser.ActiveObject is { } activeObject ? browserAssets.GetValueOrDefault(activeObject)?.AlternativeGroup is not null ? activeObject : null : null;
        if (activeWeapon is null)
        {
            activeWeapon = browser.Assets.FirstOrDefault(row => !row.IsClip && row.Supported && browserAssets.GetValueOrDefault(row.Id)?.AlternativeGroup is not null)?.Id;
        }
        bool IncludeObject(ModelAssetDiscoveryItem item) => preview ? item.Id == browser.Selected?.Id || item.Id == browser.ActiveObject || item.Id == activeWeapon : browser.SelectedAssets.Contains(item.Id);
        foreach (ModelAssetDiscoveryItem item in browserAssets.Values.Where(item => item.Kind == ModelAssetDiscoveryKind.Object && IncludeObject(item)))
        {
            AddObject(item, true);
        }
        IEnumerable<ModelAssetDiscoveryItem> selectedClips = browserAssets.Values.Where(item => item.Kind == ModelAssetDiscoveryKind.Clip && (preview ? activeClip is not null && item.GroupId == activeClip.GroupId : browser.SelectedAssets.Contains(item.Id)));
        foreach (ModelAssetDiscoveryItem item in selectedClips)
        {
            string target = ClipModelTarget(item.Id);
            if (preview && browserAssets.Values.FirstOrDefault(asset => asset.Kind == ModelAssetDiscoveryKind.Object && asset.Id == target)?.AlternativeGroup is not null && target != activeWeapon)
            {
                continue;
            }
            EditableModel source = ReadDiscoveredModel(item);
            if ((uint)item.ClipIndex >= source.Animations.Count)
            {
                throw new InvalidDataException("The selected clip is absent from its source bank.");
            }
            string rig = item.RigOwnerId ?? target;
            if (!objects.ContainsKey(target))
            {
                objects.Add(target, new(target, BrowserAssetSource(item), BrowserFingerprint(item, source), rig, source, false));
            }
            clips.Add(new(item.Id, target, rig, item.GroupId ?? item.Id, BrowserAssetSource(item), BrowserFingerprint(item, source), source.Animations[item.ClipIndex], (int)MathF.Round(source.Animations[item.ClipIndex].Duration * source.NativeFramesPerSecond) + 1, 1f / source.NativeFramesPerSecond));
        }
        if (preview && activeClip is not null && objects.Values.Where(item => item.GeometrySelected && item.Model.Bones.Any(bone => bone.IsJoint)).Select(item => item.RigId).Distinct().Any(rig => !clips.Any(clip => clip.RigId == rig)))
        {
            browser.SetMessage(() => LocalizeAuthored("model-browser.clip.missing-pair", "Missing paired clip component. Its rig remains in the base pose."));
        }
        return new(objects.Values.ToArray(), clips) { Edition = resourceHost!.Resources.Source.Edition.ToString() };

        void AddObject(ModelAssetDiscoveryItem item, bool geometry)
        {
            EditableModel source = ReadDiscoveredModel(item);
            string rig = item.RigOwnerId ?? (item.UsesOwnerSkeleton ? item.OwnerId : item.Id);
            objects[item.Id] = new(item.Id, BrowserAssetSource(item), BrowserFingerprint(item, source), rig, source, geometry, item.AlternativeGroup, item.ReplacementPart);
        }
    }

    private void CompleteBrowserExport()
    {
        try
        {
            if (!browserAssetsComplete && !ModelBrowserDialogs.Confirm(Window.Handle, LocalizeAuthored("model.export.incomplete-discovery", "Discovery has not finished. Export only the currently discovered selection?"), LocalizeAuthored("model.export.selection", "Export selection"), LocalizeAuthored("common.continue-question", "Continue?"), LocalizeUiText))
            {
                return;
            }
            ModelAssetSelection selection = BrowserAssetSelection();
            if (selection.Objects.All(item => !item.GeometrySelected) && selection.Clips.Count == 0)
            {
                throw new InvalidDataException("Select geometry or at least one clip component.");
            }
            string? path = ModelBrowserDialogs.Export(Window.Handle, modelBrowser!.Selected!.Id.Replace('/', '_'), LocalizeUiText);
            if (path is null)
            {
                return;
            }
            ModelAssetExchange.ExportGlbAtomic(path, selection);
            modelBrowser.SetMessage(() => LocalizeAuthored("model-browser.export.completed", "Selection exported to one self-contained GLB."));
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            modelBrowser?.SetMessage(exception.Message);
        }
        finally
        {
            keyboard.SuppressHeldNameInput();
            modelBrowserDialogInputDrain = true;
        }
    }
}
