using Memoria.FF8.Events;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private static readonly string[] TitleChoices = ["NEW GAME", "Continue"];
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
        float top = bounds.Y + bounds.Height * 0.67f;
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
        List<PlayerSaveEntry> entries = new();
        if (saveMenu.QuickEntry is PlayerSaveEntry quick)
        {
            entries.Add(quick);
        }

        entries.AddRange(saveMenu.VisibleEntries);
        string key = string.Join("|", saveMenu.Mode, saveMenu.Bank, saveMenu.Page, saveMenu.SelectedSlot, saveMenu.ConfirmOverwrite, saveMenu.Message, string.Join(";", entries), keyboard.Profile.GetHashCode(), keyboard.ControllerPresentation, keyboard.LastInputDevice);
        if (savesWindowUi is null || savesWindowKey != key)
        {
            UiAnchorPanel root = new();
            UiGrid content = new() { Anchors = new UiAnchors(0.02f, 0.02f, 0.98f, 0.98f), Gap = 5 };
            content.ColumnDefinitions.Add(UiGridLength.Star());
            content.RowDefinitions.Add(UiGridLength.Pixel(50));
            content.RowDefinitions.Add(UiGridLength.Pixel(72));
            content.RowDefinitions.Add(UiGridLength.Star());
            content.RowDefinitions.Add(UiGridLength.Pixel(32));
            bool loading = saveMenu.Mode == PlayerSaveMenuMode.Load;
            UiGrid header = new() { Gap = 5 };
            header.RowDefinitions.Add(UiGridLength.Star());
            header.ColumnDefinitions.Add(UiGridLength.Star(3.7f));
            header.ColumnDefinitions.Add(UiGridLength.Star(1.3f));
            UiPanel info = new() { Caption = "INFO.", CaptionFontSize = 13, CaptionHeight = 15, Padding = new UiThickness(8, 2) };
            info.Children.Add(new UiLabel { Text = $"Bank {saveMenu.Bank + 1} / {PlayerSaveStore.BankCount}    Page {saveMenu.Page + 1} / {PlayerSaveMenu.PageCount}", FontSize = 23 });
            header.Children.Add(info);
            UiPanel title = new() { GridColumn = 1, Padding = new UiThickness(8, 3) };
            title.Children.Add(new UiLabel { Text = loading ? "Load" : "Save", FontSize = 27 });
            header.Children.Add(title);
            content.Children.Add(header);

            string instruction = loading ? "Choose block to load" : saveMenu.ConfirmOverwrite ? "Confirm overwrite" : "Choose block to save";
            string detail = saveMenu.Message ?? (saveMenu.ConfirmOverwrite ? "Confirm again to overwrite the selected save." : "");
            UiPanel help = new() { GridRow = 1, Caption = "HELP", CaptionFontSize = 13, CaptionHeight = 15, Padding = new UiThickness(8, 2) };
            help.Children.Add(new UiLabel { Text = instruction + (detail.Length > 0 ? "\n" + detail : ""), FontSize = 20 });
            content.Children.Add(help);

            UiGrid blocks = BuildPlayerSaveBlocks(entries, loading);
            blocks.GridRow = 2;
            int selectedIndex = Math.Max(0, entries.FindIndex(entry => entry.Slot == saveMenu.SelectedSlot));
            UiButton? selectedBlock = blocks.Children.OfType<UiButton>().ElementAtOrDefault(selectedIndex);
            content.Children.Add(blocks);
            UiPanel footer = new() { GridRow = 3, Padding = new UiThickness(8, 3) };
            footer.Children.Add(new UiLabel { Text = $"Select / bank: directions    Page: {keyboard.BindingHint(ControlAction.UiPageUp)} / {keyboard.BindingHint(ControlAction.UiPageDown)}    Confirm: {keyboard.BindingHint(ControlAction.Confirm)}    Back: {keyboard.BindingHint(ControlAction.Cancel)}", FontSize = 16 });
            content.Children.Add(footer);
            root.Children.Add(content);
            savesWindowUi = CreateWindowView(root);
            savesWindowUi.Resize(960, 672);
            if (selectedBlock is not null)
            {
                savesWindowUi.Focus(selectedBlock);
            }
            savesWindowKey = key;
        }

        DrawWindowView(savesWindowUi, bounds);
    }

    internal static UiGrid BuildPlayerSaveBlocks(IReadOnlyList<PlayerSaveEntry> entries, bool loading)
    {
        UiGrid blocks = new() { Columns = 1, Rows = Math.Max(1, entries.Count), Gap = 3 };
        for (int index = 0; index < entries.Count; index++)
        {
            PlayerSaveEntry entry = entries[index];
            UiButton block = new() { GridRow = index, Id = "save-slot-" + entry.Slot, HighlightSelection = false, PointerSize = 20 };
            UiGrid metadata = new() { Margin = new UiThickness(35, 3, 8, 3), Gap = 1 };
            metadata.RowDefinitions.Add(UiGridLength.Star());
            metadata.RowDefinitions.Add(UiGridLength.Star());
            metadata.ColumnDefinitions.Add(UiGridLength.Star(1.5f));
            metadata.ColumnDefinitions.Add(UiGridLength.Star());
            metadata.Children.Add(new UiLabel { Text = entry.Label, FontSize = 20, Muted = loading && !entry.CanLoad });
            metadata.Children.Add(new UiLabel { GridColumn = 1, Text = entry.SavedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "", FontSize = 16, Muted = true });
            metadata.Children.Add(new UiLabel { GridRow = 1, GridColumnSpan = 2, Text = entry.Details, FontSize = 20, Muted = loading && !entry.CanLoad });
            block.Children.Add(metadata);
            blocks.Children.Add(block);
        }
        return blocks;
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
        string instruction = menu.Phase is NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization ? "Returning to field" : menu.Kind == NativeFieldMenuKind.Save ? "Esc: continue without saving" : menu.Kind is NativeFieldMenuKind.Normal or NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop ? "Esc: return to field" : menu.SelectedPartySlot is not null ? "Select destination   Enter: swap   Esc: cancel selection" : "Select a slot to change, or Confirm party";
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
