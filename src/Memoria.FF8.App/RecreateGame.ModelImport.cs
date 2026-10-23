using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly Dictionary<string, EditableModel> browserImportOriginals = new(StringComparer.Ordinal);
    private readonly List<string> browserImportWarnings = [];

    private void OpenBrowserImport(ModelBrowserEntry entry, ModelBrowserVariant variant)
    {
        try
        {
            string? path = ModelBrowserDialogs.Import(Window.Handle);
            if (path is null)
            {
                return;
            }
            if (new FileInfo(path).Length > 256 * 1024 * 1024)
            {
                throw new InvalidDataException("The GLB must be no larger than 256 MiB.");
            }
            byte[] bytes = File.ReadAllBytes(path);
            ModelAssetImportInventory inventory = ModelAssetExchange.InspectGlb(bytes);
            string edition = resourceHost!.Resources.Source.Edition.ToString();
            if (inventory.Edition != "unspecified" && inventory.Edition != edition)
            {
                throw new InvalidDataException($"This GLB targets {inventory.Edition}; the selected installation is {edition}.");
            }
            if (inventory.Objects.Count == 0)
            {
                ValidateSingleUnidentifiedModelMapping(bytes);
                if (!ModelBrowserDialogs.Confirm(Window.Handle, "The GLB has no preserved logical model identity. Explicitly map its single model and clip slots to " + entry.Id + "?", "Map GLB target", "Use this target?"))
                {
                    return;
                }
                EditableModel original = ReadBrowserModel(entry, variant);
                ModelExchangeImportResult legacy = ModelExchange.ImportGlb(bytes, original);
                EditableModel model = legacy.Model;
                ModelAssetObject item = new(entry.Id, "mapped", "", entry.Id, model);
                ModelAssetClip[] clips = model.Animations.Select((animation, index) => new ModelAssetClip(ModelAssetDiscoveryBindings.ClipTarget(entry.Id, "embedded", index), entry.Id, entry.Id, "mapped/" + index, "mapped", "", animation, animation.Channels.Select(channel => channel.Times.Length).DefaultIfEmpty(1).Max(), 1f / original.NativeFramesPerSecond)).ToArray();
                inventory = new([item], clips, legacy.Warnings) { Edition = edition };
            }
            browserAssetReader?.Dispose();
            browserAssetReader = null;
            browserAssetsComplete = true;
            browserImportOriginals.Clear();
            browserImportWarnings.Clear();
            browserImportWarnings.AddRange(inventory.Warnings);
            browserImport = inventory;
            modelPreviewIdentity = entry.Id + "\0" + variant.Id;
            modelPreview = ReadBrowserModel(entry, variant);
            browserAssemblyIdentity = null;
            modelBrowser!.ResetAssets();
            foreach (ModelAssetObject item in inventory.Objects.Where(item => item.GeometrySelected))
            {
                AddImportRow("object:" + item.Target, item.Model.Name, item.AlternativeGroup ?? "Objects", false, item.Target, "embedded", item.Model, fingerprint: item.Fingerprint);
            }
            foreach (ModelAssetClip clip in inventory.Clips)
            {
                ParseClipAddress(clip.Target, out string target, out string bank, out int slot);
                ModelAssetObject owner = inventory.Objects.Single(item => item.Target == clip.ModelTarget);
                AddImportRow(clip.Target, clip.Animation.Name, clip.GroupId, true, target, bank, owner.Model, slot, clip.Fingerprint, clip.Animation);
            }
            foreach (string warning in inventory.Warnings.Where(warning => warning.Contains("mapping", StringComparison.OrdinalIgnoreCase)))
            {
                modelBrowser.AddAsset(new("unmapped:" + warning, "Needs mapping", "Unresolved content", false, false, warning, false));
            }
            modelBrowser.SetDiscoveryStatus("Import inventory: actual surviving objects and channels");
            modelBrowser.OpenAssetPanel(CompleteBrowserImport, importing: true);
            modelBrowser.SetHints("Tab: preview / list · Arrows: navigate · Confirm / Space: checkbox · Click label: preview\nUnchecked leaves unchanged · Rigs are validation context · Back: cancel without installation");
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

    internal static void ValidateSingleUnidentifiedModelMapping(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 20 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46546C67 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]) != 0x4E4F534A)
        {
            throw new InvalidDataException("The GLB has no valid JSON directory for explicit mapping.");
        }
        int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]);
        if (length < 0 || length > bytes.Length - 20)
        {
            throw new InvalidDataException("The GLB JSON directory exceeds its container.");
        }
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(bytes.Slice(20, length).ToArray());
        System.Text.Json.JsonElement root = document.RootElement;
        int objects = root.TryGetProperty("nodes", out System.Text.Json.JsonElement nodes) ? nodes.EnumerateArray().Count(node => node.TryGetProperty("mesh", out _)) : 0;
        int skins = root.TryGetProperty("skins", out System.Text.Json.JsonElement skinArray) ? skinArray.GetArrayLength() : 0;
        if (objects > 1 || skins > 1 || objects == 0 && skins != 1)
        {
            throw new NotSupportedException("Several objects or rigs have no logical model identities. Restore their Memoria identities or export one model for explicit mapping; mapping the entire assembly to the selected catalog model is unsupported.");
        }
    }

    private void AddImportRow(string id, string name, string group, bool clip, string target, string bank, EditableModel imported, int slot = -1, string fingerprint = "", EditableAnimation? animation = null)
    {
        string status = "Matched: " + target + (clip ? $" / {bank} / {slot}" : " / geometry");
        bool supported = true;
        try
        {
            EditableModel original = ReadImportOriginal(target, bank);
            int before = original.Bones.Count(bone => bone.IsJoint);
            int after = imported.Bones.Count(bone => bone.IsJoint);
            if (before != after)
            {
                browserImportWarnings.Add($"{target}: native joint count changed from {before} to {after}.");
            }
            _ = ModelMods.Preview(original, new(target, imported, !clip, clip && animation is not null ? [new(bank, slot, animation)] : []));
            if (fingerprint.Length != 0 && !Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ModelExchange.ExportGlb(original))).Equals(fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                browserImportWarnings.Add($"{target} / {bank}: source revision differs from the exported provider; retained target content is resolved from the current mod stack.");
                status = "Warning: source revision differs · " + target;
            }
            if (clip && animation is not null && animation.Channels.Any(channel => channel.Interpolation != "LINEAR" || channel.Times.Any(time => MathF.Abs(time * original.NativeFramesPerSecond - MathF.Round(time * original.NativeFramesPerSecond)) > 0.0001f)))
            {
                browserImportWarnings.Add($"{target} / {bank} / {slot}: edited interpolation or keys between native frames will be sampled on the native frame grid; between-frame curves are not preserved by the native clip format.");
            }
            if (clip && (uint)slot >= original.Animations.Count)
            {
                throw new NotSupportedException("The selected native clip slot does not exist. Adding gameplay slots is unsupported.");
            }
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            supported = false;
            status = "Unsupported: " + target + " · " + exception.Message;
        }
        modelBrowser!.AddAsset(new(id, name, group, clip, supported, status + " · Scope: all consumers of this exact target slot · Retained target rig is required", supported) { RigId = target });
    }

    private EditableModel ReadImportOriginal(string target, string bank)
    {
        string key = target + "/" + bank;
        if (browserImportOriginals.TryGetValue(key, out EditableModel? model))
        {
            return model;
        }
        if (!ModelMods.IsModelId(target))
        {
            throw new NotSupportedException("The logical target is unavailable and needs explicit catalog mapping.");
        }
        ModelCatalogEntry entry = modelCatalog.GetValueOrDefault(target) ?? CatalogEntryForTarget(target);
        if (bank == "embedded")
        {
            model = modelBrowserAssets!.LoadCatalogModel(entry);
        }
        else
        {
            ModelAssetDiscoveryItem? item = null;
            string? diagnostic = null;
            foreach (ModelAssetDiscoveryItem candidate in modelBrowserAssets!.DiscoverModelAssets(entry))
            {
                if (candidate.Kind == ModelAssetDiscoveryKind.Clip && candidate.Bank == bank && ClipModelTarget(candidate.Id) == target)
                {
                    item = candidate;
                    break;
                }
                if (candidate.Kind == ModelAssetDiscoveryKind.Diagnostic && candidate.Id == entry.Id + "/diagnostic/" + bank)
                {
                    diagnostic = candidate.Error;
                }
            }
            if (item is null)
            {
                throw new NotSupportedException(diagnostic ?? "No loader binds this external bank to the target.");
            }
            model = item.Load!();
        }
        model = resourceHost!.ModelMods.Resolve(target, model, bank: bank);
        browserImportOriginals.Add(key, model);
        return model;
    }

    private static ModelCatalogEntry CatalogEntryForTarget(string target)
    {
        string[] parts = target.Split('/');
        return parts[0] switch
        {
            "field" => new(target, target, "Field", $"field/{parts[1]}/chara.one", target, ModelIndex: int.Parse(parts[2])),
            "world" when parts.Length == 2 => new(target, target, "World character", "world/chara.one", target, ModelIndex: int.Parse(parts[1])),
            "world" => new(target, target, "World static", "world/wmsetus.obj", target, ModelIndex: int.Parse(parts[2])),
            "battle" => new(target, target, parts[1].StartsWith("a0stg", StringComparison.Ordinal) ? "Battle stage" : parts[1].Length > 2 && parts[1][0] == 'd' && parts[1][2] == 'w' ? "Battle weapon" : "Battle model", target, target),
            _ => throw new NotSupportedException("Unknown model domain.")
        };
    }

    private static void ParseClipAddress(string address, out string target, out string bank, out int slot)
    {
        target = ClipModelTarget(address);
        string suffix = address[(target.Length + 7)..];
        int separator = suffix.LastIndexOf('/');
        if (separator < 1 || !int.TryParse(suffix[(separator + 1)..], out slot) || slot < 0)
        {
            throw new InvalidDataException("The clip replacement address is invalid.");
        }
        bank = suffix[..separator];
        if (!ModelMods.IsBank(bank))
        {
            throw new InvalidDataException("The clip bank address is unsupported.");
        }
    }

    private ModelAssetSelection BrowserImportSelection(bool preview)
    {
        ModelAssetImportInventory inventory = browserImport!;
        List<ModelAssetObject> objects = [];
        List<ModelAssetClip> clips = [];
        string? activeAlternative = inventory.Objects.FirstOrDefault(item => "object:" + item.Target == modelBrowser!.ActiveObject && item.AlternativeGroup is not null)?.Target ?? inventory.Objects.FirstOrDefault(item => item.AlternativeGroup is not null)?.Target;
        string? activeGroup = inventory.Clips.FirstOrDefault(clip => clip.Target == modelBrowser!.ActiveClip)?.GroupId;
        foreach (ModelAssetObject item in inventory.Objects)
        {
            if (preview && item.AlternativeGroup is not null && item.Target != activeAlternative)
            {
                continue;
            }
            bool geometry = modelBrowser!.SelectedAssets.Contains("object:" + item.Target);
            ModelAssetClip[] chosen = inventory.Clips.Where(clip => clip.ModelTarget == item.Target && (preview || modelBrowser.SelectedAssets.Contains(clip.Target)) && (!preview || activeGroup is null || clip.GroupId == activeGroup)).ToArray();
            if (!preview && !geometry && chosen.Length == 0)
            {
                continue;
            }
            if (preview && !geometry && chosen.Length == 0 && !modelBrowser.Assets.Any(row => row.Supported && (row.Id == "object:" + item.Target || inventory.Clips.Any(clip => clip.ModelTarget == item.Target && clip.Target == row.Id))))
            {
                continue;
            }
            EditableModel original = ReadImportOriginal(item.Target, "embedded");
            EditableModel model = preview ? ModelMods.Preview(original, new(item.Target, item.Model, geometry, []) { PreserveNativeParts = PreservedImportParts(inventory.Objects, item) }) : item.Model;
            objects.Add(item with { Model = model, GeometrySelected = preview ? model.Meshes.Count != 0 : geometry });
            foreach (ModelAssetClip clip in chosen)
            {
                if (!preview)
                {
                    clips.Add(clip);
                    continue;
                }
                ParseClipAddress(clip.Target, out _, out string bank, out int slot);
                EditableModel current = ReadImportOriginal(item.Target, bank);
                bool selected = modelBrowser.SelectedAssets.Contains(clip.Target);
                EditableModel aligned = selected ? ModelMods.Preview(current, new(item.Target, item.Model, false, [new(bank, slot, clip.Animation)])) : current;
                clips.Add(clip with { Animation = aligned.Animations[slot], SecondsPerFrame = 1f / current.NativeFramesPerSecond, NativeFrameCount = (int)MathF.Round(aligned.Animations[slot].Duration * current.NativeFramesPerSecond) + 1 });
            }
        }
        return new(objects, clips) { Edition = inventory.Edition };
    }

    private IReadOnlyList<int> PreservedImportParts(IEnumerable<ModelAssetObject> objects, ModelAssetObject owner)
    {
        return objects.Where(item => item.Target != owner.Target && item.RigId == owner.RigId && item.GeometrySelected && modelBrowser!.SelectedAssets.Contains("object:" + item.Target) && item.ReplacedPart?.StartsWith("mesh/", StringComparison.Ordinal) == true).Select(item => int.TryParse(item.ReplacedPart![5..], out int part) && part is >= 0 and < 256 ? part : throw new InvalidDataException("Invalid native replacement part.")).Distinct().ToArray();
    }

    private void CompleteBrowserImport()
    {
        try
        {
            ModelAssetSelection selection = BrowserImportSelection(preview: false);
            List<ModelModReplacement> replacements = [];
            List<string> warnings = [.. browserImportWarnings];
            foreach (ModelAssetObject item in selection.Objects)
            {
                ModelAssetClip[] clips = selection.Clips.Where(clip => clip.ModelTarget == item.Target).ToArray();
                if (!item.GeometrySelected && clips.Length == 0)
                {
                    continue;
                }
                ModelModClipReplacement[] selectedClips = clips.Select(clip =>
                {
                    ParseClipAddress(clip.Target, out _, out string bank, out int slot);
                    EditableModel original = ReadImportOriginal(item.Target, bank);
                    if (MathF.Abs(original.Animations[slot].Duration - clip.Animation.Duration) > 0.0001f)
                    {
                        warnings.Add($"{clip.Target}: clip duration changed; game scripts may rely on native frame positions.");
                    }
                    return new ModelModClipReplacement(bank, slot, clip.Animation);
                }).ToArray();
                ModelModReplacement incoming = new(item.Target, item.Model, item.GeometrySelected, selectedClips) { PreserveNativeParts = PreservedImportParts(selection.Objects, item) };
                EditableModel normalized = ModelMods.Preview(ReadImportOriginal(item.Target, "embedded"), incoming with { Clips = [] });
                List<ModelModClipReplacement> normalizedClips = [];
                foreach (string bank in selectedClips.Select(clip => clip.Bank).Distinct())
                {
                    ModelModClipReplacement[] components = selectedClips.Where(clip => clip.Bank == bank).ToArray();
                    EditableModel aligned = ModelMods.Preview(ReadImportOriginal(item.Target, bank), incoming with { ReplaceGeometry = false, Clips = components });
                    normalizedClips.AddRange(components.Select(clip => clip with { Animation = aligned.Animations[clip.Slot] }));
                }
                replacements.Add(new(item.Target, normalized, item.GeometrySelected, normalizedClips) { PreserveNativeParts = incoming.PreserveNativeParts });
            }
            if (replacements.Count == 0)
            {
                throw new InvalidDataException("Select at least one matched object or clip component.");
            }
            ModelModInstallPlan plan = ModelModInstaller.Plan(replacements);
            if (warnings.Count != 0 && !ModelBrowserDialogs.Confirm(Window.Handle, string.Join("\n", warnings.Distinct())))
            {
                return;
            }
            string root = Path.Combine(resourceHost!.Mods.PrimaryModsDirectory, "Memoria");
            string? destination = ModelBrowserDialogs.Destination(Window.Handle, root);
            if (destination is null)
            {
                return;
            }
            IReadOnlyList<string> conflicts = plan.GetConflicts(root, destination);
            if (conflicts.Count != 0 && !ModelBrowserDialogs.Confirm(Window.Handle, $"{conflicts.Count} selected replacement files already exist. Unselected files and package settings will be retained.", "Replace selected mod content", "Replace these files?"))
            {
                return;
            }
            ModelModInstaller.Install(root, destination, plan);
            resourceHost.Mods.RequestRefresh();
            bool published = resourceHost.Pump(true);
            modelBrowser!.CloseAssetPanel();
            RefreshModelBrowserEntries(refreshVariants: true);
            modelPreviewIdentity = null;
            modelBrowser.SetMessage(published ? "Selected replacements installed. Reload the scene to apply them." : "Files installed; inspect Mods diagnostics before use.");
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
