using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private void DrawNativeShot(IUiCanvas canvas, UiTheme theme, BattleNativeShotState state)
    {
        BattleTimedInputUi ui = controller.ShotUi ?? new(BattleTimedInputUiPhase.Active);
        float reveal = ui.Reveal(BattleSession.FramesPerSecond);
        if (reveal <= 0)
        {
            return;
        }
        // TODO(TD-009): Present the selected native target mask; interactive native aiming remains a separate widget contract.
        canvas.PushClip(Rect(36, 492, 726 * reveal, 156 * reveal));
        try
        {
            theme.Panel(canvas, Rect(36, 492, 726, 156));
            string targets = string.Join(", ", controller.ShotTargets.Select(controller.CombatantDisplayName));
            Text(canvas, theme, controller.SystemText("battle.hud.target", "TARGET") + ": " + targets, 54, 504, 540, 30);
            int timer = Math.Max(0, (int)state.RemainingTimer);
            Text(canvas, theme, $"{timer / BattleSession.FramesPerSecond}.{100 * (timer % BattleSession.FramesPerSecond) / BattleSession.FramesPerSecond:00}", 636, 504, 108, 30);
            Text(canvas, theme, controller.FormatSystemText("battle.ui.shot.ammo", "Ammo: {0}", state.RemainingAmmo), 54, 540, 480, 28);
            bool ready = state.Phase == BattleNativeShotPhase.Ready && state.RemainingTimer > 0 && state.RemainingAmmo > 0;
            theme.Panel(canvas, Rect(42, 576, 342, 60));
            theme.Panel(canvas, Rect(414, 576, 342, 60));
            Text(canvas, theme, controller.FormatSystemText("battle.ui.shot.fire", "{0}: Fire", ButtonPromptText.Mark(controller.GunbladeBindingHint)), 60, 591, 306, 30, muted: !ready);
            Text(canvas, theme, controller.FormatSystemText("battle.ui.shot.finish", "{0}: Finish", ButtonPromptText.Mark(controller.NativeButtonBindingHint(0x10))), 432, 591, 306, 30, muted: state.Phase is BattleNativeShotPhase.Opening or BattleNativeShotPhase.Ending);
        }
        finally
        {
            canvas.PopClip();
        }
    }
}
