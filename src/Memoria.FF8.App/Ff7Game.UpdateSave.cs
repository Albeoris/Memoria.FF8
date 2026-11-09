using Memoria.FF7.Runtime;
using Memoria.FF8.Platform;

namespace Memoria.FF8;

internal sealed partial class Ff7Game
{
    private ControlQueue? updateControls;
    private UpdateSaveHandoff updateSave = null!;

    private void InitializeUpdateHandoff()
    {
        updateSave = new UpdateSaveHandoff(() =>
        {
            if (!scene.CanSave)
            {
                throw new InvalidOperationException("Wait for an FFVII save-enabled boundary before preparing an update.");
            }
        }, path =>
        {
            CombinedSessionSave saved = scene.Capture();
            using Ff7Session restored = Ff7Session.Restore(installation, saved.Ff7!);
            CombinedSessionCodec.Save(path, saved);
        });
        if (launchOptions?.UpdateControlDirectory is string directory)
        {
            updateControls = new ControlQueue(directory, DispatchUpdateControl);
        }
    }

    private object DispatchUpdateControl(ControlRequest request)
    {
        switch (request.Command.ToLowerInvariant())
        {
            case "status":
                return new { Game = "FFVII", UpdatePaused = updateSave.Paused, scene.CanSave };
            case "save-for-update":
                return updateSave.Save(request.Path, request.ExpiresAt);
            case "cancel-update":
                updateSave.Cancel();
                return new { Resumed = true };
            case "graphics-restart":
                return GraphicsRestartHandoff.Request(GraphicsBackendSelection.Parse(request.Name ?? throw new ArgumentException("A graphics backend is required.")), request.ExpiresAt, updateSave.Paused, (path, deadline) => updateSave.Save(path, deadline), updateSave.Cancel, Exit, scene.IsTitle ? "Title" : "Field", 0);
            case "quit":
                Exit();
                return new { Exiting = true };
            default:
                throw new ArgumentException("Update commands: status, save-for-update, cancel-update, graphics-restart, quit.");
        }
    }
}
