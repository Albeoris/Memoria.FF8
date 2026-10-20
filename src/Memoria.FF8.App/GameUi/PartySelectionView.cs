using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Three combat cells and the retained four-by-two reserve pane.</summary>
public static class PartySelectionView
{
    public static UiView Build(NativePartyMenuState menu, MenuData data, int candidate)
    {
        // TODO(TD-011): Retain native window transitions and cursor animation timing.
        return new UiView(new PartyPane(menu, data, candidate)) { DesignWidth = 960, DesignHeight = 672 };
    }

    public static int Navigate(NativePartyMenuState menu, int candidate, int horizontal, int vertical)
    {
        IReadOnlyList<PlayerAction> inputs = NativePartyMenuProjection.Inputs(menu);
        if (inputs.Count == 0)
        {
            return 0;
        }
        candidate = Math.Clamp(candidate, 0, inputs.Count - 1);
        (int x, int y) = Cell(menu, inputs[candidate]);
        int next = candidate;
        int best = int.MaxValue;
        for (int index = 0; index < inputs.Count; index++)
        {
            (int targetX, int targetY) = Cell(menu, inputs[index]);
            int primary = horizontal != 0 ? (targetX - x) * Math.Sign(horizontal) : (targetY - y) * Math.Sign(vertical);
            int secondary = horizontal != 0 ? Math.Abs(targetY - y) : Math.Abs(targetX - x);
            if (primary <= 0)
            {
                continue;
            }
            int distance = secondary * 100 + primary;
            if (distance < best)
            {
                best = distance;
                next = index;
            }
        }
        return next;
    }

    private static (int X, int Y) Cell(NativePartyMenuState menu, PlayerAction input)
    {
        if (input.Kind == PlayerActionKind.SelectPartySlot)
        {
            return (0, input.Value);
        }
        if (input.Kind == PlayerActionKind.SelectPartyMember)
        {
            int index = Array.IndexOf(menu.TemporaryReserveParty!, (byte)input.Value);
            return (1 + index % 4, index / 4);
        }
        return (0, 3);
    }

    private sealed class PartyPane(NativePartyMenuState menu, MenuData data, int candidate) : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            IReadOnlyList<PlayerAction> inputs = NativePartyMenuProjection.Inputs(menu);
            PlayerAction? focused = inputs.Count == 0 ? null : inputs[Math.Clamp(candidate, 0, inputs.Count - 1)];
            bool pending = menu.SelectedPartySlot is not null;
            byte source = pending ? menu.TemporaryCombatParty![menu.SelectedPartySlot!.Value] : (byte)255;
            bool lockedSource = source != 255 && (menu.LockedMask & 1 << source) != 0;
            theme.Panel(canvas, new UiRect(18, 18, 924, 78), "HELP", 18);
            string help = menu.Phase != NativePartyMenuPhase.Active ? "Returning to field" : lockedSource ? "Select a party member to reorder." : pending ? "Select a character to exchange." : "Select a party member.";
            canvas.Text(help, 25, new UiRect(34, 34, 890, 44), theme.Foreground, UiAlignment.Center);

            for (int slot = 0; slot < 3; slot++)
            {
                UiRect bounds = new(18, 108 + slot * 162, 510, 156);
                theme.Panel(canvas, bounds);
                byte row = menu.TemporaryCombatParty![slot];
                MenuCharacter? character = Character(row);
                Portrait(canvas, character, new UiRect(62, bounds.Y + 14, 88, 128));
                Status(canvas, theme, character, new UiRect(174, bounds.Y + 18, 328, 124));
                if (row != 255 && (menu.LockedMask & 1 << row) != 0)
                {
                    canvas.Text("LOCK", 15, new UiRect(446, bounds.Y + 10, 65, 24), theme.Muted);
                }
                bool active = focused is { Kind: PlayerActionKind.SelectPartySlot } action && action.Value == slot;
                if (menu.SelectedPartySlot == slot)
                {
                    theme.Pointer(canvas, 30, bounds.Y + 57, 18);
                }
                if (active)
                {
                    theme.Pointer(canvas, 38, bounds.Y + 65, 18);
                }
            }

            theme.Panel(canvas, new UiRect(534, 108, 408, 318));
            for (int index = 0; index < 8; index++)
            {
                byte row = menu.TemporaryReserveParty![index];
                UiRect cell = new(552 + index % 4 * 96, 120 + index / 4 * 150, 88, 138);
                MenuCharacter? character = Character(row);
                Portrait(canvas, character, new UiRect(cell.X + 12, cell.Y + 4, 64, 96));
                bool locked = row != 255 && (menu.LockedMask & 1 << row) != 0;
                canvas.Text(character?.Name ?? "", 18, new UiRect(cell.X, cell.Y + 104, cell.Width, 30), locked || lockedSource ? theme.Muted : theme.Foreground);
                if (locked)
                {
                    canvas.Text("LOCK", 13, new UiRect(cell.X + 20, cell.Y + 84, 62, 22), theme.Muted);
                }
                if (focused is { Kind: PlayerActionKind.SelectPartyMember } action && action.Value == row && Array.IndexOf(menu.TemporaryReserveParty, row) == index)
                {
                    theme.Pointer(canvas, cell.X - 8, cell.Y + 42, 16);
                    if (row == 255)
                    {
                        canvas.Text("Empty", 18, new UiRect(cell.X, cell.Y + 104, cell.Width, 30), theme.Foreground);
                    }
                }
            }

            theme.Panel(canvas, new UiRect(534, 432, 408, 156));
            int selectedRow = focused is { Kind: PlayerActionKind.SelectPartyMember } reserve ? reserve.Value : focused is { Kind: PlayerActionKind.SelectPartySlot } combat ? menu.TemporaryCombatParty![combat.Value] : 255;
            Status(canvas, theme, Character(selectedRow), new UiRect(558, 450, 360, 126));
            theme.Panel(canvas, new UiRect(18, 600, 924, 54));
            canvas.Text(pending ? "Cancel: clear selection" : "Choose three members, then confirm.", 19, new UiRect(250, 612, 670, 32), theme.Muted);
            bool canConfirm = !pending && inputs.Any(input => input.Kind == PlayerActionKind.Confirm);
            canvas.Text("Confirm", 24, new UiRect(62, 610, 142, 36), canConfirm ? theme.Foreground : theme.Muted);
            if (focused is { Kind: PlayerActionKind.Confirm })
            {
                theme.Pointer(canvas, 30, 620, 18);
            }
        }

        private MenuCharacter? Character(int row)
        {
            return row == 255 ? null : data.Characters.FirstOrDefault(character => character.Id == $"character-{row}");
        }

        private static void Portrait(IUiCanvas canvas, MenuCharacter? character, UiRect bounds)
        {
            if (character?.NativeId is int nativeId)
            {
                canvas.Image(new UiImageSource(UiImageKind.CharacterPortrait, nativeId), bounds);
            }
        }

        private static void Status(IUiCanvas canvas, UiTheme theme, MenuCharacter? character, UiRect bounds)
        {
            canvas.Text(character?.Name ?? "Empty", 28, new UiRect(bounds.X, bounds.Y, bounds.Width, 40), theme.Foreground);
            if (character is null)
            {
                return;
            }
            canvas.Text($"Lv  {MenuController.Number(character.Level)}", 23, new UiRect(bounds.X, bounds.Y + 43, bounds.Width, 34), theme.Foreground);
            canvas.Text($"HP  {MenuController.Hp(character)}", 23, new UiRect(bounds.X, bounds.Y + 81, bounds.Width, 38), theme.Foreground);
        }
    }
}
