using System.Globalization;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private const string Scene3DPackageScreen = "field-scene-3d";
    private const string Scene3DEditorScreen = "field-scene-3d-edit";
    private FieldScene3DPackageStore? scene3DStore;
    private FieldSceneDraft? scene3DSaved;
    private PreparedFieldScene? scene3DDraft;
    private readonly Dictionary<string, byte[]> scene3DImports = new(StringComparer.Ordinal);
    private readonly Stack<PreparedFieldScene> scene3DUndo = new();
    private readonly Stack<PreparedFieldScene> scene3DRedo = new();
    private string? scene3DSelectedView;
    private int scene3DInstanceIndex;
    private bool scene3DDialogOpen;
    private bool scene3DCompare;
    private bool scene3DPreviewFullScreen;
    private TimeSpan scene3DFrozenTime;
    private float? scene3DPreviewSeconds;
    private string scene3DNotice = "";
    private bool CanEditScene3D => LightingEditorAvailable && lightingEditor is null && fieldGpu is not null;
    private bool Scene3DDirty => scene3DDraft is not null && !ReferenceEquals(scene3DDraft, scene3DSaved?.Prepared);
    private string Scene3DLightingSource => lightingAuthoringEnabled && lightingStore is not null
        ? lightingStore.Package.Name + " (authoring preview)"
        : resourceHost?.Mods.Lighting.PackageId ?? "Native";

    private void ConnectScene3DAuthoring()
    {
        gameMenus!.Scene3DAction = RunScene3DAction;
        gameMenus.Register(new MenuScreenDefinition
        {
            Id = Scene3DPackageScreen,
            Title = "3D field scenes",
            Help = "Export the current field to Blender or select a writable mod to import a scene.",
            Entries = _ => Scene3DPackageEntries(),
            Details = _ => [scene3DNotice],
            GalleryEligible = false
        });
        gameMenus.Register(new MenuScreenDefinition
        {
            Id = Scene3DEditorScreen,
            Title = "Edit 3D field scene",
            Help = "Gameplay is paused. Changes preview in the field and are written only by Save.",
            Entries = _ => Scene3DEditorEntries(),
            Details = _ => [.. new[] { Scene3DTransformPrompt, scene3DNotice, world?.Scene3DStatus ?? "Native presentation", "Lighting: " + Scene3DLightingSource }.Where(line => line.Length != 0)],
            GalleryEligible = false
        });
    }

    private static MenuEntry Scene3DEntry(string key, string label, string help, bool enabled = true) => new(key, label, help, Action: new("field-scene-3d", key), Enabled: enabled);

    private IReadOnlyList<MenuEntry> Scene3DPackageEntries()
    {
        List<MenuEntry> entries =
        [
            Scene3DEntry("export", "Export native reference to Blender", "Export the current field, cameras, walkmesh and reference material to a folder.", CanEditScene3D),
            Scene3DEntry("create", "Create 3D field scene mod", "Create a disabled writable scene package."),
            Scene3DEntry("edit", "Open scene editor (" + scene3DShortcut + ")", scene3DStore?.Package.Name ?? "Select a scene package below.", CanEditScene3D && scene3DStore is not null),
            Scene3DEntry("shortcut", "Rebind scene editor shortcut", "Release the keys, then press a new shortcut.")
        ];
        foreach (ModPackage package in resourceHost!.Mods.InstalledPackages.Where(package => package.HasFieldScene3D))
        {
            entries.Add(Scene3DEntry("package:" + package.Id, package.Name, package.IsBundled || package.Official is not null ? "Make and select a writable copy." : "Select this writable scene package."));
        }
        return entries;
    }

    private FieldSceneView? Scene3DView => scene3DDraft?.Document.Views.FirstOrDefault(view => view.Id == scene3DSelectedView);
    private FieldSceneInstance? Scene3DInstance => Scene3DView?.Instances.ElementAtOrDefault(scene3DInstanceIndex);

    private IReadOnlyList<MenuEntry> Scene3DEditorEntries()
    {
        FieldSceneView? view = Scene3DView;
        FieldSceneInstance? instance = Scene3DInstance;
        FieldSceneAsset? asset = instance is null ? null : scene3DDraft!.Assets[instance.AssetId];
        string slot = world?.Events.Scene.CameraIndex.ToString(CultureInfo.InvariantCulture) ?? "–";
        List<MenuEntry> entries =
        [
            Scene3DEntry("import", "Import GLB from Blender", "Review and add a scene instance for native camera slot " + slot + ".", !scene3DDialogOpen),
            Scene3DEntry("reimport", "Reimport selected asset", "Keep placement and overrides matched by stable ID.", instance is not null && !scene3DDialogOpen),
            Scene3DEntry("export", "Export native reference to Blender", "Capture this frozen field view.", !scene3DDialogOpen),
            Scene3DEntry("view", $"View: {view?.Id ?? "None"}", "Cycle the bindings in this field.", scene3DDraft?.Document.Views.Length > 1),
            Scene3DEntry("instance", $"Instance: {instance?.Id ?? "None"}", "Cycle scene instances in the selected view.", view?.Instances.Length > 1),
            Scene3DEntry("enabled", instance?.Enabled == true ? "Instance visible" : "Instance hidden", "Toggle the selected instance.", instance is not null),
            Scene3DEntry("project", $"Camera: {view?.Projection.ToString() ?? "Native"}", "Select the native projection or cycle the selected instance's imported cameras.", asset?.Cameras.Count > 0),
            Scene3DEntry("inspection", $"Preview camera: {scene3DInspection switch { Scene3DInspectionMode.ImportedAuthorCamera => "Imported author camera", Scene3DInspectionMode.FreeInspection => "Free inspection", _ => "Game camera" }}", "Switch preview cameras without editing the package."),
            Scene3DEntry("orbit", "Orbit inspection camera · 15°", "Orbit around the captured field pivot.", scene3DInspection == Scene3DInspectionMode.FreeInspection),
            Scene3DEntry("frame-fit", $"Frame fit: {view?.FrameFit.ToString() ?? "Contain"}", "Cycle contain, crop and expand.", view?.Projection == FieldSceneProjection.Authored),
            Scene3DEntry("camera-motion", $"Camera motion: {view?.CameraMotion.ToString() ?? "Static"}", "A static authored view suppresses native scroll and shake. Capture-relative mode preserves their projected deltas.", view?.Projection == FieldSceneProjection.Authored),
            Scene3DEntry("pose", "Align author camera pose", "Initialize pose against the native camera. Check scale and floor alignment in preview.", asset?.Cameras.Count > 0),
            Scene3DEntry("calibrate", "Fit 3D landmarks", "Load paired Blender/glTF and native XYZ landmarks, then measure the native pixel residual.", instance is not null),
            Scene3DEntry("placement", "Adjust placement", instance is null ? "" : DescribePlacement(instance), instance is not null),
            Scene3DEntry("surface", "Apply painted background material", "Keep fully opaque depth, blend fractional alpha and receive field shadows.", instance is not null && asset?.Materials.Count > 0),
            Scene3DEntry("clip", "Clip: " + (instance?.Playback?.ClipId ?? "Rest pose"), "Cycle independent animation clips.", asset?.Clips.Count > 0),
            Scene3DEntry("clip-play", scene3DPreviewSeconds is null ? "Pause clip preview" : "Play clip preview", "Control presentation-only animation time.", instance?.Playback is not null),
            Scene3DEntry("clip-time", $"Step clip preview · {scene3DPreviewSeconds ?? 0:0.00}s", "Scrub forward 0.25 seconds and wrap.", instance?.Playback is not null),
            Scene3DEntry("replace", $"Background: {view?.Background.ToString() ?? "Native"}", "Cycle overlay, selected modules and full replacement for the current native states.", view is not null && view.Projection == FieldSceneProjection.Native),
            Scene3DEntry("modules", "Choose native modules", "Select exactly which background modules this view replaces.", view?.Background == FieldSceneBackgroundMode.SelectedModules),
            Scene3DEntry("capture-states", "Bind current native states", "Apply this view only when the captured background states match.", view is not null),
            Scene3DEntry("cover-states", "Cover all native background states", "Review all tile states before claiming them for this replacement.", view?.Background == FieldSceneBackgroundMode.Replace),
            Scene3DEntry("compare", scene3DCompare ? "Show custom scene" : "Compare native field", "Toggle the complete native presentation."),
            Scene3DEntry("preview", "Full-screen field preview", "Press Back to return to the editor."),
            Scene3DEntry("lighting", "Edit active field lighting", "Switch to the selected lighting editor and keep the scene draft.", lightingAuthoringEnabled && lightingStore is not null),
            Scene3DEntry("undo", "Undo", "Undo the most recent draft edit.", scene3DUndo.Count != 0),
            Scene3DEntry("redo", "Redo", "Restore the most recently undone edit.", scene3DRedo.Count != 0),
            Scene3DEntry("save", "Save scene", "Publish the current field to the selected package.", Scene3DDirty && !scene3DDialogOpen),
            Scene3DEntry("close", "Close editor", "Resume gameplay.")
        ];
        return entries;
    }

    private async void RunScene3DAction(string action)
    {
        if (scene3DDialogOpen)
        {
            return;
        }
        try
        {
            if (action.StartsWith("package:", StringComparison.Ordinal))
            {
                ModPackage package = resourceHost!.Mods.InstalledPackages.Single(package => package.Id == action[8..] && package.HasFieldScene3D);
                FieldScene3DPackageStore store = new(package);
                scene3DStore = store.RequiresCopy ? FieldScene3DPackageStore.Create(resourceHost.Mods.PrimaryModsDirectory, package.Name + " (copy)", store) : store;
                resourceHost.Mods.Refresh();
                SaveScene3DPreferences();
                scene3DNotice = "Selected " + scene3DStore.Package.Name;
            }
            else if (action.StartsWith("step:", StringComparison.Ordinal))
            {
                StepScene3DPlacement(action[5..]);
            }
            else if (action.StartsWith("module:", StringComparison.Ordinal))
            {
                ToggleScene3DModule(int.Parse(action.AsSpan(7), CultureInfo.InvariantCulture));
            }
            else
            {
                switch (action)
                {
                    case "create":
                        CreateScene3DPackage(world?.Field is { } current && CanEditScene3D ? "3D scene · " + current.Name : "My 3D field scenes");
                        if (CanEditScene3D)
                        {
                            OpenScene3DEditor();
                        }
                        else
                        {
                            gameMenus!.Open(Scene3DPackageScreen, true);
                        }
                        break;
                    case "shortcut":
                        scene3DBindingShortcut = true;
                        scene3DWaitForRelease = true;
                        scene3DNotice = "Release all keys, then press a shortcut. Escape cancels.";
                        break;
                    case "edit":
                        OpenScene3DEditor();
                        break;
                    case "export":
                        await ExportScene3DWithDialog();
                        break;
                    case "import":
                    case "reimport":
                        await ImportScene3DWithDialog(action == "reimport");
                        break;
                    case "view":
                        SelectNextScene3DView();
                        break;
                    case "instance":
                        scene3DInstanceIndex = (scene3DInstanceIndex + 1) % Scene3DView!.Instances.Length;
                        break;
                    case "enabled":
                        ChangeScene3DInstance(instance => instance with { Enabled = !instance.Enabled });
                        break;
                    case "project":
                        CycleScene3DCamera();
                        break;
                    case "inspection":
                        CycleScene3DInspection();
                        break;
                    case "orbit":
                        scene3DInspectionAngle = (scene3DInspectionAngle + MathF.PI / 12) % MathF.Tau;
                        break;
                    case "frame-fit":
                        ChangeScene3DView(view => view with { FrameFit = (FieldSceneFrameFit)(((int)view.FrameFit + 1) % 3) });
                        break;
                    case "camera-motion":
                        ToggleScene3DCameraMotion();
                        break;
                    case "pose":
                        AlignScene3DCamera();
                        break;
                    case "calibrate":
                        await CalibrateScene3DWithDialog();
                        break;
                    case "placement":
                        gameMenus!.Push("field-scene-3d-placement");
                        break;
                    case "surface":
                        ChangeScene3DInstance(instance => instance with { Materials = scene3DDraft!.Assets[instance.AssetId].Materials.Select(material => new FieldSceneMaterialOverride(material.Id, FieldSceneShading.UnlitShadowReceiver, FieldSceneAlpha.OpaqueCoreAndBlend, true, ReceiveShadows: true)).ToArray() });
                        break;
                    case "clip":
                        CycleScene3DClip();
                        break;
                    case "clip-play":
                        scene3DPreviewSeconds = scene3DPreviewSeconds is null ? 0 : null;
                        break;
                    case "clip-time":
                        scene3DPreviewSeconds = (scene3DPreviewSeconds ?? 0) + 0.25f;
                        break;
                    case "replace":
                        CycleScene3DBackground();
                        break;
                    case "modules":
                        gameMenus!.Push("field-scene-3d-modules");
                        break;
                    case "capture-states":
                        ChangeScene3DView(view => view with { NativeStates = CaptureScene3DStates(), NativeStateSets = null });
                        break;
                    case "cover-states":
                        Dictionary<int, int[]> states = world!.Field!.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => (int)tile.AnimationId).ToDictionary(group => group.Key, group => new[] { -1 }.Concat(group.Select(tile => (int)tile.AnimationState).Distinct().Order()).ToArray());
                        string coverage = string.Join("\n", states.Select(pair => $"Module {pair.Key}: {string.Join(", ", pair.Value)}"));
                        if (await ConfirmScene3DAsync("This view replaces every listed background state. Verify that the custom scene represents any scripted doors, screens and effects in this list.\n" + coverage, "Replace native background states", "Claim states"))
                        {
                            ChangeScene3DView(view => view with { NativeStates = null, NativeStateSets = states });
                        }
                        break;
                    case "compare":
                        scene3DCompare = !scene3DCompare;
                        break;
                    case "preview":
                        scene3DPreviewFullScreen = !scene3DPreviewFullScreen;
                        break;
                    case "lighting":
                        OpenLightingEditor();
                        scene3DNotice = lightingNotice;
                        break;
                    case "undo":
                        RestoreScene3DDraft(scene3DUndo, scene3DRedo);
                        break;
                    case "redo":
                        RestoreScene3DDraft(scene3DRedo, scene3DUndo);
                        break;
                    case "save":
                        SaveScene3DDraft();
                        break;
                    case "close":
                        if (scene3DPreviewFullScreen)
                        {
                            scene3DPreviewFullScreen = false;
                            break;
                        }
                        if (!Scene3DDirty || await ConfirmScene3DAsync("Discard the unsaved changes to this field?", "Close scene editor", "Discard changes"))
                        {
                            CloseScene3DEditor();
                        }
                        break;
                }
            }
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            scene3DNotice = exception.Message;
        }
        finally
        {
            gameMenus?.InvalidateLiveData();
        }
    }

    private void CreateScene3DPackage(string name)
    {
        scene3DStore = FieldScene3DPackageStore.Create(resourceHost!.Mods.PrimaryModsDirectory, name);
        resourceHost.Mods.Refresh();
        SaveScene3DPreferences();
        scene3DNotice = "Selected " + scene3DStore.Package.Name;
    }

    private void OpenScene3DEditor()
    {
        if (!CanEditScene3D || world?.Field is not { } current || scene3DStore is null || scene3DDraft is not null)
        {
            throw new InvalidOperationException("Select a writable scene package and enter a stable FFVIII field with GPU rendering.");
        }
        if (scene3DStore.RequiresCopy)
        {
            scene3DStore = FieldScene3DPackageStore.Create(resourceHost!.Mods.PrimaryModsDirectory, scene3DStore.Package.Name + " (copy)", scene3DStore);
            resourceHost.Mods.Refresh();
            SaveScene3DPreferences();
        }
        FieldSceneDraft saved = scene3DStore.Open(current.Name);
        if (!world.BeginLightingEdit())
        {
            throw new InvalidOperationException("Wait until the current field has been drawn before opening the editor.");
        }
        scene3DSaved = saved;
        scene3DDraft = saved.Prepared;
        FieldSceneView[] views = saved.Prepared.Document.Views;
        int slot = world.Events.Scene.CameraIndex;
        scene3DSelectedView = (views.FirstOrDefault(view => view.Matches(slot, world.Events.BackgroundStates)) ??
            views.FirstOrDefault(view => view.CameraSlot == slot) ?? views.FirstOrDefault())?.Id;
        scene3DInstanceIndex = 0;
        scene3DFrozenTime = gameplayClock.Total;
        scene3DPreviewStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        scene3DCompare = false;
        scene3DPreviewFullScreen = false;
        scene3DInspection = Scene3DInspectionMode.GameCamera;
        scene3DInspectionAngle = MathF.PI / 12;
        scene3DPreviewSeconds = null;
        scene3DUndo.Clear();
        scene3DRedo.Clear();
        scene3DImports.Clear();
        presentationTiming.DiscardPendingTime();
        gameMenus!.Open(Scene3DEditorScreen);
        SynchronizeUiMoviePause();
    }

    private void CloseScene3DEditor()
    {
        scene3DDraft = null;
        scene3DPreviewFullScreen = false;
        scene3DTransformMode = null;
        scene3DSaved = null;
        scene3DUndo.Clear();
        scene3DRedo.Clear();
        scene3DImports.Clear();
        world?.EndLightingEdit();
        gameMenus?.Close();
        presentationTiming.DiscardPendingTime();
        SynchronizeUiMoviePause();
    }

    private async Task<bool> ConfirmScene3DAsync(string details, string title, string confirm)
    {
        scene3DDialogOpen = true;
        try
        {
            return await ModelBrowserDialogs.ShowConfirmationAsync(new(details, title, "Cancel", confirm, "Up / Down: scroll review"));
        }
        finally
        {
            scene3DDialogOpen = false;
        }
    }

    private async Task ExportScene3DWithDialog()
    {
        if (!CanEditScene3D || world is not { } owner)
        {
            throw new InvalidOperationException("Open a stable FFVIII field before exporting.");
        }
        FieldAssets current = owner.Field!;
        string? path;
        scene3DDialogOpen = true;
        try
        {
            path = await ModelBrowserDialogs.PathAsync(ModelBrowserDialogs.Desktop.SelectFolderAsync(new("Export native field reference to Blender")));
        }
        finally
        {
            scene3DDialogOpen = false;
            keyboard.SuppressHeldNameInput();
        }
        if (path is not null && ReferenceEquals(owner, world) && ReferenceEquals(current, owner.Field))
        {
            scene3DNotice = "Exported " + ExportCurrentScene3D(Path.Combine(path, current.Name + "-reference"));
        }
    }

    private string ExportCurrentScene3D(string path) => world!.ExportScene3DReference(path, resourceHost!.Resources.Source.Edition.ToString(), resourceHost.Resources.ResourceLanguage, gameplayClock.Total);

    private async Task ImportScene3DWithDialog(bool reimport)
    {
        PreparedFieldScene draft = scene3DDraft ?? throw new InvalidOperationException("Open the field scene editor before importing.");
        string? path;
        scene3DDialogOpen = true;
        try
        {
            path = await ModelBrowserDialogs.ImportAsync(Window.Handle, globalSettings, (key, text) => key switch
            {
                "model.dialog.filter.glb" => "glTF binary scene (*.glb)",
                "model.dialog.import.title" => "Import 3D field scene from Blender",
                _ => LocalizeUiText(key, text)
            });
        }
        finally
        {
            scene3DDialogOpen = false;
            keyboard.SuppressHeldNameInput();
        }
        if (path is null || !ReferenceEquals(draft, scene3DDraft))
        {
            return;
        }
        byte[] bytes = FieldScene3DPackageStore.ReadGlb(path);
        FieldSceneAsset asset = await Task.Run(() => FieldSceneAsset.ImportGlb(bytes));
        string summary = $"{draft.Document.Field} · Native camera {world!.Events.Scene.CameraIndex}\n{DescribeScene3DAsset(asset)}";
        if (reimport && Scene3DInstance is { } instance)
        {
            FieldSceneAsset before = draft.Assets[instance.AssetId];
            string[] removed = before.Primitives.Select(primitive => primitive.Id).Except(asset.Primitives.Select(primitive => primitive.Id)).Concat(before.Materials.Select(material => material.Id).Except(asset.Materials.Select(material => material.Id))).Concat(before.Clips.Select(clip => clip.Id).Except(asset.Clips.Select(clip => clip.Id))).Concat(before.Cameras.Select(camera => camera.Id).Except(asset.Cameras.Select(camera => camera.Id))).ToArray();
            if (removed.Length != 0)
            {
                summary += "\n\nRemoved or renamed IDs:\n" + string.Join("\n", removed) + "\nSave requires valid clip, camera and material bindings.";
            }
        }
        if (await ConfirmScene3DAsync(summary, reimport ? "Reimport scene asset" : "Import 3D field scene", "Preview") && ReferenceEquals(draft, scene3DDraft))
        {
            ApplyScene3DImport(bytes, asset, reimport);
        }
    }

    private static string DescribeScene3DAsset(FieldSceneAsset asset)
    {
        long textureBytes = asset.Geometry.Textures.Sum(texture => (long)texture.Width * texture.Height * 4);
        string clips = string.Join(", ", asset.Clips.Select(clip => $"{clip.Name} ({clip.Duration:0.###} s)"));
        return $"{asset.Geometry.Meshes.Count} primitives · {asset.Materials.Count} materials · {asset.Geometry.Bones.Count(bone => bone.IsJoint)} joints · {asset.Cameras.Count} cameras\n{asset.Geometry.Textures.Count} decoded textures · {textureBytes / 1048576.0:0.##} MiB\n{clips}\n{string.Join("\n", asset.Warnings)}";
    }

    private void ApplyScene3DImport(byte[] bytes, FieldSceneAsset asset, bool reimport)
    {
        PreparedFieldScene draft = scene3DDraft ?? throw new InvalidOperationException("Open the field scene editor before importing.");
        string digest = FieldSceneDocuments.Digest(bytes);
        string assetId = reimport && Scene3DInstance is { } selected ? selected.AssetId : UniqueScene3DId(asset.Id, draft.Assets.Keys);
        Dictionary<string, FieldSceneAsset> assets = new(draft.Assets, StringComparer.Ordinal) { [assetId] = asset };
        FieldSceneDocument document = draft.Document;
        FieldSceneAssetBinding[] assetBindings = [.. document.Assets.Where(binding => binding.Id != assetId), new(assetId, digest)];
        FieldSceneView[] views = document.Views;
        if (!reimport)
        {
            FieldSceneView? view = Scene3DView;
            if (view?.CameraSlot != world!.Events.Scene.CameraIndex)
            {
                view = null;
            }
            string id = UniqueScene3DId("camera-" + world.Events.Scene.CameraIndex.ToString(CultureInfo.InvariantCulture), views.Select(view => view.Id));
            view ??= new(id, world.Events.Scene.CameraIndex, FieldSceneBackgroundMode.Overlay, []);
            FieldSceneInstance instance = new(UniqueScene3DId(assetId, view.Instances.Select(instance => instance.Id)), assetId, FieldSceneDocuments.Values(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateScale(128)), Calibration: "Native reference basis (128 field units per glTF unit)");
            scene3DSelectedView = view.Id;
            scene3DInstanceIndex = view.Instances.Length;
            views = [.. views.Where(other => other.Id != view.Id), view with { Instances = [.. view.Instances, instance] }];
        }
        scene3DImports[digest] = bytes;
        SetScene3DDraft(new(draft.PackageId, document with { Assets = assetBindings, Views = views }, assets));
        scene3DNotice = DescribeScene3DAsset(asset);
    }

    private static string UniqueScene3DId(string name, IEnumerable<string> existing)
    {
        string root = string.IsNullOrWhiteSpace(name) ? "scene" : name[..Math.Min(225, name.Length)];
        HashSet<string> ids = new(existing, StringComparer.Ordinal);
        string id = root;
        for (int index = 2; ids.Contains(id); index++)
        {
            id = root + "-" + index.ToString(CultureInfo.InvariantCulture);
        }
        return id;
    }
}
