using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private sealed record Scene3DPreferences(string? PackageId, AuthoringKeyChord Shortcut);
    private AuthoringKeyChord scene3DShortcut = new(Keys.K, Control: true);
    private KeyboardState scene3DPreviousKeys;
    private bool scene3DBindingShortcut;
    private bool scene3DWaitForRelease;
    private int scene3DPlacementStep = 16;
    private long scene3DPreviewStarted;
    private string Scene3DPreferencesPath => Path.Combine(Path.GetDirectoryName(UiPreferencesPath)!, "field-scene-authoring.json");

    private void ConnectScene3DEditorDetails()
    {
        try
        {
            if (File.Exists(Scene3DPreferencesPath) && JsonSerializer.Deserialize<Scene3DPreferences>(File.ReadAllBytes(Scene3DPreferencesPath)) is { } preferences)
            {
                if (Scene3DShortcutConflict(preferences.Shortcut) is null)
                {
                    scene3DShortcut = preferences.Shortcut;
                }
                if (resourceHost!.Mods.InstalledPackages.FirstOrDefault(package => package.HasFieldScene3D && package.Id == preferences.PackageId) is { } package)
                {
                    scene3DStore = new(package);
                }
            }
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            scene3DNotice = exception.Message;
        }

        gameMenus!.Register(new MenuScreenDefinition
        {
            Id = "field-scene-3d-placement",
            Title = "Scene placement",
            Help = "Adjust the complete scene instance in native field units. Changes also move its camera.",
            Entries = _ => Scene3DPlacementEntries(),
            Details = _ => [Scene3DTransformPrompt, Scene3DInstance is { } instance ? DescribePlacement(instance) : scene3DNotice],
            GalleryEligible = false
        });
        gameMenus.Register(new MenuScreenDefinition
        {
            Id = "field-scene-3d-modules",
            Title = "Native background modules",
            Help = "Checked modules are hidden when this replacement view is active.",
            Entries = _ => Scene3DModuleEntries(),
            GalleryEligible = false
        });
    }

    private string? Scene3DShortcutConflict(AuthoringKeyChord chord)
    {
        if (chord is null || !Enum.IsDefined(chord.Key) || chord.Key is Keys.None or Keys.Escape or Keys.LeftControl or Keys.RightControl or Keys.LeftShift or Keys.RightShift or Keys.LeftAlt or Keys.RightAlt)
        {
            return "Choose a non-modifier shortcut.";
        }
        if (lightingShortcuts.Bindings.TryGetValue(LightingEditorCommand.OpenEditor, out AuthoringKeyChord? lighting) && LightingShortcutSettings.Overlaps(lighting, chord))
        {
            return "This shortcut opens the lighting editor.";
        }
        return LightingShortcutConflict(LightingEditorCommand.OpenEditor, chord);
    }

    private void SaveScene3DPreferences()
    {
        string path = Scene3DPreferencesPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(new Scene3DPreferences(scene3DStore?.Package.Id, scene3DShortcut)));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private bool PollScene3DShortcut()
    {
        KeyboardState keys = physicalInput.ReadKeyboard();
        KeyboardState previous = scene3DPreviousKeys;
        scene3DPreviousKeys = keys;
        if (PollScene3DTransform(keys, previous))
        {
            return true;
        }
        if (scene3DBindingShortcut)
        {
            if (scene3DWaitForRelease)
            {
                scene3DWaitForRelease = keys.GetPressedKeys().Length != 0;
                return true;
            }
            Keys key = keys.GetPressedKeys().FirstOrDefault(key => previous.IsKeyUp(key) && key is not (Keys.LeftControl or Keys.RightControl or Keys.LeftShift or Keys.RightShift or Keys.LeftAlt or Keys.RightAlt));
            if (key == Keys.None)
            {
                return true;
            }
            scene3DBindingShortcut = false;
            if (key != Keys.Escape)
            {
                AuthoringKeyChord chord = new(key, keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl), keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift), keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
                string? conflict = Scene3DShortcutConflict(chord);
                if (conflict is null)
                {
                    scene3DShortcut = chord;
                    SaveScene3DPreferences();
                }
                scene3DNotice = conflict ?? "Scene editor shortcut: " + scene3DShortcut;
            }
            keyboard.SuppressHeldUiInput();
            gameMenus?.InvalidateLiveData();
            return true;
        }
        if (physicalInput.CanRead && scene3DStore is not null && scene3DDraft is null && gameMenus?.IsOpen != true && CanEditScene3D && scene3DShortcut.Matches(keys) && !scene3DShortcut.Matches(previous) && Scene3DShortcutConflict(scene3DShortcut) is null)
        {
            RunScene3DAction("edit");
            return true;
        }
        return false;
    }

    private void RestoreScene3DDraft(Stack<PreparedFieldScene> from, Stack<PreparedFieldScene> to)
    {
        if (!from.TryPop(out PreparedFieldScene? draft))
        {
            return;
        }
        to.Push(scene3DDraft!);
        scene3DDraft = draft;
        if (Scene3DView is null)
        {
            scene3DSelectedView = draft.Document.Views.FirstOrDefault()?.Id;
        }
        scene3DInstanceIndex = Math.Clamp(scene3DInstanceIndex, 0, Math.Max(0, (Scene3DView?.Instances.Length ?? 0) - 1));
    }

    private void SetScene3DDraft(PreparedFieldScene draft)
    {
        FieldSceneDocuments.Write(draft.Document);
        scene3DUndo.Push(scene3DDraft!);
        scene3DRedo.Clear();
        scene3DDraft = draft;
    }

    private void SelectNextScene3DView()
    {
        FieldSceneView[] views = scene3DDraft!.Document.Views;
        int index = Array.FindIndex(views, view => view.Id == scene3DSelectedView);
        scene3DSelectedView = views[(index + 1) % views.Length].Id;
        scene3DInstanceIndex = 0;
    }

    private void ChangeScene3DView(Func<FieldSceneView, FieldSceneView> update)
    {
        PreparedFieldScene draft = scene3DDraft!;
        FieldSceneView[] views = draft.Document.Views.Select(view => view.Id == scene3DSelectedView ? update(view) : view).ToArray();
        SetScene3DDraft(draft with { Document = draft.Document with { Views = views } });
    }

    private void ChangeScene3DInstance(Func<FieldSceneInstance, FieldSceneInstance> update)
    {
        ChangeScene3DView(view => view with { Instances = view.Instances.Select((instance, index) => index == scene3DInstanceIndex ? update(instance) : instance).ToArray() });
    }

    private Dictionary<int, int> CaptureScene3DStates() => world!.Field!.Background.Where(tile => tile.AnimationId != 255).Select(tile => (int)tile.AnimationId).Distinct().ToDictionary(module => module, module => world.Events.BackgroundStates.GetValueOrDefault(module, -1));

    private void CycleScene3DCamera()
    {
        FieldSceneView view = Scene3DView!;
        FieldSceneInstance instance = Scene3DInstance!;
        FieldSceneCamera[] cameras = scene3DDraft!.Assets[instance.AssetId].Cameras.ToArray();
        int next = Array.FindIndex(cameras, camera => camera.Id == view.CameraId && instance.Id == view.CameraInstance) + 1;
        if (next == cameras.Length)
        {
            ChangeScene3DView(view => view with { Projection = FieldSceneProjection.Native, CameraInstance = null, CameraId = null, CameraMotion = FieldSceneCameraMotion.Static, ScrollBaseline = null });
            return;
        }
        ChangeScene3DView(view => view with
        {
            Projection = FieldSceneProjection.Authored,
            CameraInstance = instance.Id,
            CameraId = cameras[next].Id,
            Background = FieldSceneBackgroundMode.Replace,
            ReplacedModules = null,
            NativeStates = view.Background == FieldSceneBackgroundMode.Replace ? view.NativeStates : CaptureScene3DStates(),
            NativeStateSets = view.Background == FieldSceneBackgroundMode.Replace ? view.NativeStateSets : null
        });
    }

    private void ToggleScene3DCameraMotion()
    {
        Vector2 baseline = world!.Scene3DNativeScroll;
        ChangeScene3DView(view => view.CameraMotion == FieldSceneCameraMotion.Static
            ? view with { CameraMotion = FieldSceneCameraMotion.CaptureRelative, ScrollBaseline = new(baseline.X, baseline.Y) }
            : view with { CameraMotion = FieldSceneCameraMotion.Static, ScrollBaseline = null });
    }

    private void CycleScene3DClip()
    {
        FieldSceneInstance instance = Scene3DInstance!;
        FieldSceneClip[] clips = scene3DDraft!.Assets[instance.AssetId].Clips.ToArray();
        int next = Array.FindIndex(clips, clip => clip.Id == instance.Playback?.ClipId) + 1;
        ChangeScene3DInstance(instance => instance with { Playback = next < clips.Length ? new(clips[next].Id) : null });
    }

    private void CycleScene3DBackground()
    {
        int[] modules = world!.Field!.Background.Select(tile => (int)tile.AnimationId).Distinct().Order().ToArray();
        ChangeScene3DView(view => view.Background switch
        {
            FieldSceneBackgroundMode.Overlay when modules.Length != 0 => view with { Background = FieldSceneBackgroundMode.SelectedModules, ReplacedModules = [modules[0]] },
            FieldSceneBackgroundMode.Overlay or FieldSceneBackgroundMode.SelectedModules => view with { Background = FieldSceneBackgroundMode.Replace, NativeStates = CaptureScene3DStates(), NativeStateSets = null, ReplacedModules = null },
            _ => view with { Background = FieldSceneBackgroundMode.Overlay, NativeStates = null, NativeStateSets = null }
        });
    }

    private void ToggleScene3DModule(int module)
    {
        ChangeScene3DView(view =>
        {
            HashSet<int> modules = view.ReplacedModules!.ToHashSet();
            if (!modules.Add(module))
            {
                modules.Remove(module);
            }
            return modules.Count > 0
                ? view with { ReplacedModules = modules.Order().ToArray() }
                : view with { Background = FieldSceneBackgroundMode.Overlay, ReplacedModules = null };
        });
        if (Scene3DView!.Background == FieldSceneBackgroundMode.Overlay)
        {
            gameMenus!.Back();
        }
    }

    private IReadOnlyList<MenuEntry> Scene3DModuleEntries()
    {
        IReadOnlySet<int> selected = (Scene3DView?.ReplacedModules ?? []).ToHashSet();
        return world!.Field!.Background.Select(tile => (int)tile.AnimationId).Distinct().Order().Select(module => Scene3DEntry("module:" + module, (selected.Contains(module) ? "☑ " : "☐ ") + (module == 255 ? "Static tiles" : "Module " + module), "Toggle replacement for these native tiles.")).ToArray();
    }

    private void AlignScene3DCamera()
    {
        FieldSceneInstance instance = Scene3DInstance!;
        FieldSceneAsset asset = scene3DDraft!.Assets[instance.AssetId];
        FieldSceneCamera camera = asset.Cameras.FirstOrDefault(camera => camera.Id == Scene3DView!.CameraId) ?? asset.Cameras[0];
        FieldCamera native = world!.Field!.GetCamera(Scene3DView!.CameraSlot);
        Matrix4x4 fieldToNativeCamera = new(native.AxisX.X, native.AxisY.X, native.AxisZ.X, 0, native.AxisX.Y, native.AxisY.Y, native.AxisZ.Y, 0, native.AxisX.Z, native.AxisY.Z, native.AxisZ.Z, 0, native.Translation.X, native.Translation.Y, native.Translation.Z, 1);
        Matrix4x4 cameraWorld = asset.SampleTransforms(-1, 0)[camera.Node];
        if (!Matrix4x4.Invert(cameraWorld, out Matrix4x4 assetToCamera) || !Matrix4x4.Invert(fieldToNativeCamera, out Matrix4x4 cameraToField) || !Matrix4x4.Decompose(FieldSceneDocuments.Matrix(instance.AssetToField), out Vector3 scale, out _, out _))
        {
            throw new InvalidDataException("The selected cameras or placement cannot be inverted for alignment.");
        }
        Matrix4x4 placement = assetToCamera * Matrix4x4.CreateScale(scale.X, -scale.X, -scale.X) * cameraToField;
        ChangeScene3DInstance(instance => instance with { AssetToField = FieldSceneDocuments.Values(placement), Calibration = "Author pose aligned to native camera; scale and intrinsics require landmark verification" });
        scene3DNotice = "Camera poses aligned. Compare native and custom framing and adjust placement against landmarks.";
    }

    private async Task CalibrateScene3DWithDialog()
    {
        scene3DDialogOpen = true;
        try
        {
            string? path = await ModelBrowserDialogs.PathAsync(ModelBrowserDialogs.Desktop.OpenFileAsync(new("Choose 3D landmark pairs", Filters: [new("Landmark pairs (*.json)", ["json"])])));
            if (path is not null)
            {
                CalibrateScene3D(path);
            }
        }
        finally
        {
            scene3DDialogOpen = false;
            keyboard.SuppressHeldNameInput();
        }
    }

    private void CalibrateScene3D(string path)
    {
        if (Scene3DInstance is null || new FileInfo(path).Length > 256 * 1024)
        {
            throw new InvalidDataException("Select a scene instance and a landmark file no larger than 256 KiB.");
        }
        FieldSceneLandmark[] points = FieldSceneCalibration.ReadLandmarks(File.ReadAllBytes(path));
        FieldSceneCalibrationResult fit = FieldSceneCalibration.Fit(points);
        (float rms, float maximum) = world!.MeasureScene3DReprojection(points, fit.AssetToField);
        string report = FormattableString.Invariant($"{points.Length} landmarks · {fit.RmsFieldUnits:0.###} RMS field units ({fit.MaximumFieldUnits:0.###} max) · {rms:0.##} RMS native pixels ({maximum:0.##} max)");
        ChangeScene3DInstance(instance => instance with { AssetToField = FieldSceneDocuments.Values(fit.AssetToField), Calibration = report });
        scene3DNotice = report;
    }

    private static string DescribePlacement(FieldSceneInstance instance)
    {
        Matrix4x4 matrix = FieldSceneDocuments.Matrix(instance.AssetToField);
        Vector3 offset = matrix.Translation;
        return $"X {offset.X:0.##} · Y {offset.Y:0.##} · Z {offset.Z:0.##} · scale {new Vector3(matrix.M11, matrix.M12, matrix.M13).Length():0.###}";
    }

    private IReadOnlyList<MenuEntry> Scene3DPlacementEntries()
    {
        List<MenuEntry> entries =
        [
            Scene3DEntry("step:numeric-translate", "Move · G", "Enter an exact translation along X, Y or Z."),
            Scene3DEntry("step:numeric-rotate", "Rotate · R", "Enter an exact rotation in degrees around X, Y or Z."),
            Scene3DEntry("step:numeric-scale", "Scale · S", "Enter an exact uniform scale multiplier."),
            Scene3DEntry("step:size", "Translation step: " + scene3DPlacementStep, "Cycle 1, 4, 16 and 64 native units.")
        ];
        foreach (string axis in new[] { "X", "Y", "Z" })
        {
            entries.Add(Scene3DEntry("step:move-" + axis + "+", "Move +" + axis, "Translate the selected instance."));
            entries.Add(Scene3DEntry("step:move-" + axis + "-", "Move −" + axis, "Translate the selected instance."));
            entries.Add(Scene3DEntry("step:rotate-" + axis + "+", "Rotate +" + axis + " · 5°", "Rotate around the instance origin."));
            entries.Add(Scene3DEntry("step:rotate-" + axis + "-", "Rotate −" + axis + " · 5°", "Rotate around the instance origin."));
        }
        entries.Add(Scene3DEntry("step:scale+", "Increase scale · 10%", "Uniform scale about the instance origin."));
        entries.Add(Scene3DEntry("step:scale-", "Decrease scale · 10%", "Uniform scale about the instance origin."));
        entries.Add(Scene3DEntry("undo", "Undo", "Undo the most recent draft edit.", scene3DUndo.Count > 0));
        entries.Add(Scene3DEntry("redo", "Redo", "Restore the most recently undone edit.", scene3DRedo.Count > 0));
        return entries;
    }

    private void StepScene3DPlacement(string command)
    {
        if (command.StartsWith("numeric-", StringComparison.Ordinal) && Enum.TryParse(command[8..], true, out Scene3DTransformMode mode))
        {
            BeginScene3DTransform(mode);
            return;
        }
        if (command == "size")
        {
            scene3DPlacementStep = scene3DPlacementStep == 64 ? 1 : scene3DPlacementStep * 4;
            return;
        }
        float direction = command.EndsWith('+') ? 1 : -1;
        ChangeScene3DInstance(instance =>
        {
            Matrix4x4 current = FieldSceneDocuments.Matrix(instance.AssetToField);
            if (command.StartsWith("scale", StringComparison.Ordinal))
            {
                current = Matrix4x4.CreateScale(direction > 0 ? 1.1f : 1 / 1.1f) * current;
            }
            else
            {
                Vector3 axis = command[^2] switch { 'X' => Vector3.UnitX, 'Y' => Vector3.UnitY, _ => Vector3.UnitZ };
                if (command.StartsWith("move-", StringComparison.Ordinal))
                {
                    current.Translation += axis * (direction * scene3DPlacementStep);
                }
                else
                {
                    Vector3 origin = current.Translation;
                    current.Translation = Vector3.Zero;
                    current *= Matrix4x4.CreateFromAxisAngle(axis, direction * MathF.PI / 36);
                    current.Translation = origin;
                }
            }
            return instance with { AssetToField = FieldSceneDocuments.Values(current) };
        });
    }

    private void SaveScene3DDraft()
    {
        if (scene3DDraft is null || scene3DStore is null || scene3DSaved is null)
        {
            throw new InvalidOperationException("There is no open scene draft.");
        }
        scene3DSaved = scene3DStore.Save(scene3DDraft.Document, scene3DSaved.Revision, scene3DImports);
        scene3DDraft = scene3DSaved.Prepared;
        resourceHost!.Mods.Refresh();
        scene3DNotice = $"Saved {scene3DDraft.Document.Field} to {scene3DStore.Package.Name}.";
    }

    private void ApplyScene3DPreview()
    {
        world!.Scene3DPreview = Scene3DDisplayDraft();
        world.Scene3DCompareNative = scene3DDraft is not null && scene3DCompare;
        world.Scene3DPreviewTime = scene3DDraft is null ? null : scene3DPreviewSeconds ?? (float)System.Diagnostics.Stopwatch.GetElapsedTime(scene3DPreviewStarted).TotalSeconds;
    }

    private void DrawScene3DPreview(Microsoft.Xna.Framework.Rectangle bounds)
    {
        if (scene3DDraft is null || gameMenus?.ScreenId is not (Scene3DEditorScreen or "field-scene-3d-placement" or "field-scene-3d-modules"))
        {
            return;
        }
        var surface = PresentedFieldTexture;
        var design = new Microsoft.Xna.Framework.Rectangle(bounds.X + (int)(bounds.Width * 0.505f), bounds.Y + (int)(bounds.Height * 0.41f), (int)(bounds.Width * 0.46f), (int)(bounds.Height * 0.42f));
        var fit = Runtime.ViewportLayout.Fit(surface.Width, surface.Height, design.Width, design.Height);
        batch.Draw(surface, new Microsoft.Xna.Framework.Rectangle(design.X + fit.X, design.Y + fit.Y, fit.Width, fit.Height), Microsoft.Xna.Framework.Color.White);
    }

    private object DispatchScene3DControl(ControlRequest request)
    {
        if (!CanEditScene3D)
        {
            throw new InvalidOperationException("The 3D scene tool requires a stable FFVIII field with GPU rendering.");
        }
        switch (request.Command.ToLowerInvariant())
        {
            case "scene3d-export":
                return new { Reference = ExportCurrentScene3D(Path.GetFullPath(request.Path ?? throw new InvalidDataException("Supply a reference output directory."))) };
            case "scene3d-create":
                CreateScene3DPackage(request.Name ?? "3D field scene");
                OpenScene3DEditor();
                break;
            case "scene3d-open":
                ModPackage package = resourceHost!.Mods.InstalledPackages.Single(package => package.HasFieldScene3D && package.Id == request.Name);
                scene3DStore = new(package);
                OpenScene3DEditor();
                break;
            case "scene3d-import":
                byte[] bytes = FieldScene3DPackageStore.ReadGlb(Path.GetFullPath(request.Path ?? throw new InvalidDataException("Supply a GLB path.")));
                ApplyScene3DImport(bytes, FieldSceneAsset.ImportGlb(bytes), reimport: request.Selection == 1);
                break;
            case "scene3d-edit":
                RunScene3DAction(request.Name ?? throw new InvalidDataException("Supply an editor action."));
                break;
            case "scene3d-calibrate":
                CalibrateScene3D(Path.GetFullPath(request.Path ?? throw new InvalidDataException("Supply a landmark JSON path.")));
                break;
            case "scene3d-close":
                if (Scene3DDirty && request.Selection != 1)
                {
                    throw new InvalidOperationException("Save the draft or explicitly request discard with Selection = 1.");
                }
                CloseScene3DEditor();
                break;
            case "scene3d-status":
                break;
            default:
                throw new InvalidDataException("Unknown 3D scene command.");
        }
        gameMenus?.InvalidateLiveData();
        return new
        {
            Package = scene3DStore?.Package.Id,
            Directory = scene3DStore?.Package.RootDirectory,
            Draft = scene3DDraft?.Document,
            Dirty = Scene3DDirty,
            Notice = scene3DNotice,
            Presentation = world?.Scene3DStatus,
            Installed = resourceHost?.Mods.InstalledPackages.Where(package => package.HasFieldScene3D).Select(package => new { package.Id, package.Status, package.RequestedEnabled }),
            Diagnostics = resourceHost?.Mods.Diagnostics.Where(diagnostic => diagnostic.Code.Contains("FieldScene", StringComparison.Ordinal) || diagnostic.PackageId == scene3DStore?.Package.Id)
        };
    }
}
