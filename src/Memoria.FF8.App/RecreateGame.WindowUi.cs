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

        string hint = $"Confirm: {keyboard.BindingHint(ControlAction.Confirm)}    Menu: {keyboard.BindingHint(ControlAction.Menu)}    Load: {keyboard.BindingHint(ControlAction.LoadMenu)}";
        text.DrawCentered(batch, hint, Math.Min(bounds.Height * 0.02f, bounds.Width * 0.024f), new Vector2(bounds.Center.X, bounds.Y + bounds.Height * 0.89f), new Color(155, 155, 155), "Times New Roman");
    }

    private void DrawPlayerSavesUi(Rectangle bounds)
    {
        batch.Draw(pixel, bounds, Color.Black);
        IEnumerable<PlayerSaveEntry> displayedEntries = saveMenu.VisibleEntries;
        string key = string.Join("|", saveMenu.Mode, saveMenu.Bank, saveMenu.Page, saveMenu.SelectedSlot, saveMenu.ConfirmOverwrite, saveMenu.TransferSource, saveMenu.Message, string.Join(";", displayedEntries.Select(entry => $"{entry}:{string.Join(",", entry.PartyPortraits)}")), keyboard.Profile.GetHashCode(), keyboard.ControllerPresentation, keyboard.LastInputDevice);
        if (savesWindowUi is null || savesWindowKey != key)
        {
            string transferHint = $"{keyboard.BindingHint(ControlAction.Card)}: Transfer";
            string navigationHint = "Left / Right: Bank    Page Up / Down: Page";
            savesWindowUi = PlayerSaveView.Build(saveMenu, transferHint, navigationHint);
            savesWindowKey = key;
        }
        DrawWindowView(savesWindowUi, bounds);
    }

    private void DrawNativePartyUi(Rectangle bounds)
    {
        NativePartyMenuState menu = events!.PartyMenu!;
        string title = menu.Kind switch { NativeFieldMenuKind.Normal => "Menu", NativeFieldMenuKind.Save => "Save", NativeFieldMenuKind.Shop => "Shop", NativeFieldMenuKind.WeaponShop => "Junk Shop", _ => "Party" };
        string Name(byte row) => row == 255 ? "Empty" : Memoria.FF8.GameUi.LiveMenuData.CharacterName(session.State, row, events.Scene.Names, liveMenuCatalog);
        string party = menu.Kind == NativeFieldMenuKind.Save ? "Save slots are unavailable." : menu.Kind is NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop ? "Opening shop" : menu.TemporaryCombatParty is null ? "Opening party menu" : string.Join(" / ", menu.TemporaryCombatParty.Select(row => row == 255 ? "Empty" : Name(row)));
        IReadOnlyList<PlayerAction> inputs = Memoria.FF8.GameUi.NativePartyMenuProjection.Inputs(menu);
        List<string> candidates = inputs.Select(input => input.Kind switch
        {
            PlayerActionKind.SelectPartySlot => $"Slot {input.Value + 1}: {Name(menu.TemporaryCombatParty![input.Value])}",
            PlayerActionKind.SelectPartyMember => input.Value == 255 ? "Reserve: Empty (remove member)" : $"Reserve: {Name((byte)input.Value)}",
            _ => "Confirm party"
        }).ToList();
        string instruction = menu.Phase is NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization ? "Returning to field" : menu.Kind == NativeFieldMenuKind.Save ? "Continue without saving" : menu.Kind is NativeFieldMenuKind.Normal or NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop ? "Return to field" : menu.SelectedPartySlot is not null ? "Select destination" : "Select a slot to change, or Confirm party";
        string key = string.Join("|", title, party, menu.SelectedPartySlot, partyMenuCandidate, instruction, string.Join(";", candidates));
        batch.Draw(pixel, bounds, Color.Black * 0.8f);
        if (nativePartyWindowUi is null || nativePartyWindowKey != key)
        {
            UiAnchorPanel root = new();
            UiPanel panel = new() { Anchors = new UiAnchors(0.04f, 0.04f, 0.96f, 0.96f), Padding = 16 };
            UiGrid content = new() { Gap = 8 };
            content.ColumnDefinitions.Add(UiGridLength.Star());
            content.RowDefinitions.Add(UiGridLength.Pixel(36));
            content.RowDefinitions.Add(UiGridLength.Pixel(40));
            content.RowDefinitions.Add(UiGridLength.Star());
            content.RowDefinitions.Add(UiGridLength.Pixel(40));
            content.Children.Add(new UiLabel { Text = title, FontSize = 26 });
            content.Children.Add(new UiLabel { GridRow = 1, Text = party, FontSize = 20 });
            List<UiImageSource?> portraits = inputs.Select(input =>
            {
                int row = input.Kind == PlayerActionKind.SelectPartySlot ? menu.TemporaryCombatParty![input.Value] : input.Kind == PlayerActionKind.SelectPartyMember ? input.Value : 255;
                return row != 255 && session.State.CharacterVitals?.Records.ElementAtOrDefault(row) is { } character ? new UiImageSource?(new UiImageSource(UiImageKind.CharacterPortrait, character.CharacterId)) : null;
            }).ToList();
            content.Children.Add(new UiList { GridRow = 2, Items = candidates, Images = portraits, SelectedIndex = partyMenuCandidate, RowHeight = 40, FontSize = 20 });
            content.Children.Add(new UiLabel { GridRow = 3, Text = instruction, FontSize = 16, Muted = true });
            panel.Children.Add(content);
            root.Children.Add(panel);
            nativePartyWindowUi = CreateWindowView(root);
            nativePartyWindowKey = key;
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
