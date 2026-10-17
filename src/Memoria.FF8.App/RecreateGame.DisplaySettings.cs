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
            // MonoGame owns presentation pacing; gameplay always consumes the same 60 Hz steps.
            // Faster presentation repeats the latest state without interpolating native simulation.
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
            Rectangle bounds = Window.ClientBounds;
            graphics.PreferredBackBufferWidth = bounds.Width;
            graphics.PreferredBackBufferHeight = bounds.Height;
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
        while (presentationTiming.TryStep())
        {
            UpdateGameFrame(new GameTime(presentationTiming.SimulationTotal, PresentationTiming.SimulationStep, gameTime.IsRunningSlowly));
            if (pendingCapture is not null)
            {
                break;
            }
        }
    }
}
