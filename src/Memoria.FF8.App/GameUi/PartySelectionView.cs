using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Three combat cells and the retained four-by-two reserve pane.</summary>
public static class PartySelectionView
{
    public static UiView Build(NativePartyMenuState menu, MenuData data, int candidate, string? cancelHint = null, float textScale = 1, Func<string, string, string>? localizeText = null)
    {
        if (!float.IsFinite(textScale) || textScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textScale));
        }
        // TODO(TD-011): Retain native window transitions and cursor animation timing.
        return new UiView(new PartyPane(menu, data, candidate, cancelHint ?? new KeyboardInput().BindingPrompt(ControlAction.Cancel), textScale, localizeText)) { DesignWidth = 960, DesignHeight = 672 };
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

    private sealed class PartyPane(NativePartyMenuState menu, MenuData data, int candidate, string cancelHint, float textScale, Func<string, string, string>? localizeText) : UiElement
    {
        private string SystemText(string id, string original) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Translate(localizeText, id, original);

        private string FormatSystemText(string id, string original, params object?[] arguments) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(SystemText(id, original), original, arguments);

        private float bodyTop = 108;
        private float bodyScale = 1;

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            IReadOnlyList<PlayerAction> inputs = NativePartyMenuProjection.Inputs(menu);
            PlayerAction? focused = inputs.Count == 0 ? null : inputs[Math.Clamp(candidate, 0, inputs.Count - 1)];
            bool pending = menu.SelectedPartySlot is not null;
            byte source = pending ? menu.TemporaryCombatParty![menu.SelectedPartySlot!.Value] : (byte)255;
            bool lockedSource = source != 255 && (menu.LockedMask & 1 << source) != 0;
            float captionSize = UiTheme.DefaultCaptionFontSize * textScale;
            float captionHeight = theme.CaptionContentHeight(captionSize);
            float helpHeight = 44;
            if (captionHeight > 0)
            {
                try
                {
                    helpHeight = Math.Max(helpHeight, canvas.MeasureText("Mg", 25 * textScale, float.PositiveInfinity).Height);
                }
                catch (NotSupportedException)
                {
                    helpHeight = Math.Max(helpHeight, 25 * textScale * 1.5f);
                }
            }
            float headerHeight = Math.Max(78, captionHeight + helpHeight + 16);
            bodyTop = 18 + headerHeight + 12;
            bodyScale = Math.Max(0, 588 - bodyTop) / 480;
            theme.Panel(canvas, new UiRect(18, 18, 924, headerHeight), SystemText("inspector.ui.partyselectionview.drawself.help", "HELP"), captionSize);
            string help = menu.Phase != NativePartyMenuPhase.Active ? SystemText("inspector.ui.partyselectionview.drawself.returning.to.field", "Returning to field") : lockedSource ? SystemText("inspector.ui.partyselectionview.drawself.select.a.party.member.to.reorder", "Select a party member to reorder.") : pending ? SystemText("inspector.ui.partyselectionview.drawself.select.a.character.to.exchange", "Select a character to exchange.") : SystemText("inspector.ui.partyselectionview.drawself.select.a.party.member", "Select a party member.");
            Text(canvas, help, 25, new UiRect(34, Math.Max(34, 18 + captionHeight), 890, helpHeight), theme.Foreground, UiAlignment.Center);

            for (int slot = 0; slot < 3; slot++)
            {
                UiRect bounds = new(18, 108 + slot * 162, 510, 156);
                theme.Panel(canvas, BodyRect(bounds));
                byte row = menu.TemporaryCombatParty![slot];
                MenuCharacter? character = Character(row);
                Portrait(canvas, character, new UiRect(62, bounds.Y + 14, 88, 128));
                Status(canvas, theme, character, new UiRect(174, bounds.Y + 18, 328, 124));
                if (row != 255 && (menu.LockedMask & 1 << row) != 0)
                {
                    Text(canvas, SystemText("inspector.ui.partyselectionview.drawself.lock", "LOCK"), 15, BodyRect(new UiRect(62, bounds.Y + 116, 88, 24)), theme.Muted);
                }
                bool active = focused is { Kind: PlayerActionKind.SelectPartySlot } action && action.Value == slot;
                if (menu.SelectedPartySlot == slot)
                {
                    theme.Pointer(canvas, 30, BodyY(bounds.Y + 57), 18 * bodyScale);
                }
                if (active)
                {
                    theme.Pointer(canvas, 38, BodyY(bounds.Y + 65), 18 * bodyScale);
                }
            }

            theme.Panel(canvas, BodyRect(new UiRect(534, 108, 408, 318)));
            for (int index = 0; index < 8; index++)
            {
                byte row = menu.TemporaryReserveParty![index];
                UiRect cell = new(552 + index % 4 * 96, 120 + index / 4 * 150, 88, 138);
                MenuCharacter? character = Character(row);
                Portrait(canvas, character, new UiRect(cell.X + 12, cell.Y + 4, 64, 96));
                bool locked = row != 255 && (menu.LockedMask & 1 << row) != 0;
                Text(canvas, character?.Name ?? "", 18, BodyRect(new UiRect(cell.X, cell.Y + 104, cell.Width, 30)), locked || lockedSource ? theme.Muted : theme.Foreground);
                if (locked)
                {
                    Text(canvas, SystemText("inspector.ui.partyselectionview.drawself.lock", "LOCK"), 13, BodyRect(new UiRect(cell.X + 20, cell.Y + 78, 62, 22)), theme.Muted);
                }
                if (focused is { Kind: PlayerActionKind.SelectPartyMember } action && action.Value == row && Array.IndexOf(menu.TemporaryReserveParty, row) == index)
                {
                    theme.Pointer(canvas, cell.X - 8, BodyY(cell.Y + 42), 16 * bodyScale);
                    if (row == 255)
                    {
                        Text(canvas, SystemText("inspector.ui.partyselectionview.drawself.empty", "Empty"), 18, BodyRect(new UiRect(cell.X, cell.Y + 104, cell.Width, 30)), theme.Foreground);
                    }
                }
            }

            theme.Panel(canvas, BodyRect(new UiRect(534, 432, 408, 156)));
            int selectedRow = focused is { Kind: PlayerActionKind.SelectPartyMember } reserve ? reserve.Value : focused is { Kind: PlayerActionKind.SelectPartySlot } combat ? menu.TemporaryCombatParty![combat.Value] : 255;
            Status(canvas, theme, Character(selectedRow), new UiRect(558, 450, 360, 126));
            theme.Panel(canvas, new UiRect(18, 600, 924, 54));
            Text(canvas, pending ? FormatSystemText("inspector.ui.partyselectionview.drawself.clear.selection", "{0}: clear selection", cancelHint) : SystemText("inspector.ui.partyselectionview.drawself.choose.three.members.then.confirm", "Choose three members, then confirm."), 19, new UiRect(250, 612, 670, 32), theme.Muted);
            bool canConfirm = !pending && inputs.Any(input => input.Kind == PlayerActionKind.Confirm);
            Text(canvas, SystemText("inspector.ui.partyselectionview.drawself.confirm", "Confirm"), 24, new UiRect(62, 610, 142, 36), canConfirm ? theme.Foreground : theme.Muted);
            if (focused is { Kind: PlayerActionKind.Confirm })
            {
                theme.Pointer(canvas, 30, 620, 18);
            }
        }

        private float BodyY(float y) => bodyTop + (y - 108) * bodyScale;

        private UiRect BodyRect(UiRect bounds) => new(bounds.X, BodyY(bounds.Y), bounds.Width, bounds.Height * bodyScale);

        private MenuCharacter? Character(int row)
        {
            return row == 255 ? null : data.Characters.FirstOrDefault(character => character.Id == $"character-{row}");
        }

        private void Portrait(IUiCanvas canvas, MenuCharacter? character, UiRect bounds)
        {
            if (character?.NativeId is int nativeId)
            {
                canvas.Image(new UiImageSource(UiImageKind.CharacterPortrait, nativeId), BodyRect(bounds));
            }
        }

        private void Text(IUiCanvas canvas, string value, float size, UiRect bounds, UiColor color, UiAlignment alignment = UiAlignment.Start)
        {
            canvas.Text(value, size * textScale, bounds, color, alignment, UiTextFlow.SingleLine);
        }

        private void Status(IUiCanvas canvas, UiTheme theme, MenuCharacter? character, UiRect bounds)
        {
            Text(canvas, character?.Name ?? SystemText("inspector.ui.partyselectionview.status.empty", "Empty"), 28, BodyRect(new UiRect(bounds.X, bounds.Y, bounds.Width, 40)), theme.Foreground);
            if (character is null)
            {
                return;
            }
            Text(canvas, FormatSystemText("inspector.ui.partyselectionview.status.lv", "Lv  {0}", MenuController.Number(character.Level)), 23, BodyRect(new UiRect(bounds.X, bounds.Y + 43, bounds.Width, 34)), theme.Foreground);
            Text(canvas, FormatSystemText("inspector.ui.partyselectionview.status.hp", "HP  {0}", MenuController.Hp(character)), 23, BodyRect(new UiRect(bounds.X, bounds.Y + 81, bounds.Width, 38)), theme.Foreground);
        }
    }
}
