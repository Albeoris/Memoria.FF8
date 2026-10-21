using Memoria.FF8.Events;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private static readonly string[] TitleChoices = ["NEW GAME", "Continue", "Config"];
    private UiView? savesWindowUi;
    private string? savesWindowKey;
    private UiView? nativePartyWindowUi;
    private string? nativePartyWindowKey;
    private UiView? topNoticeWindowUi;
    private UiView? bottomNoticeWindowUi;
    private string? topNoticeWindowText;
    private string? bottomNoticeWindowText;

    private void DrawTitleUi(Rectangle bounds)
    {
        float size = Math.Min(bounds.Height * 0.044f, bounds.Width * 0.06f);
        float rowHeight = bounds.Height * 0.074f;
        float top = bounds.Y + bounds.Height * 0.63f;
        for (int index = 0; index < TitleChoices.Length; index++)
        {
            Vector2 measured = text.MeasureWrapped(TitleChoices[index], size, float.PositiveInfinity, "Times New Roman");
            float y = top + rowHeight * index;
            text.DrawCentered(batch, TitleChoices[index], size, new Vector2(bounds.Center.X, y), Color.White, "Times New Roman");
            if (index == titleSelection)
            {
                float pointerHeight = size * 0.85f;
                uiCanvas?.MenuPointer(new UiRect(bounds.Center.X - measured.X / 2 - pointerHeight * 1.8f, y + (measured.Y - pointerHeight) / 2, pointerHeight * 1.5f, pointerHeight));
            }
        }
    }

    private float WindowTextScale => uiPreferences.TextScale / 100f * uiPreferences.UiFontSize / 24f;

    private void DrawPlayerSavesUi(Rectangle bounds)
    {
        batch.Draw(pixel, bounds, Color.Black);
        PlayerSaveView.ConfigureTextScale(saveMenu, WindowTextScale);
        IEnumerable<PlayerSaveEntry> displayedEntries = saveMenu.VisibleEntries;
        string key = string.Join("|", WindowTextScale, uiPreferences.UiFontFamily, saveMenu.Mode, saveMenu.Bank, saveMenu.Page, saveMenu.SelectedSlot, saveMenu.ConfirmOverwrite, saveMenu.TransferSource, saveMenu.Message, string.Join(";", displayedEntries.Select(entry => $"{entry}:{string.Join(",", entry.PartyPortraits)}")), keyboard.Profile.GetHashCode(), keyboard.ControllerPresentation, keyboard.LastInputDevice);
        if (savesWindowUi is null || savesWindowKey != key)
        {
            string transferHint = $"{keyboard.BindingPrompt(ControlAction.Card)}: Transfer";
            string navigationHint = $"{keyboard.BindingPrompt(ControlAction.MoveLeft)} / {keyboard.BindingPrompt(ControlAction.MoveRight)}: Bank    {keyboard.BindingPrompt(ControlAction.UiPageUp)} / {keyboard.BindingPrompt(ControlAction.UiPageDown)}: Page";
            savesWindowUi = PlayerSaveView.Build(saveMenu, transferHint, navigationHint, keyboard.BindingPrompt(ControlAction.Confirm), keyboard.BindingPrompt(ControlAction.Cancel), WindowTextScale);
            savesWindowKey = key;
        }
        DrawWindowView(savesWindowUi, bounds);
    }

    private void DrawNativePartyUi(Rectangle bounds)
    {
        NativePartyMenuState menu = events!.PartyMenu!;
        batch.Draw(pixel, bounds, Color.Black * 0.8f);
        if (menu is { Kind: NativeFieldMenuKind.PartySelection, TemporaryCombatParty.Length: 3, TemporaryReserveParty.Length: 8 })
        {
            IReadOnlySet<int> visible = menu.TemporaryCombatParty.Concat(menu.TemporaryReserveParty).Where(row => row != 255).Select(row => (int)row).ToHashSet();
            string key = string.Join("|", WindowTextScale, uiPreferences.UiFontFamily, menu.Phase, menu.SelectedPartySlot, menu.LockedMask, partyMenuCandidate, string.Join(",", menu.TemporaryCombatParty), string.Join(",", menu.TemporaryReserveParty), keyboard.Profile.GetHashCode(), keyboard.ControllerPresentation, keyboard.LastInputDevice, string.Join(";", visible.Select(row => session.State.CharacterVitals?.Records.ElementAtOrDefault(row)).Where(character => character is not null).Select(character => $"{character!.CharacterId}:{character.Experience}:{character.HitPoints}:{GameUi.LiveMenuData.CharacterName(session.State, character.CharacterId, events.Scene.Names, liveMenuCatalog)}")));
            if (nativePartyWindowUi is null || nativePartyWindowKey != key)
            {
                GameUi.MenuData data = GameUi.LiveMenuData.Build(session.State, events.Scene.Names, liveMenuCatalog, visible);
                nativePartyWindowUi = GameUi.PartySelectionView.Build(menu, data, partyMenuCandidate, keyboard.BindingPrompt(ControlAction.Cancel), WindowTextScale);
                nativePartyWindowKey = key;
            }
            DrawWindowView(nativePartyWindowUi, bounds);
            return;
        }
        string message = menu.Phase is NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization ? "Returning to field" : menu.Kind switch
        {
            NativeFieldMenuKind.Normal => "Opening menu",
            NativeFieldMenuKind.Save => "Opening save menu",
            NativeFieldMenuKind.Shop => "Opening shop",
            NativeFieldMenuKind.WeaponShop => "Opening junk shop",
            _ => "Opening party menu"
        };
        if (nativePartyWindowUi is null || nativePartyWindowKey != message)
        {
            nativePartyWindowUi = new UiView(new GameUi.NoticeElement(message));
            nativePartyWindowKey = message;
        }
        DrawWindowView(nativePartyWindowUi, bounds);
    }
    private void DrawNoticeUi(Rectangle bounds, string message, bool bottom)
    {
        UiView? view = bottom ? bottomNoticeWindowUi : topNoticeWindowUi;
        string? previous = bottom ? bottomNoticeWindowText : topNoticeWindowText;
        if (view is null || previous != message)
        {
            UiAnchorPanel root = new();
            UiPanel panel = new() { Anchors = bottom ? new UiAnchors(0.03f, 0.69f, 0.97f, 0.97f) : new UiAnchors(0.25f, 0.018f, 0.75f, 0.143f), Padding = 14 };
            panel.Children.Add(new UiLabel { Text = message, FontSize = 19 });
            root.Children.Add(panel);
            view = bottom ? CreateWindowView(root) : new UiView(new GameUi.NoticeElement(message));
            if (bottom)
            {
                bottomNoticeWindowUi = view;
                bottomNoticeWindowText = message;
            }
            else
            {
                topNoticeWindowUi = view;
                topNoticeWindowText = message;
            }
        }

        DrawWindowView(view, bounds);
    }

    private static UiView CreateWindowView(UiElement root)
    {
        return new UiView(root) { DesignWidth = 960, DesignHeight = 672 };
    }

    private void DrawWindowView(UiView view, Rectangle bounds)
    {
        if (uiCanvas is null)
        {
            return;
        }

        view.Resize(bounds.Width, bounds.Height);
        view.Draw(uiCanvas, uiTheme);
    }
}
