using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private sealed record LightingWorkspace(string? SelectedPackageId, LightingShortcutSettings Shortcuts);
    private LightingEditorController? lightingEditor;
    private FieldLightingPackageStore? lightingStore;
    private FieldLightingSceneSnapshot? lightingSavedScene;
    private LightingShortcutSettings lightingShortcuts = new();
    private string? lightingRememberedPackage;
    private bool lightingAuthoringEnabled;
    private KeyboardState lightingPreviousKeys;
    private LightingEditorCommand? lightingRebinding;
    private bool lightingRebindingWaitForRelease;
    private string lightingNotice = "";
    private long lightingPreviewGeneration = -1;
    private string? lightingPreviewField;
    private int lightingEditingFieldId;
    private FieldAssets? lightingEditingField;
    private TimeSpan lightingFrozenTime;
    private bool lightingNameInputActive;
    private string LightingWorkspacePath => Path.Combine(Path.GetDirectoryName(UiPreferencesPath)!, "lighting-authoring.json");

    private bool LightingEditorAvailable => !InFf7 && SceneInspectorAvailable && !startup.Active && battleEntryTransition is null && !saveMenu.IsOpen && modelBrowser is null && sceneInspector is null && !localizationWorkbench.CapturesSimulation && authoringCandidates is null && events?.PartyMenu is null && events?.Scene.NativeMenuTutorial is null && events?.Scene.NativeTutorial is null && events?.FieldId == session.State.FieldId && world?.HasPendingBattle != true;

    private void ConnectLightingAuthoring()
    {
        try
        {
            if (File.Exists(LightingWorkspacePath))
            {
                LightingWorkspace? workspace = JsonSerializer.Deserialize<LightingWorkspace>(File.ReadAllText(LightingWorkspacePath));
                if (workspace is not null)
                {
                    workspace.Shortcuts.Validate();
                    lightingShortcuts = workspace.Shortcuts;
                    lightingRememberedPackage = workspace.SelectedPackageId;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            lightingNotice = "Lighting preferences could not be loaded: " + exception.Message;
        }
        gameMenus!.ConnectLightingAuthoring(LightingAuthoringEntries, LightingAuthoringAction, CreateLightingPackage, resourceHost!.Mods.PrimaryModsDirectory);
        gameMenus.Register(new MenuScreenDefinition
        {
            Id = "lighting-shortcuts",
            Title = "Lighting editor shortcuts",
            Help = "Choose a command, release the keys, then press a new shortcut. Captured keys never reach gameplay.",
            Entries = _ => LightingShortcutEntries(),
            GalleryEligible = false
        });
    }

    private IReadOnlyList<MenuEntry> LightingAuthoringEntries()
    {
        ModPackage? selected = lightingStore?.Package;
        ModPackage? installed = resourceHost!.Mods.InstalledPackages.FirstOrDefault(package => package.Id == selected?.Id);
        List<MenuEntry> entries =
        [
            new("lighting-create", "Create lighting mod", "Create one empty writable package, select it and enable authoring.", Destination: "lighting-create"),
            new("lighting-toggle", "Lighting authoring mode: " + (lightingAuthoringEnabled ? "On" : "Off"), "Authoring preview uses only the selected package. Turning it off restores installed mod selection.", Action: new("lighting-authoring", "toggle"), Enabled: selected is not null || lightingRememberedPackage is not null),
            new("lighting-selected", selected is null ? "No lighting mod selected" : "Selected: " + selected.Name, selected is null ? "Choose an existing package below or create one." : "Installed activation: " + (installed?.RequestedEnabled == true ? "Enabled" : "Disabled") + ". Preview does not enable or reorder mods.", Enabled: false),
            new("lighting-open", "Open current scene light editor (" + lightingShortcuts.Describe(LightingEditorCommand.OpenEditor) + ")", "Closing the editor resumes gameplay and leaves authoring enabled.", Action: new("lighting-authoring", "open"), Enabled: lightingAuthoringEnabled && LightingEditorAvailable),
            new("lighting-activate", "Enable selected mod for normal play", "Enable the saved package through the installed mod configuration.", Action: new("lighting-authoring", "enable"), Enabled: selected is not null && installed?.RequestedEnabled != true),
            new("lighting-shortcuts", "Editor shortcuts", "Rebind all commands, including opening and hiding the editor.", Destination: "lighting-shortcuts")
        ];
        foreach (ModPackage package in resourceHost.Mods.InstalledPackages.Where(package => package.HasLighting))
        {
            bool writable = FieldLightingPackageStore.CanEdit(package, out string? reason);
            entries.Add(new("lighting-package-" + package.Id, (selected?.Id == package.Id ? "Selected · " : "") + package.Name, writable ? "Edit this package without changing installed activation." : reason ?? "A writable copy is required.", Action: new("lighting-authoring", (writable ? "select:" : "copy:") + package.Id)));
        }
        if (lightingNotice.Length > 0)
        {
            entries.Add(new("lighting-notice", lightingNotice, "", Enabled: false));
        }
        return entries;
    }

    private IReadOnlyList<MenuEntry> LightingShortcutEntries()
    {
        List<MenuEntry> entries = Enum.GetValues<LightingEditorCommand>().Select(command => new MenuEntry("lighting-key-" + command, command + ": " + lightingShortcuts.Describe(command), lightingRebinding == command ? "Release all keys, then press the new shortcut." : "Select to capture a new shortcut.", Action: new("lighting-authoring", "bind:" + command))).ToList();
        entries.Add(new("lighting-defaults", "Restore defaults", "Restore the standard lighting editor shortcuts.", Action: new("lighting-authoring", "defaults")));
        if (lightingNotice.Length > 0)
        {
            entries.Add(new("lighting-notice", lightingNotice, "", Enabled: false));
        }
        return entries;
    }

    private void LightingAuthoringAction(string action)
    {
        if (lightingEditor is not null)
        {
            lightingEditor.RequestClose(() => LightingAuthoringAction(action));
            return;
        }
        try
        {
            if (action == "toggle")
            {
                if (!lightingAuthoringEnabled && lightingStore is null && lightingRememberedPackage is { } remembered)
                {
                    ModPackage? package = resourceHost!.Mods.InstalledPackages.FirstOrDefault(package => package.Id == remembered && package.HasLighting);
                    if (package is null)
                    {
                        lightingRememberedPackage = null;
                        lightingNotice = "The previously selected lighting mod is no longer installed. Select a package or create one.";
                        SaveLightingWorkspace();
                    }
                    else
                    {
                        SelectLightingPackage(package);
                    }
                }
                else
                {
                    lightingAuthoringEnabled = !lightingAuthoringEnabled;
                }
                InvalidateLightingPreview();
            }
            else if (action == "open")
            {
                OpenLightingEditor();
            }
            else if (action == "enable" && lightingStore is not null)
            {
                ModOperationResult result = resourceHost!.Mods.SetEnabled(lightingStore.Package.Id, true);
                if (!result.Success)
                {
                    lightingNotice = result.Error ?? "The lighting mod could not be enabled.";
                }
                else
                {
                    lightingNotice = "The saved lighting mod is enabled for normal play.";
                }
                resourceHost.Mods.Refresh();
            }
            else if (action.StartsWith("select:", StringComparison.Ordinal) || action.StartsWith("copy:", StringComparison.Ordinal))
            {
                bool copy = action.StartsWith("copy:", StringComparison.Ordinal);
                ModPackage package = resourceHost!.Mods.InstalledPackages.Single(package => package.Id == action[(copy ? 5 : 7)..]);
                if (copy)
                {
                    AdoptLightingStore(FieldLightingPackageStore.CreateWritableCopy(resourceHost.Mods.PrimaryModsDirectory, package, package.Name + " (custom)"));
                    resourceHost.Mods.Refresh();
                }
                else
                {
                    SelectLightingPackage(package);
                }
            }
            else if (action.StartsWith("bind:", StringComparison.Ordinal))
            {
                lightingRebinding = Enum.Parse<LightingEditorCommand>(action[5..]);
                lightingRebindingWaitForRelease = true;
                lightingNotice = "Release the keys, then press the new shortcut.";
            }
            else if (action == "defaults")
            {
                SaveLightingShortcuts(new());
                lightingNotice = "Default shortcuts restored.";
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException or JsonException)
        {
            lightingNotice = exception.Message;
        }
        gameMenus?.InvalidateLiveData();
    }

    private void CreateLightingPackage(string name)
    {
        try
        {
            AdoptLightingStore(FieldLightingPackageStore.Create(resourceHost!.Mods.PrimaryModsDirectory, name));
            resourceHost.Mods.Refresh();
            lightingNotice = "Created " + lightingStore!.Package.Name + ". Authoring preview is on; installed activation remains disabled.";
            gameMenus!.Open("lighting-authoring", true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            lightingNotice = exception.Message;
            gameMenus!.ReportLightingMessage(exception.Message);
        }
        ResetLightingInput();
    }

    private void SelectLightingPackage(ModPackage package)
    {
        AdoptLightingStore(FieldLightingPackageStore.Open(package));
        lightingNotice = "Authoring preview: " + package.Name;
    }

    private void AdoptLightingStore(FieldLightingPackageStore store)
    {
        lightingStore = store;
        lightingRememberedPackage = store.Package.Id;
        lightingAuthoringEnabled = true;
        InvalidateLightingPreview();
        SaveLightingWorkspace();
    }

    private void SaveLightingWorkspace()
    {
        try
        {
            string path = LightingWorkspacePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new LightingWorkspace(lightingRememberedPackage, lightingShortcuts)));
            File.Move(temporary, path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            lightingNotice = "Lighting preferences could not be saved: " + exception.Message;
        }
    }

    private void SaveLightingShortcuts(LightingShortcutSettings shortcuts)
    {
        shortcuts.Validate();
        lightingShortcuts = shortcuts;
        SaveLightingWorkspace();
        ResetLightingInput();
    }

    private void InvalidateLightingPreview()
    {
        lightingPreviewGeneration = -1;
        lightingPreviewField = null;
        lightingSavedScene = null;
    }

    private FieldLightingScene? ResolveFieldLighting()
    {
        if (session.State.Scene != SceneKind.Field || world?.Field is not { } current)
        {
            return null;
        }
        if (lightingEditor is not null)
        {
            if (current.Id != lightingEditingFieldId || !ReferenceEquals(current, lightingEditingField))
            {
                throw new InvalidOperationException("The field changed while its lighting draft was frozen.");
            }
            return lightingEditor.Draft;
        }
        if (!lightingAuthoringEnabled || lightingStore is null)
        {
            return resourceHost?.Mods.Lighting.ResolveScene(current.Name);
        }
        long generation = resourceHost!.Mods.Catalog.Generation;
        if (lightingPreviewField != current.Name || lightingPreviewGeneration != generation)
        {
            try
            {
                lightingSavedScene = lightingStore.ReadScene(current.Name);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                lightingNotice = "Lighting preview could not reload: " + exception.Message;
                if (lightingSavedScene?.Scene.FieldName != current.Name)
                {
                    lightingSavedScene = new(FieldLightingScene.Empty(current.Name), FieldLightingPackageStore.AbsentRevision);
                }
            }
            lightingPreviewField = current.Name;
            lightingPreviewGeneration = generation;
        }
        return lightingSavedScene!.Scene;
    }

    private void OpenLightingEditor()
    {
        if (lightingEditor is not null || !lightingAuthoringEnabled || lightingStore is null)
        {
            return;
        }
        if (!LightingEditorAvailable)
        {
            lightingNotice = "The light editor requires a stable FFVIII field, outside movies, transitions and battles.";
            return;
        }
        if (fieldGpu is not IFieldLightingRenderer { FieldLightingSupported: true })
        {
            lightingNotice = (fieldGpu as IFieldLightingRenderer)?.FieldLightingUnavailableReason ?? "The current graphics renderer does not support field lighting. Legacy rendering remains active.";
            gameMenus!.ReportLightingMessage(lightingNotice);
            return;
        }
        string? conflict = LightingShortcutConflict(LightingEditorCommand.OpenEditor, lightingShortcuts.Bindings[LightingEditorCommand.OpenEditor]);
        if (conflict is not null)
        {
            lightingNotice = conflict;
            gameMenus!.Open("lighting-shortcuts", true);
            return;
        }
        try
        {
            FieldAssets current = world!.Field!;
            lightingSavedScene = lightingStore.ReadScene(current.Name);
            Vector3 pivot = LightingInitialPivot(current);
            float actorScale = LightingActorHeight(current);
            lightingEditingFieldId = current.Id;
            lightingEditingField = current;
            lightingFrozenTime = gameplayClock.Total;
            if (!world.BeginLightingEdit())
            {
                lightingNotice = "Wait for the current field to be presented before opening its light editor.";
                return;
            }
            lightingEditor = new(lightingSavedScene.Scene, lightingStore.Package.Name, pivot, actorScale, SaveLightingScene, CloseLightingEditor, lightingShortcuts);
            lightingEditor.SettingsChanged += SaveLightingShortcuts;
            lightingEditor.ShortcutConflict = LightingShortcutConflict;
            lightingEditor.ReadClipboard = authoringClipboard.Paste;
            lightingEditor.WriteClipboard = authoringClipboard.Copy;
            lightingEditor.SetProjection(ProjectLightingPoint, PickLightingGround, LightingScreenRay);
            gameMenus?.Close();
            presentationTiming.DiscardPendingTime();
            ResetLightingInput();
            SynchronizeUiMoviePause();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or JsonException)
        {
            lightingNotice = exception.Message;
            world?.EndLightingEdit();
        }
    }

    private Vector3 LightingInitialPivot(FieldAssets current)
    {
        if (events?.PlayerActor is { } actor)
        {
            return new(actor.X, actor.Y, actor.Z);
        }
        if (current.Walkmesh.Triangles.Count > 0)
        {
            var triangle = current.Walkmesh.Triangles[current.Walkmesh.Triangles.Count / 2];
            return (triangle.A + triangle.B + triangle.C) / 3;
        }
        return Vector3.Zero;
    }

    private float LightingActorHeight(FieldAssets current)
    {
        int modelId = events?.PlayerActor?.RenderModelId ?? -1;
        if ((uint)modelId < current.Models.Count)
        {
            Vector3[] pose = current.Models[modelId].Pose(0, 0);
            if (pose.Length > 0)
            {
                float height = pose.Max(point => point.Z) - pose.Min(point => point.Z);
                if (height > 0 && float.IsFinite(height))
                {
                    return height;
                }
            }
        }
        return 100;
    }

    private string? SaveLightingScene(FieldLightingScene scene)
    {
        try
        {
            lightingSavedScene = lightingStore!.SaveScene(scene, lightingSavedScene!.Revision);
            lightingPreviewField = scene.FieldName;
            lightingPreviewGeneration = resourceHost!.Mods.Catalog.Generation;
            resourceHost.Mods.Refresh();
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            return exception.Message;
        }
    }

    private void CloseLightingEditor()
    {
        lightingEditor = null;
        lightingEditingField = null;
        world?.EndLightingEdit();
        InvalidateLightingPreview();
        presentationTiming.DiscardPendingTime();
        ResetElapsedTime();
        ResetLightingInput();
        drainUiButtons = true;
        SynchronizeUiMoviePause();
    }

    private void ResetLightingInput()
    {
        KeyboardState keys = physicalInput.ReadKeyboard();
        lightingPreviousKeys = keys;
        lightingEditor?.ResetInput(keys, physicalInput.ReadMouse());
        keyboard.Read(keys, physicalInput.ReadController(), false);
        keyboard.SuppressHeldUiInput();
        movieSkipGesture.Reset();
    }

    private bool UpdateLightingAuthoring(GameTime time)
    {
        KeyboardState keys = physicalInput.ReadKeyboard();
        if (lightingEditor is not null)
        {
            // Preparation remains responsive; publishing another package's resources is deferred until the frozen field is released.
            resourceHost?.Mods.PreparePending();
            lightingEditor.Poll(keys, physicalInput.ReadMouse(), physicalInput.CanRead, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            lightingPreviousKeys = keys;
            if (pendingCapture is null)
            {
                controls?.Poll();
            }
            presentationTiming.DiscardPendingTime();
            presentationPacer.WaitForNextFrame(appliedFrameLimit ?? 60);
            controllerFeedback?.Update(session.State.VibrationProcess, false, keyboard.Profile.Controller.VibrationEnabled);
            fieldAudio?.Update();
            return true;
        }
        if (lightingRebinding is not null)
        {
            UpdateLightingShortcutCapture(keys);
            lightingPreviousKeys = keys;
            return false;
        }
        bool open = physicalInput.CanRead && lightingShortcuts.Bindings[LightingEditorCommand.OpenEditor].Matches(keys) && !lightingShortcuts.Bindings[LightingEditorCommand.OpenEditor].Matches(lightingPreviousKeys);
        lightingPreviousKeys = keys;
        if (open && lightingAuthoringEnabled && gameMenus?.IsTypingName != true && gameMenus?.IsLightingNameEditing != true && gameMenus?.IsCapturingControls != true && !localizationWorkbench.OwnsKeyboard)
        {
            OpenLightingEditor();
            return lightingEditor is not null;
        }
        return false;
    }

    private void UpdateLightingShortcutCapture(KeyboardState keys)
    {
        if (lightingRebindingWaitForRelease)
        {
            lightingRebindingWaitForRelease = keys.GetPressedKeys().Length != 0;
            return;
        }
        Keys key = keys.GetPressedKeys().FirstOrDefault(key => key is not (Keys.LeftControl or Keys.RightControl or Keys.LeftShift or Keys.RightShift or Keys.LeftAlt or Keys.RightAlt));
        if (key == Keys.None)
        {
            return;
        }
        LightingEditorCommand command = lightingRebinding!.Value;
        AuthoringKeyChord chord = new(key, keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl), keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift), keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
        lightingRebinding = null;
        try
        {
            string? conflict = LightingShortcutConflict(command, chord);
            if (conflict is not null)
            {
                throw new InvalidDataException(conflict);
            }
            SaveLightingShortcuts(lightingShortcuts.Rebind(command, chord));
            lightingNotice = command + " is now " + chord + ".";
        }
        catch (InvalidDataException exception)
        {
            lightingNotice = exception.Message;
        }
        keyboard.Read(keys, physicalInput.ReadController(), false);
        keyboard.SuppressHeldUiInput();
        gameMenus?.InvalidateLiveData();
    }

    private string? LightingShortcutConflict(LightingEditorCommand command, AuthoringKeyChord chord)
    {
        if (command != LightingEditorCommand.OpenEditor)
        {
            return null;
        }
        foreach (ControlAction action in Enum.GetValues<ControlAction>())
        {
            foreach (InputChord binding in keyboard.Profile.GetBindings(action, ControlDevice.Keyboard).Concat(ControlsProfile.GetPermanentBindings(action, ControlDevice.Keyboard)))
            {
                if (binding.Keys.Contains(chord.Key) && chord.Matches(new KeyboardState(binding.Keys)))
                {
                    return "Opening shortcut conflicts with " + action + ". Choose another chord.";
                }
            }
        }
        return null;
    }

    private Vector2 FieldPixelToLightingDesign(Vector2 point)
    {
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        PixelBounds fit = ViewportLayout.Fit(world!.RenderWidth, world.RenderHeight, bounds.Width, bounds.Height);
        float scale = Math.Min(bounds.Width / LightingEditorController.DesignWidth, bounds.Height / LightingEditorController.DesignHeight);
        Vector2 offset = new((bounds.Width - LightingEditorController.DesignWidth * scale) / 2, (bounds.Height - LightingEditorController.DesignHeight * scale) / 2);
        return (new Vector2(fit.X, fit.Y) + point * new Vector2(fit.Width / (float)world.RenderWidth, fit.Height / (float)world.RenderHeight) - offset) / scale;
    }

    private Vector2 LightingDesignToFieldPixel(Vector2 point)
    {
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        PixelBounds fit = ViewportLayout.Fit(world!.RenderWidth, world.RenderHeight, bounds.Width, bounds.Height);
        float scale = Math.Min(bounds.Width / LightingEditorController.DesignWidth, bounds.Height / LightingEditorController.DesignHeight);
        Vector2 offset = new((bounds.Width - LightingEditorController.DesignWidth * scale) / 2, (bounds.Height - LightingEditorController.DesignHeight * scale) / 2);
        return (point * scale + offset - new Vector2(fit.X, fit.Y)) * new Vector2(world.RenderWidth / (float)fit.Width, world.RenderHeight / (float)fit.Height);
    }

    private Vector2? ProjectLightingPoint(Vector3 point)
    {
        return world?.ProjectLightingPoint(point) is { } projected ? FieldPixelToLightingDesign(projected) : null;
    }

    private Vector3? PickLightingGround(Vector2 point)
    {
        return world!.TryPickLightingGround(LightingDesignToFieldPixel(point), out Vector3 position) ? position : null;
    }

    private LightingEditorRay? LightingScreenRay(Vector2 point)
    {
        return world!.TryLightingScreenRay(LightingDesignToFieldPixel(point), out Vector3 origin, out Vector3 direction) ? new(origin, direction) : null;
    }

    private void DrawLightingAuthoring(Rectangle bounds)
    {
        if (uiCanvas is null)
        {
            return;
        }
        if (lightingEditor is not null)
        {
            lightingEditor.RendererStatus = world?.LightingStatus ?? "";
            if (lightingEditor.OverlayVisible)
            {
                UiView view = lightingEditor.BuildView();
                view.Resize(bounds.Width, bounds.Height);
                view.Draw(uiCanvas, uiTheme);
            }
        }
        else if (lightingAuthoringEnabled && lightingStore is not null && session.State.Scene == SceneKind.Field && gameMenus?.IsOpen != true)
        {
            uiCanvas.Text("Authoring preview: " + lightingStore.Package.Name + " · " + lightingShortcuts.Describe(LightingEditorCommand.OpenEditor) + " — Edit scene lights", 16, new(bounds.X + 12, bounds.Bottom - 30, bounds.Width - 24, 26), new UiColor(255, 224, 156));
        }
        if (lightingEditor is null && world?.LightingUnavailableReason is { } unavailable)
        {
            uiCanvas.Text("Field lighting unavailable: " + unavailable, 16, new(bounds.X + 12, bounds.Bottom - 58, bounds.Width - 24, 26), new UiColor(255, 199, 118));
        }
    }
}
