using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly PresentationTiming presentationTiming = new();
    private int? appliedFrameLimit;

    private void ApplyDisplaySettings()
    {
        bool menu = startup.Active || session.State.Scene == SceneKind.Title || gameMenus?.IsOpen == true || saveMenu.IsOpen || battleDebug is not null || world?.ActiveTripleTriad is not null;
        int frameLimit = PresentationTiming.FrameLimit(uiPreferences, menu, ActiveBattle is not null);
        if (appliedFrameLimit != frameLimit)
        {
            // MonoGame owns display pacing; native battle callbacks retain their own elapsed-time clock.
            IsFixedTimeStep = frameLimit != 0;
            if (frameLimit != 0)
            {
                TargetElapsedTime = TimeSpan.FromSeconds(1d / frameLimit);
            }
            MaxElapsedTime = PresentationTiming.MaximumElapsed;
            appliedFrameLimit = frameLimit;
        }
        if (graphics.SynchronizeWithVerticalRetrace != uiPreferences.VSync)
        {
            graphics.SynchronizeWithVerticalRetrace = uiPreferences.VSync;
            graphics.ApplyChanges();
        }
    }

    private void UpdateDisplayFrame(GameTime gameTime)
    {
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
            if (pendingCapture is not null)
            {
                break;
            }
        }
    }
}
