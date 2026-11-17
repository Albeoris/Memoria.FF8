using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private object DispatchLightingControl(ControlRequest request)
    {
        string command = request.Command.ToLowerInvariant();
        if (command == "lighting-status")
        {
            return LightingAuthoringStatus();
        }
        if (lightingEditor is { OverlayVisible: false } && command != "lighting-visibility")
        {
            throw new InvalidOperationException("Show the lighting editor interface before editing its suspended session.");
        }
        if (lightingEditor is { DiscardConfirmation: true } && command is not ("lighting-discard" or "lighting-continue" or "lighting-visibility"))
        {
            throw new InvalidOperationException("Resolve the lighting editor's discard confirmation before changing its draft.");
        }
        switch (command)
        {
            case "lighting-create":
                if (lightingEditor is not null)
                {
                    throw new InvalidOperationException("Close the current scene editor before creating a package.");
                }
                CreateLightingPackage(request.Name ?? throw new ArgumentException("A mod name is required."));
                break;
            case "lighting-select":
                LightingAuthoringAction("select:" + (request.Name ?? throw new ArgumentException("A package ID is required.")));
                break;
            case "lighting-toggle":
                LightingAuthoringAction("toggle");
                break;
            case "lighting-open":
                OpenLightingEditor();
                break;
            case "lighting-close":
                RequireLightingEditor().RequestClose();
                break;
            case "lighting-discard":
                if (RequireLightingEditor().DiscardConfirmation)
                {
                    lightingEditor!.DiscardAndClose();
                }
                break;
            case "lighting-continue":
                RequireLightingEditor().ContinueEditing();
                break;
            case "lighting-visibility":
                RequireLightingEditor().ToggleVisibility();
                break;
            case "lighting-save":
                RequireLightingEditor().Save();
                break;
            case "lighting-add":
                RequireLightingEditor().AddLight();
                break;
            case "lighting-delete":
                RequireLightingEditor().DeleteSelected();
                break;
            case "lighting-select-light":
                RequireLightingEditor().Select(request.Name);
                break;
            case "lighting-enabled":
                RequireLightingEditor().ToggleEnabled();
                break;
            case "lighting-primary":
                RequireLightingEditor().TogglePrimary();
                break;
            case "lighting-property":
                LightingEditorController editor = RequireLightingEditor();
                editor.BeginPropertyEdit(Enum.Parse<LightingProperty>(request.Name ?? throw new ArgumentException("A light property name is required."), true));
                editor.SetPropertyText(request.Ui?.Text ?? throw new ArgumentException("Ui.Text must contain the entered value."));
                editor.CommitProperty();
                break;
            case "lighting-undo":
                RequireLightingEditor().Undo();
                break;
            case "lighting-redo":
                RequireLightingEditor().Redo();
                break;
            default:
                throw new ArgumentException("Unknown lighting control command: " + request.Command);
        }
        return LightingAuthoringStatus();
    }

    private LightingEditorController RequireLightingEditor()
    {
        return lightingEditor ?? throw new InvalidOperationException("Open the current scene light editor first.");
    }

    private object LightingAuthoringStatus()
    {
        ModPackage? package = lightingStore?.Package;
        return new
        {
            AuthoringEnabled = lightingAuthoringEnabled,
            SessionActive = lightingEditor is not null,
            OverlayVisible = lightingEditor?.OverlayVisible ?? false,
            PackageId = package?.Id,
            PackageName = package?.Name,
            InstalledEnabled = resourceHost?.Mods.InstalledPackages.FirstOrDefault(installed => installed.Id == package?.Id)?.RequestedEnabled ?? false,
            FieldId = world?.Field?.Id,
            FieldName = world?.Field?.Name,
            Frame = events?.Frame,
            Dirty = lightingEditor?.IsDirty ?? false,
            lightingEditor?.DiscardConfirmation,
            lightingEditor?.SelectedId,
            Scene = lightingEditor?.Draft ?? lightingSavedScene?.Scene,
            Notice = lightingEditor?.Message ?? lightingNotice,
            Renderer = world?.LightingStatus
        };
    }
}
