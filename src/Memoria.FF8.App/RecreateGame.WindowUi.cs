using Memoria.FF8.Events;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private UiView? titleWindowUi;
    private int titleWindowSelection = -1;
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
        batch.Draw(pixel, bounds, new Color(22, 22, 22));
        if (titleWindowUi is null || titleWindowSelection != titleSelection)
        {
            UiAnchorPanel root = new();
            root.Children.Add(new UiSigil { Seed = 8, Anchors = new UiAnchors(0.39f, 0.05f, 0.61f, 0.31f) });
            root.Children.Add(new UiLabel { Text = "FINAL FANTASY VIII", FontSize = 42, Anchors = new UiAnchors(0.1f, 0.29f, 0.9f, 0.39f), HorizontalAlignment = UiAlignment.Center, VerticalAlignment = UiAlignment.Center });
            root.Children.Add(new UiLabel { Text = "MEMORIA", FontSize = 20, Muted = true, Anchors = new UiAnchors(0.1f, 0.39f, 0.9f, 0.45f), HorizontalAlignment = UiAlignment.Center, VerticalAlignment = UiAlignment.Center });
            UiPanel menu = new() { Anchors = new UiAnchors(0.32f, 0.53f, 0.68f, 0.75f), Padding = new UiThickness(12, 14) };
            UiList choices = new() { Items = new List<string> { "New Game", "Continue" }, SelectedIndex = titleSelection, RowHeight = 56, FontSize = 28 };
            menu.Children.Add(choices);
            root.Children.Add(menu);
            root.Children.Add(new UiLabel { Text = "Arrows: select    Enter: confirm    F10: load", FontSize = 16, Muted = true, Anchors = new UiAnchors(0.05f, 0.91f, 0.95f, 0.98f), HorizontalAlignment = UiAlignment.Center, VerticalAlignment = UiAlignment.Center });
            titleWindowUi = CreateWindowView(root);
            titleWindowSelection = titleSelection;
        }

        DrawWindowView(titleWindowUi, bounds);
    }

    private void DrawPlayerSavesUi(Rectangle bounds)
    {
        batch.Draw(pixel, bounds, Color.Black * 0.6f);
        List<PlayerSaveEntry> entries = new();
        if (saveMenu.QuickEntry is PlayerSaveEntry quick)
        {
            entries.Add(quick);
        }

        entries.AddRange(saveMenu.VisibleEntries);
        string key = string.Join("|", saveMenu.Mode, saveMenu.Bank, saveMenu.Page, saveMenu.SelectedSlot, saveMenu.ConfirmOverwrite, saveMenu.CanRecreate, saveMenu.Message, saveMenu.RecreateUnavailableReason, string.Join(";", entries));
        if (savesWindowUi is null || savesWindowKey != key)
        {
            UiAnchorPanel root = new();
            UiPanel panel = new() { Anchors = new UiAnchors(0.04f, 0.04f, 0.96f, 0.96f), Padding = 16 };
            UiGrid content = new() { Gap = 4 };
            content.ColumnDefinitions.Add(UiGridLength.Star());
            content.RowDefinitions.Add(UiGridLength.Pixel(36));
            content.RowDefinitions.Add(UiGridLength.Pixel(24));
            content.RowDefinitions.Add(UiGridLength.Star());
            content.RowDefinitions.Add(UiGridLength.Pixel(28));
            content.RowDefinitions.Add(UiGridLength.Pixel(52));
            content.RowDefinitions.Add(UiGridLength.Pixel(72));
            content.Children.Add(new UiLabel { Text = saveMenu.Mode == PlayerSaveMenuMode.Save ? "Save Game" : "Load Game", FontSize = 28 });
            content.Children.Add(new UiLabel { GridRow = 1, Text = $"Bank {saveMenu.Bank + 1} / {PlayerSaveStore.BankCount}    Page {saveMenu.Page + 1} / {PlayerSaveMenu.PageCount}", FontSize = 17, Muted = true });
            UiList rows = new() { GridRow = 2, RowHeight = 50, Items = entries.Select(FormatPlayerSaveRow).ToList(), SelectedIndex = Math.Max(0, entries.FindIndex(entry => entry.Slot == saveMenu.SelectedSlot)) };
            content.Children.Add(rows);
            bool loading = saveMenu.Mode == PlayerSaveMenuMode.Load;
            content.Children.Add(new UiLabel { GridRow = 3, Text = loading ? "R: Recreate selected save from start" : saveMenu.ConfirmOverwrite ? "Confirm overwrite" : "Choose a save slot", FontSize = 18, Muted = loading && !saveMenu.CanRecreate });
            string detail = saveMenu.Message ?? (loading ? saveMenu.RecreateUnavailableReason ?? "Rebuild saved progress with this engine, then replace and load this slot after success." : "Enter: save to the selected slot. Existing saves require overwrite confirmation.");
            content.Children.Add(new UiLabel { GridRow = 4, Text = detail, FontSize = 15 });
            content.Children.Add(new UiLabel { GridRow = 5, Text = "Up/Down: select   Left/Right: bank   PgUp/PgDn: page\nEnter: confirm   Escape: cancel\nF5: quick save   F9: quick load   F6: save   F10: load", FontSize = 16, Muted = true });
            panel.Children.Add(content);
            root.Children.Add(panel);
            savesWindowUi = CreateWindowView(root);
            savesWindowKey = key;
        }

        DrawWindowView(savesWindowUi, bounds);
    }

    private static string FormatPlayerSaveRow(PlayerSaveEntry entry)
    {
        string timestamp = entry.SavedAt is DateTime savedAt ? "    " + savedAt.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
        return entry.Label + timestamp + "\n" + entry.Details;
    }

    private void DrawNativePartyUi(Rectangle bounds)
    {
        NativePartyMenuState menu = events!.PartyMenu!;
        string title = menu.Kind == NativeFieldMenuKind.Normal ? "Menu" : "Party";
        string Name(byte row) => row switch
        {
            0 => events.Scene.Names.GetValueOrDefault(0, "Squall"),
            1 => "Zell",
            2 => "Irvine",
            3 => "Quistis",
            4 => events.Scene.Names.GetValueOrDefault(1, "Rinoa"),
            5 => "Selphie",
            6 => "Seifer",
            7 => "Edea",
            _ => throw new NotSupportedException("Party menu labels require native normal character identities.")
        };
        string party = menu.TemporaryCombatParty is null ? "Opening party menu" : string.Join(" / ", menu.TemporaryCombatParty.Select(row => row == 255 ? "Empty" : Name(row)));
        List<string> candidates = events.PartyMenuCandidates.Select(Name).ToList();
        string instruction = menu.Phase is NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization ? "Returning to field" : menu.Kind == NativeFieldMenuKind.Normal ? "Esc: return to field" : menu.TemporaryCombatParty?.All(row => row != 255) == true ? "Enter: confirm party" : "Arrows: select   Enter: add member";
        string key = string.Join("|", title, party, partyMenuCandidate, instruction, string.Join(";", candidates));
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
            content.Children.Add(new UiList { GridRow = 2, Items = candidates, SelectedIndex = partyMenuCandidate, RowHeight = 40, FontSize = 20 });
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
            UiPanel panel = new() { Anchors = bottom ? new UiAnchors(0.03f, 0.69f, 0.97f, 0.97f) : new UiAnchors(0.03f, 0.03f, 0.97f, 0.2f), Padding = 14 };
            panel.Children.Add(new UiLabel { Text = message, FontSize = 19 });
            root.Children.Add(panel);
            view = CreateWindowView(root);
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
