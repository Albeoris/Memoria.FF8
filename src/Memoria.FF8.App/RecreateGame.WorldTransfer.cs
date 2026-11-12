using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private WorldTransferPresentation? worldTransfer;
    private bool worldTransferIncoming;
    private Task? worldTransferAuthoringSave;

    private void BeginWorldTransfer(CombinedSessionSave destination)
    {
        if (worldTransfer is not null)
        {
            throw new InvalidOperationException("A world transfer is already in progress.");
        }
        UpdateAchievements(force: true);
        WorldProcessHandoff transfer = WorldProcessHandoff.Start(options, destination, windowMode!.CaptureTransfer());
        worldTransferIncoming = false;
        worldTransferAuthoringSave = null;
        worldTransfer = CreateWorldTransfer(transfer, incoming: false);
        PauseWorldTransferAudio();
    }

    private WorldTransferPresentation CreateWorldTransfer(WorldProcessHandoff transfer, bool incoming)
    {
        return new WorldTransferPresentation(transfer, incoming, () =>
        {
            DisposeAchievements();
            achievementsSuspended = true;
        }, () =>
        {
            achievementsSuspended = false;
            InitializeAchievements();
            InitializeAuthoringWorkspace();
        }, () => windowMode!.RevealTransferChild(), () =>
        {
            // The outgoing workspace was durably saved before the irreversible platform shutdown.
            authoringExitAllowed = true;
            ExitGame();
        });
    }

    private void PrepareIncomingWorldTransfer()
    {
        if (WorldProcessHandoff.Current is not { } transfer)
        {
            return;
        }
        if (InFf7)
        {
            ff7Scene!.PrepareMappedCombatForSwitch();
            crossover = ff7Scene.Capture();
        }
        else
        {
            world ??= CreateWorld();
            world.EnsureField();
        }
        worldTransferIncoming = true;
        worldTransfer = CreateWorldTransfer(transfer, incoming: true);
        PauseWorldTransferAudio();
    }

    private void PauseWorldTransferAudio()
    {
        fieldAudio?.SetFocusPaused(true);
        ff7Scene?.SetFocusPaused(true);
        SynchronizeUiMoviePause();
        controllerFeedback?.Update(session.State.VibrationProcess, false, keyboard.Profile.Controller.VibrationEnabled);
    }

    private bool UpdateWorldTransfer(GameTime time)
    {
        if (worldTransfer is null)
        {
            return false;
        }
        try
        {
            if (!worldTransferIncoming && !PrepareWorldTransferAuthoring())
            {
                presentationPacer.WaitForNextFrame(60);
                return true;
            }
            worldTransfer.Update(time.ElapsedGameTime);
        }
        catch (Exception exception) when (!worldTransferIncoming && !worldTransfer.Committed && exception is IOException or UnauthorizedAccessException or InvalidOperationException or OperationCanceledException)
        {
            worldTransfer.Dispose();
            worldTransfer = null;
            fieldAudio?.SetFocusPaused(focusPaused || InFf7);
            ff7Scene?.SetFocusPaused(focusPaused || updateSave.Paused);
            SynchronizeUiMoviePause();
            ShowSaveNotice("World transfer failed: " + exception.Message, 8);
            ResetElapsedTime();
            return true;
        }
        if (worldTransfer.Complete)
        {
            worldTransfer.Dispose();
            worldTransfer = null;
            fieldAudio?.SetFocusPaused(focusPaused || InFf7);
            ff7Scene?.SetFocusPaused(focusPaused || updateSave.Paused);
            SynchronizeUiMoviePause();
            keyboard.SuppressHeldUiInput();
            drainUiButtons = true;
            ResetElapsedTime();
            return true;
        }
        presentationPacer.WaitForNextFrame(60);
        return true;
    }

    private bool PrepareWorldTransferAuthoring()
    {
        if (!authoringWorkspaceInitialized)
        {
            return true;
        }
        if (worldTransferAuthoringSave is null)
        {
            if (authoringWorkspaceLoading is not null || authoringWorkspaceSaving is not null)
            {
                return false;
            }
            SaveAuthoringWorkspace();
            worldTransferAuthoringSave = authoringWorkspaceSaving!;
        }
        if (!worldTransferAuthoringSave.IsCompleted)
        {
            return false;
        }
        worldTransferAuthoringSave.GetAwaiter().GetResult();
        return true;
    }

    protected override void Draw(GameTime gameTime)
    {
        DrawGameFrame(gameTime);
        if (worldTransfer is not null)
        {
            if (fieldFaulted || fieldRenderFaulted || ff7Scene?.Error is not null)
            {
                throw new InvalidOperationException("The destination world could not prepare its presentation.");
            }
            worldTransfer.Draw(batch, pixel, GraphicsDevice.Viewport.Bounds);
        }
    }
}
