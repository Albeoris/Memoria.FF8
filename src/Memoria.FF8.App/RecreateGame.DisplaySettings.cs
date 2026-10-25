using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly PresentationTiming presentationTiming = new();
    private readonly PresentationPacer presentationPacer = new();
    private int? appliedFrameLimit;
    private bool appliedNativeBattlePacing;
    private int appliedMsaaSamples;

    private void ApplyDisplaySettings()
    {
        windowMode?.Apply(uiPreferences.BorderlessWindow);
        bool menu = startup.Active || session.State.Scene == SceneKind.Title || gameMenus?.IsOpen == true || saveMenu.IsOpen || battleDebug is not null || world?.ActiveTripleTriad is not null;
        int frameLimit = PresentationTiming.FrameLimit(uiPreferences, menu, ActiveBattle is not null, session.State.Scene == SceneKind.WorldMap);
        bool nativeBattle = ActiveBattle?.NativeGraphics is not null;
        if (appliedFrameLimit != frameLimit || appliedNativeBattlePacing != nativeBattle)
        {
            // Simulation has its own clocks. Every host tick must reach Draw, including under load.
            IsFixedTimeStep = false;
            if (frameLimit != 0)
            {
                TargetElapsedTime = TimeSpan.FromSeconds(1d / frameLimit);
            }
            MaxElapsedTime = PresentationTiming.MaximumElapsed;
            appliedFrameLimit = frameLimit;
            appliedNativeBattlePacing = nativeBattle;
            presentationPacer.Reset();
        }
        bool samplesChanged = appliedMsaaSamples != uiPreferences.Graphics.MsaaSamples;
        if (graphics.SynchronizeWithVerticalRetrace != uiPreferences.VSync || samplesChanged)
        {
            graphics.SynchronizeWithVerticalRetrace = uiPreferences.VSync;
            graphics.PreferMultiSampling = uiPreferences.Graphics.MsaaSamples > 0;
            graphics.ApplyChanges();
            appliedMsaaSamples = uiPreferences.Graphics.MsaaSamples;
            if (samplesChanged)
            {
                fieldGpu?.InvalidatePresentation();
                battleGpu?.InvalidatePresentation();
            }
        }
    }

    private void UpdateDisplayFrame(GameTime gameTime)
    {
        presentationPacer.WaitForNextFrame(appliedFrameLimit ?? 60);
        TimeSpan workBudget = appliedFrameLimit is > 0 ? TimeSpan.FromSeconds(1d / appliedFrameLimit.Value) : PresentationTiming.SimulationStep;
        presentationTiming.Accumulate(gameTime.ElapsedGameTime);
        if (pendingCapture is not null)
        {
            return;
        }
        presentationTiming.RunUpdates(workBudget, () =>
        {
            double nativeCallbackRate = ActiveBattle?.NativeGraphics is null ? 0 : NativeBattleGraphicsPresentationClock.CallbacksPerSecond * gameplayClock.SpeedMultiplier;
            return PresentationTiming.UpdateStep(appliedFrameLimit ?? 60, nativeCallbackRate);
        }, step =>
        {
            UpdateGameFrame(new GameTime(presentationTiming.SimulationTotal, step, gameTime.IsRunningSlowly));
            return battleEntryTransition is null && pendingCapture is null;
        });
    }
}
