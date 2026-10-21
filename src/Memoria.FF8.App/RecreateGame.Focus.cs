using System.Diagnostics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool focusPaused;
    private bool windowActivityInitialized;
    private bool discardFocusElapsed;
    private bool focusDisposed;

    protected override void OnActivated(object sender, EventArgs args)
    {
        base.OnActivated(sender, args);
        SetWindowActive(true);
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        SetWindowActive(false);
        base.OnDeactivated(sender, args);
    }

    internal void SetWindowActive(bool active)
    {
        if (focusDisposed)
        {
            return;
        }
        windowActivityInitialized = true;
        // Explicitly controlled diagnostic hosts must also work with a hidden window.
        bool pause = !active && options.ControlDirectory is null;
        if (focusPaused == pause)
        {
            return;
        }
        focusPaused = pause;
        presentationTiming.DiscardPendingTime();
        presentationPacer.Reset();
        ResetElapsedTime();
        movieSkipGesture.Reset();
        keyboard.Read(Keyboard.GetState(), controller?.Read() ?? default, false);
        fieldAudio?.SetFocusPaused(pause);
        SynchronizeUiMoviePause();
        controllerFeedback?.Update(session.State.VibrationProcess, false, keyboard.Profile.Controller.VibrationEnabled);
        if (!pause)
        {
            // Game.Tick may already have sampled its elapsed time before the activation event.
            discardFocusElapsed = true;
            battleEntryCallbackStarted = Stopwatch.GetTimestamp();
        }
    }

    private bool UpdateFocusPause()
    {
        if (!windowActivityInitialized)
        {
            SetWindowActive(IsActive);
        }
        if (focusPaused)
        {
            presentationPacer.WaitForNextFrame(30);
            SuppressDraw();
            return true;
        }
        if (discardFocusElapsed)
        {
            discardFocusElapsed = false;
            presentationTiming.DiscardPendingTime();
            ResetElapsedTime();
            return true;
        }
        return false;
    }

    protected override bool BeginDraw()
    {
        return !focusPaused && base.BeginDraw();
    }
}
