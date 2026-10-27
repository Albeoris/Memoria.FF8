using System.Security.Cryptography;
using System.Text.Json;
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
        if (GraphicsBackendSelection.UnavailableReason(backend, HostPlatforms.Current) is string reason)
        {
            throw new PlatformNotSupportedException(reason);
        }
        string? requestPath = Environment.GetEnvironmentVariable("MEMORIA_GRAPHICS_RESTART_REQUEST");
        string? token = Environment.GetEnvironmentVariable("MEMORIA_GRAPHICS_RESTART_TOKEN");
        if (string.IsNullOrWhiteSpace(requestPath) || !Path.IsPathFullyQualified(requestPath) || string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Start the game through the Memoria launcher or graphics dispatcher to enable automatic restart.");
        }
        if (expiresAt is null || expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new TimeoutException("The graphics restart request expired. Try again.");
        }
        if (updateSave.Paused)
        {
            throw new InvalidOperationException("Another save handoff is already prepared. Finish or cancel it before restarting graphics.");
        }
        string directory = Path.GetDirectoryName(requestPath)!;
        string statePath = Path.Combine(directory, "graphics-restart-" + Guid.NewGuid().ToString("N") + ".ff8state");
        string temporary = requestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            UpdateSaveResult saved = session.State.Scene == SceneKind.Title ? GraphicsRestartSnapshot.SaveTitle(session, statePath, expiresAt.Value) : updateSave.Save(statePath, expiresAt);
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(saved.Path))).ToLowerInvariant();
            var receipt = new { schemaVersion = 1, processId = Environment.ProcessId, token, backend = backend.ToString(), statePath = saved.Path, sha256 = hash, scene = session.State.Scene.ToString(), fieldId = session.State.FieldId };
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, receipt);
                stream.Flush(true);
            }
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                throw new TimeoutException("The graphics restart request expired before handoff. Try again.");
            }
            File.Move(temporary, requestPath);
            Console.WriteLine($"Graphics restart: requested={backend} state={saved.Path} scene={session.State.Scene} field={session.State.FieldId}");
            Exit();
            return new { Restarting = true, Path = saved.Path, Backend = backend.ToString() };
        }
        catch
        {
            updateSave.Cancel();
            SynchronizeUiMoviePause();
            throw;
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
