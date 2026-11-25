using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly PresentationTiming presentationTiming = new();
    private bool battlePresentationUpdated;
    private bool CanAdvanceBattleRenderPhase => !focusPaused && !discardFocusElapsed && !updateSave.Paused && !userPaused && !dialogContext.IsBusy && !dialogInputBlocked && pendingCapture is null && battleEntryTransition is null && !capturingControls && !fieldFaulted && autoPlay?.Paused != true;

    private void ApplyDisplaySettings()
    {
        bool hostMenuOpen = saveMenu.IsOpen || crossoverSetup is not null;
        bool ff8Menu = hostMenuOpen || startup.Active || session.State.Scene == SceneKind.Title || gameMenus?.IsOpen == true || battleDebug is not null || world?.ActiveTripleTriad is not null;
        int frameLimit = InFf7 ? ff7Scene!.GetPresentationFrameLimit(uiPreferences, hostMenuOpen) : PresentationTiming.FrameLimit(uiPreferences, ff8Menu, ActiveBattle is not null, session.State.Scene == SceneKind.WorldMap);
        ApplyPresentationSettings(uiPreferences, frameLimit, InvalidateSceneTargets);
    }

    private void InvalidateSceneTargets()
    {
        fieldGpu?.InvalidatePresentation();
        battleGpu?.InvalidatePresentation();
        ff7Scene?.InvalidateGraphicsPresentation();
    }

    private void UpdateDisplayFrame(GameTime gameTime)
    {
        TimeSpan workBudget = appliedFrameLimit is > 0 ? TimeSpan.FromSeconds(1d / appliedFrameLimit.Value) : PresentationTiming.SimulationStep;
        presentationTiming.Accumulate(gameTime.ElapsedGameTime);
        if (pendingCapture is not null)
        {
            presentationTiming.FreezePresentationFrame();
            return;
        }
        bool updated = false;
        battlePresentationUpdated = false;
        presentationTiming.RunUpdates(workBudget, () =>
        {
            gameplayClock.Configure(uiPreferences.FastForwardMultiplier, uiPreferences.BattleSpeedPercent);
            double nativeCallbackRate = ActiveBattle is null ? 0 : BattlePresentationTiming.CallbacksPerSecond * gameplayClock.EffectiveSpeed(session.State.Scene, battle: true);
            return PresentationTiming.UpdateStep(nativeCallbackRate);
        }, step =>
        {
            updated = true;
            UpdateGameFrame(new GameTime(presentationTiming.SimulationTotal, step, gameTime.IsRunningSlowly));
            return battleEntryTransition is null && pendingCapture is null;
        });
        if ((updated && !battlePresentationUpdated) || !CanAdvanceBattleRenderPhase)
        {
            presentationTiming.FreezePresentationFrame();
        }
    }
}
