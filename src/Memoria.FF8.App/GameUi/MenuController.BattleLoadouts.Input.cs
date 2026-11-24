using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string SelectedBattleLoadoutEntryId => battleLoadoutFocus == BattleLoadoutWeaponListId ? battleLoadoutWeaponFocus : battleLoadoutFocus;

    private int SelectedBattleLoadoutIndex()
    {
        return Math.Max(0, BattleLoadoutEntries().ToList().FindIndex(entry => entry.Id == SelectedBattleLoadoutEntryId));
    }

    private void SelectBattleLoadoutEntry(string id)
    {
        UiView view = BuildView();
        if (battleLoadoutSwap is null && battleLoadoutWeaponList is { } list)
        {
            int row = Array.FindIndex(BattleLoadoutWeaponRows(), candidate => candidate.Entry.Id == id);
            if (row >= 0 && battleLoadoutPreparation is null)
            {
                list.SelectedIndex = row;
                battleLoadoutWeaponFocus = id;
                view.Focus(list);
                return;
            }
        }
        UiElement? control = FocusableDescendants(view.Root).FirstOrDefault(element => element.Id == id && element.Enabled);
        if (control is null || !view.Focus(control))
        {
            throw new InvalidOperationException($"The loadout scene has no enabled entry '{id}'.");
        }
    }

    private void FocusBattleLoadoutView(UiView view)
    {
        if (battleLoadoutPreparation is not null)
        {
            return;
        }
        UiElement? control = FocusableDescendants(view.Root).FirstOrDefault(element => element.Id == battleLoadoutFocus);
        view.Focus(control ?? battleLoadoutWeaponList);
    }

    private bool NavigateBattleLoadoutEditor(MenuInput input)
    {
        if (!IsBattleLoadoutEditor)
        {
            return false;
        }
        if (input == MenuInput.Cancel)
        {
            Back();
            return true;
        }
        if (battleLoadoutPreparation is not null || battleLoadoutEditor is null)
        {
            return true;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            if (battleLoadoutSwap is null && BattleLoadoutAdjacentCharacter(input == MenuInput.PageLeft ? -1 : 1) is not null)
            {
                SoundRequested?.Invoke(MenuSound.Move);
                Execute(new("loadout-cycle", Value: input == MenuInput.PageLeft ? "-1" : "1"));
            }
            return true;
        }
        UiView view = BuildView();
        if (input is MenuInput.Right or MenuInput.FocusRight && view.FocusedElement == battleLoadoutWeaponList)
        {
            UiElement? limit = FocusableDescendants(view.Root).FirstOrDefault(element => element.Id == BattleLoadoutLimitId(BattleLimitFamily.Renzokuken));
            if (limit is not null && view.Focus(limit))
            {
                SoundRequested?.Invoke(MenuSound.Move);
            }
            return true;
        }
        if (input is MenuInput.Left or MenuInput.FocusLeft && view.FocusedElement?.Id?.StartsWith("limit-", StringComparison.Ordinal) == true)
        {
            if (view.Focus(battleLoadoutWeaponList))
            {
                SoundRequested?.Invoke(MenuSound.Move);
            }
            return true;
        }
        UiKey? key = input switch
        {
            MenuInput.Up => UiKey.Up,
            MenuInput.Down => UiKey.Down,
            MenuInput.Left or MenuInput.FocusLeft => UiKey.Left,
            MenuInput.Right or MenuInput.FocusRight => UiKey.Right,
            MenuInput.PageUp => UiKey.PageUp,
            MenuInput.PageDown => UiKey.PageDown,
            MenuInput.Confirm => UiKey.Confirm,
            MenuInput.Tab => UiKey.Tab,
            _ => null
        };
        string previous = SelectedBattleLoadoutEntryId;
        if (key is { } supported && view.Key(supported) && input != MenuInput.Confirm && previous != SelectedBattleLoadoutEntryId)
        {
            SoundRequested?.Invoke(MenuSound.Move);
        }
        return true;
    }

    internal void HandleBattleLoadoutWheel(int x, int y, int delta)
    {
        if (!IsBattleLoadoutEditor || battleLoadoutPreparation is not null || battleLoadoutSwap is not null || delta == 0)
        {
            return;
        }
        UiView view = BuildView();
        (float designX, float designY) = view.ToDesignPoint(x, y);
        if (battleLoadoutWeaponList is { } list && list.Bounds.Contains(designX, designY))
        {
            list.ScrollOffset -= Math.Sign(delta) * 3;
        }
    }
}
