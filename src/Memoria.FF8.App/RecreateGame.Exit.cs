using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly GameConfirmationOverlay windowCloseConfirmation = new();
    private Task<bool>? windowCloseDecision;
    private bool windowCloseApproved;
    private bool windowCloseWithoutAuthoringSave;
    private bool authoringExitSaveFailed;

    private void UpdateDiagnosticExit(GameTime time)
    {
        updateFrames++;
        if (options.ExitAfterFrames is int frames && updateFrames >= frames || options.ExitAfterSeconds is double seconds && time.TotalGameTime.TotalSeconds >= seconds)
        {
            ExitGame();
        }
    }

    private void ExitGame()
    {
        windowCloseApproved = true;
        Exit();
    }

    private bool ConfirmWindowClose()
    {
        if (!authoringWorkspaceInitialized || windowCloseApproved && (!authoringExitRequested || authoringExitAllowed))
        {
            return true;
        }
        ShowWindowCloseConfirmation();
        return false;
    }

    private void ShowWindowCloseConfirmation()
    {
        if (windowCloseDecision is null)
        {
            keyboard.SuppressHeldUiInput();
            windowCloseWithoutAuthoringSave = authoringExitRequested || authoringExitSaveFailed;
            string message = windowCloseWithoutAuthoringSave
                ? LocalizeAuthored("game.exit.pending-save", "The editor has not finished saving. Exit without waiting? Unsaved game progress and translation changes may be lost.")
                : LocalizeAuthored("game.exit.message", "Do you wish to exit the game? Unsaved game progress will be lost.");
            windowCloseDecision = windowCloseConfirmation.ShowAsync(new GameConfirmationRequest(message, LocalizeAuthored("game.exit.title", "Exit game"), LocalizeAuthored("game.exit.cancel", "Cancel"), LocalizeAuthored("game.exit.accept", "Exit"), ""));
        }
    }

    private bool UpdateWindowCloseConfirmation()
    {
        if (windowCloseDecision is null)
        {
            return false;
        }
        if (windowCloseDecision.IsCompleted)
        {
            bool accepted = windowCloseDecision.GetAwaiter().GetResult();
            windowCloseDecision = null;
            keyboard.SuppressHeldUiInput();
            if (accepted)
            {
                authoringExitAllowed |= windowCloseWithoutAuthoringSave;
                ExitGame();
            }
            return true;
        }
        UpdateGameConfirmation(windowCloseConfirmation);
        return true;
    }

    private void DrawWindowCloseConfirmation(Rectangle bounds)
    {
        if (uiCanvas is not null)
        {
            windowCloseConfirmation.Draw(uiCanvas, uiTheme, new(bounds.X, bounds.Y, bounds.Width, bounds.Height), uiPreferences.UiFontFamily);
        }
    }
}
