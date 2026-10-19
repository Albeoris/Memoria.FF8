using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly PresentationTiming presentationTiming = new();
    private readonly BattlePresentationPacer battlePresentationPacer = new();
    private int? appliedFrameLimit;
    private bool appliedNativeBattlePacing;

    private void ApplyDisplaySettings()
    {
        bool menu = startup.Active || session.State.Scene == SceneKind.Title || gameMenus?.IsOpen == true || saveMenu.IsOpen || battleDebug is not null || world?.ActiveTripleTriad is not null;
        int frameLimit = PresentationTiming.FrameLimit(uiPreferences, menu, ActiveBattle is not null, session.State.Scene == SceneKind.WorldMap);
        bool nativeBattle = ActiveBattle?.NativeGraphics is not null;
        if (appliedFrameLimit != frameLimit || appliedNativeBattlePacing != nativeBattle)
        {
            // Native battles yield to Draw between bounded catch-up batches instead of letting fixed-step catch-up starve presentation.
            IsFixedTimeStep = !nativeBattle && frameLimit != 0;
            if (frameLimit != 0)
            {
                TargetElapsedTime = TimeSpan.FromSeconds(1d / frameLimit);
            }
            MaxElapsedTime = PresentationTiming.MaximumElapsed;
            appliedFrameLimit = frameLimit;
            appliedNativeBattlePacing = nativeBattle;
            battlePresentationPacer.Reset();
        }
        if (graphics.SynchronizeWithVerticalRetrace != uiPreferences.VSync)
        {
            graphics.SynchronizeWithVerticalRetrace = uiPreferences.VSync;
            graphics.ApplyChanges();
        }
    }

    private void UpdateDisplayFrame(GameTime gameTime)
    {
        if (appliedNativeBattlePacing)
        {
            battlePresentationPacer.WaitForNextFrame(appliedFrameLimit ?? 60);
        }
        long started = Stopwatch.GetTimestamp();
        TimeSpan workBudget = appliedFrameLimit is > 0 ? TimeSpan.FromSeconds(1d / appliedFrameLimit.Value) : PresentationTiming.SimulationStep;
        presentationTiming.Accumulate(gameTime.ElapsedGameTime);
        if (pendingCapture is not null)
        {
            return;
        }
        while (true)
        {
            int nativeCallbackRate = ActiveBattle?.NativeGraphics is null ? 0 : BattleSession.FramesPerSecond * gameplayClock.SpeedMultiplier;
            TimeSpan step = PresentationTiming.UpdateStep(appliedFrameLimit ?? 60, nativeCallbackRate);
            if (!presentationTiming.TryStep(step))
            {
                break;
            }
            UpdateGameFrame(new GameTime(presentationTiming.SimulationTotal, step, gameTime.IsRunningSlowly));
            if (battleEntryTransition is not null)
            {
                break;
            }
            if (pendingCapture is not null)
            {
                break;
            }
            if (appliedNativeBattlePacing && Stopwatch.GetElapsedTime(started) >= workBudget)
            {
                // The remaining elapsed-time debt stays in presentationTiming for the next display frame.
                break;
            }
        }
    }
}
