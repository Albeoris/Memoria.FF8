namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState ApplyNativeShotReadiness(NativeBattleGraphicsState graphics, uint callback)
    {
        NativeBattleModelLifecycleServices frame = hostedFrameServices ?? throw new InvalidOperationException("Shot readiness requires current frame facts.");
        if (frame.Shot is not { } shot || shot.Callback != callback)
        {
            throw new InvalidDataException("Native Shot readiness requires its single current widget owner.");
        }
        frame.Emit(new NativeBattleShotReadinessReached(shot));
        if (shot.State is not { } state)
        {
            return graphics;
        }
        NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidDataException("Native Shot requires its UI service owner.");
        NativeBattleShotWidgetFacts facts = new(shot.Owner, shot.ActorId, state);
        NativeBattleShotWidgetStep step;
        if (callback == 0x48D1A0)
        {
            NativeBattleShotWidgetState widget = NativeBattleShotWidgetState.Register(state.InitialTimer);
            step = widget.Advance(ui.Frame33 == true, false, false, state.RemainingAmmo);
            graphics = SetNativeShotWindow(graphics, true);
        }
        else
        {
            NativeBattleShotWidgetState widget = (state.Widget ?? throw new InvalidDataException("Shot rearm requires its registered widget.")).Rearm();
            step = new(widget, false, false, false, false);
        }
        frame.EmitShotWidget(new(facts, step));
        return graphics;
    }

    private void AdvanceNativeShotWindow(Action<NativeBattleShotWidgetUpdated> publish)
    {
        if (NativeGraphics is not { UiServices.Windows: { } windows } graphics || windows.Rows[6].Update0 != 0x4AD8D0)
        {
            return;
        }
        if (services?.ReadShotWidget() is not { State.Widget: { } widget } facts)
        {
            NativeGraphics = SetNativeShotWindow(graphics, false);
            return;
        }
        NativeBattleShotWidgetStep step = widget.Advance(graphics.UiServices!.Frame33 == true,
            facts.State.FireRequested, facts.State.Phase == BattleNativeShotPhase.Ending,
            facts.State.FireRequested ? checked((byte)(facts.State.RemainingAmmo - 1)) : facts.State.RemainingAmmo);
        if (step.Unregistered)
        {
            NativeGraphics = SetNativeShotWindow(graphics, false);
        }
        publish(new(facts, step));
    }

    private static NativeBattleGraphicsState SetNativeShotWindow(NativeBattleGraphicsState graphics, bool registered)
    {
        NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidDataException("Native Shot lost its UI service owner.");
        NativeBattleUiWindows windows = ui.Windows ?? throw new InvalidDataException("Native Shot lost its callback window bank.");
        NativeBattleUiWindow window = windows.Rows[6];
        if (registered && window.Update0 is not (0 or 0x4AD8D0))
        {
            throw new InvalidOperationException("Native Shot cannot register over another window6 callback.");
        }
        window = window with { Update0 = registered ? 0x4AD8D0u : 0, Draw8 = registered ? 0x4ADBF0u : 0 };
        ui = registered ? ui.SetFlags(0x1800) : ui.SetFlags(0, 0x1800);
        return graphics with { UiServices = ui with { Windows = windows with { Rows = windows.Rows.SetItem(6, window) } } };
    }
}
