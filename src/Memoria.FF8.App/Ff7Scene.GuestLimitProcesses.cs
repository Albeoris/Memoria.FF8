using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private int mixedSlotsChoice;

    private CrossoverGuestHostActions? InputLimitProcess => mixedCombat?.Guests.FirstOrDefault(guest => guest.ProcessActive);

    private bool ReadGuestLimitProcess()
    {
        if (InputLimitProcess is not { } guest)
        {
            return false;
        }
        guest.SetProcessButtons(sharedInput.HeldFieldButtons);
        if (guest.ProcessFailure is { } failure)
        {
            unavailableBattleCommand = new(Ff7BattleCommand.Magic, 0, 0, true, false) { DisplayName = guest.SlotsChoosing ? guest.ProcessSlotsName : guest.DuelProcess is not null ? "Duel" : guest.ShotProcess is not null ? "Shot" : "Limit", UnsupportedReason = failure };
            guest.ClearProcessFailure();
            return true;
        }
        if (guest.SlotsChoosing && semanticInput.Cancel)
        {
            (mixedCombat?.Host ?? Session?.Battle)!.CancelGuestProcessSelection(guest.ActorSlot);
            return true;
        }
        if (guest.SlotsChoosing)
        {
            if (semanticInput.MenuVertical != 0)
            {
                mixedSlotsChoice = 1 - mixedSlotsChoice;
            }
            if (semanticInput.MenuPage != 0 || semanticInput.Confirm && mixedSlotsChoice == 0)
            {
                guest.RerollProcessSlots();
            }
            else if (semanticInput.Confirm)
            {
                guest.AcceptProcessSlots();
                mixedSlotsChoice = 0;
            }
        }
        if (guest.ShotProcess is not null)
        {
            if (semanticInput.Cancel)
            {
                guest.FinishProcessShot();
            }
            else if (sharedInput.GunbladeTriggerRequested)
            {
                guest.TriggerProcessShot();
            }
        }
        return true;
    }

    private bool DrawGuestLimitProcess(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale)
    {
        if (InputLimitProcess is not { } guest)
        {
            return false;
        }
        if (guest.SlotsChoosing && guest.SlotsProcess is { } roll)
        {
            DrawBattleChoices(canvas, theme, layout, textScale, "SLOTS", [new($"{guest.ProcessSlotsName} ×{roll.Repetitions}"), new("Do over"), new("Cast", Unsupported: guest.SlotsCastUnsupportedReason is not null)], mixedSlotsChoice + 1);
        }
        else if (guest.DuelProcess is { } duel)
        {
            BattleChoiceRow[] choices = guest.DuelProcessChoices.Where(choice => choice.Learned).Select(choice => new BattleChoiceRow(choice.Name, string.Join(" ", choice.Buttons.Select(sharedInput.NativeButtonBindingHint)), guest.DuelProcessChoiceUnsupportedReason(choice) is not null)).ToArray();
            DrawBattleChoices(canvas, theme, layout, textScale, $"DUEL {Math.Max(0, (int)duel.RemainingTimer) / 60f:0.00}", choices.Length == 0 ? [new("Duel")] : choices, 0);
        }
        else if (guest.ShotProcess is { } shot)
        {
            DrawBattleChoices(canvas, theme, layout, textScale, $"SHOT {Math.Max(0, (int)shot.RemainingTimer) / 30f:0.00}", [new($"{shot.RemainingAmmo} ammo"), new($"{sharedInput.NativeButtonBindingHint(8)}: Fire"), new($"{sharedInput.NativeButtonBindingHint(0x10)}: Finish")], 0);
        }
        return true;
    }
}
