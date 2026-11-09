using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool graphicsRestartPromptOpen;

    private async void RequestGraphicsRestartConfirmation(GraphicsBackend backend)
    {
        if (graphicsRestartPromptOpen)
        {
            return;
        }
        graphicsRestartPromptOpen = true;
        try
        {
            bool restart = await ShowGameConfirmationAsync(new GameConfirmationRequest("Restart game now to apply?\n\nMemoria will preserve your current game in a separate standalone save and resume it in the selected graphics backend. Choose Later to keep playing.", "Graphics backend - requires restart", "Later", "Restart now", ""));
            if (restart)
            {
                gameMenus?.Close();
                drainUiButtons = false;
                RestartGraphicsBackend(backend, DateTimeOffset.UtcNow.AddSeconds(30));
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            await ShowGameConfirmationAsync(new GameConfirmationRequest("The game could not restart safely. Your game is still running.\n\n" + exception.Message + "\n\nContinue to a supported save boundary and try again, or save and restart manually.", "Graphics restart unavailable", "Continue playing", "Continue playing", ""));
        }
        finally
        {
            graphicsRestartPromptOpen = false;
        }
    }

    private object RestartGraphicsBackend(GraphicsBackend backend, DateTimeOffset? expiresAt)
    {
        return GraphicsRestartHandoff.Request(backend, expiresAt, updateSave.Paused, (path, deadline) => session.State.Scene == SceneKind.Title ? GraphicsRestartSnapshot.SaveTitle(session, path, deadline) : updateSave.Save(path, deadline), () =>
        {
            updateSave.Cancel();
            SynchronizeUiMoviePause();
        }, ExitGame, session.State.Scene.ToString(), session.State.FieldId);
    }
}
