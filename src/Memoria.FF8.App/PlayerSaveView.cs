using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>One save browser for manual saving, loading, Continue and quick actions.</summary>
internal sealed partial class PlayerSaveView : UiElement
{
    private PlayerSaveView(PlayerSaveMenu menu, string transferHint, string navigationHint, string confirmHint, string cancelHint)
    {
        UiAnchorPanel root = new();
        UiGrid content = new() { Anchors = new UiAnchors(0.02f, 0.02f, 0.98f, 0.98f), Gap = 5 };
        content.ColumnDefinitions.Add(UiGridLength.Star());
        content.RowDefinitions.Add(UiGridLength.Pixel(72));
        content.RowDefinitions.Add(UiGridLength.Pixel(40));
        content.RowDefinitions.Add(UiGridLength.Pixel(70));
        content.RowDefinitions.Add(UiGridLength.Star());
        content.RowDefinitions.Add(UiGridLength.Pixel(34));
        UiGrid banks = BuildBanks(menu.Bank, menu.IsQuick);
        foreach (UiButton tab in banks.Children.OfType<UiButton>())
        {
            int bank = tab.GridColumn;
            tab.Click = () => menu.SelectBank(bank);
        }
        content.Children.Add(banks);
        UiGrid header = new() { GridRow = 1, Gap = 5 };
        header.RowDefinitions.Add(UiGridLength.Star());
        header.ColumnDefinitions.Add(UiGridLength.Star(3.7f));
        header.ColumnDefinitions.Add(UiGridLength.Star(1.3f));
        UiPanel info = new() { Caption = "INFO.", CaptionFontSize = 13, CaptionHeight = 13, Padding = new UiThickness(8, 0) };
        info.Children.Add(new UiLabel { Text = $"Page {menu.Page + 1} / {menu.PageCount}" + (menu.IsQuick ? "" : "    " + navigationHint), FontSize = 16, VerticalTextAlignment = UiAlignment.Center });
        header.Children.Add(info);
        UiPanel title = new() { GridColumn = 1, Padding = new UiThickness(8, 0) };
        string name = menu.Mode switch
        {
            PlayerSaveMenuMode.QuickLoad => "Quick Load",
            PlayerSaveMenuMode.QuickSave => "Quick Save",
            PlayerSaveMenuMode.Load => "Load",
            _ => "Save"
        };
        title.Children.Add(new UiLabel { Text = menu.IsTransferring ? "Transfer" : name, FontSize = 25, VerticalTextAlignment = UiAlignment.Center });
        header.Children.Add(title);
        content.Children.Add(header);
        string instruction = menu.IsTransferring ? menu.ConfirmOverwrite ? "Confirm transfer overwrite" : "Choose destination block" : menu.ConfirmOverwrite ? "Confirm overwrite" : menu.IsSaving ? "Choose block to save" : "Choose block to load";
        string detail = menu.TransferSource is int source ? $"{PlayerSaveStore.GetLabel(source)}  →  {PlayerSaveStore.GetLabel(menu.SelectedSlot)}" : menu.ConfirmOverwrite ? "Confirm again to overwrite the selected save." : menu.IsQuick ? menu.IsSaving ? "Create a new quick save." : "Choose a quick save to load." : $"{transferHint}    {confirmHint}: Select    {cancelHint}: Back";
        if (menu.Message is string message)
        {
            detail = message;
        }
        UiPanel help = new() { GridRow = 2, Caption = "HELP", CaptionFontSize = 13, CaptionHeight = 13, Padding = new UiThickness(8, 0) };
        help.Children.Add(new UiLabel { Text = instruction + "\n" + detail, FontSize = 18, VerticalTextAlignment = UiAlignment.Center });
        content.Children.Add(help);
        UiGrid blocks = BuildBlocks(menu.VisibleEntries, !menu.IsSaving && !menu.IsTransferring);
        blocks.GridRow = 3;
        foreach (UiButton block in blocks.Children.OfType<UiButton>())
        {
            int slot = int.Parse(block.Id![10..], System.Globalization.CultureInfo.InvariantCulture);
            block.Click = () => menu.SelectSlot(slot);
        }
        content.Children.Add(blocks);
        UiGrid actions = new() { GridRow = 4, Columns = menu.IsQuick ? 4 : 5, Rows = 1, Gap = 5 };
        actions.Children.Add(new UiButton { Id = "save-confirm", GridColumn = 0, Text = menu.ConfirmOverwrite ? "Overwrite" : menu.IsTransferring ? "Copy here" : menu.IsSaving ? "Save" : "Load", FontSize = 18, Enabled = menu.Entries.Count > 0 && (menu.IsTransferring || menu.IsSaving || menu.Entries[menu.Selection].CanLoad), Click = menu.RequestConfirm, PointerSize = 0 });
        if (!menu.IsQuick)
        {
            actions.Children.Add(new UiButton { Id = "save-transfer", GridColumn = 1, Text = menu.IsTransferring ? "Cancel copy" : menu.Bank == 0 ? "Copy to original" : "Copy to Memoria", FontSize = 16, Enabled = menu.IsTransferring || menu.CanTransfer, Click = menu.IsTransferring ? menu.CancelTransfer : menu.RequestTransfer, PointerSize = 0 });
        }
        int pageColumn = menu.IsQuick ? 1 : 2;
        actions.Children.Add(new UiButton { Id = "save-previous", GridColumn = pageColumn, Text = "Previous", FontSize = 18, Enabled = menu.Page > 0, Click = () => menu.RequestPage(-1), PointerSize = 0 });
        actions.Children.Add(new UiButton { Id = "save-next", GridColumn = pageColumn + 1, Text = "Next", FontSize = 18, Enabled = menu.Page + 1 < menu.PageCount, Click = () => menu.RequestPage(1), PointerSize = 0 });
        actions.Children.Add(new UiButton { Id = "save-back", GridColumn = pageColumn + 2, Text = "Back", FontSize = 18, Click = menu.RequestCancel, PointerSize = 0 });
        content.Children.Add(actions);
        root.Children.Add(content);
        Children.Add(root);
    }

    internal static IEnumerable<UiButton> FindButtons(UiElement parent)
    {
        foreach (UiElement child in parent.Children)
        {
            if (child is UiButton button)
            {
                yield return button;
            }
            foreach (UiButton descendant in FindButtons(child))
            {
                yield return descendant;
            }
        }
    }

    internal static UiView Build(PlayerSaveMenu menu, string transferHint, string navigationHint, string? confirmHint = null, string? cancelHint = null)
    {
        UiView view = new(new PlayerSaveView(menu, transferHint, navigationHint, confirmHint ?? new KeyboardInput().BindingPrompt(ControlAction.Confirm), cancelHint ?? new KeyboardInput().BindingPrompt(ControlAction.Cancel))) { DesignWidth = 960, DesignHeight = 672 };
        view.Resize(960, 672);
        foreach (UiButton bank in FindButtons(view.Root).Where(button => button.Id?.StartsWith("save-bank-", StringComparison.Ordinal) == true))
        {
            Action? selectBank = bank.Click;
            bank.Click = () =>
            {
                selectBank?.Invoke();
                FocusSelectedBlock(view, menu);
            };
        }
        FocusSelectedBlock(view, menu);
        return view;
    }

    private static void FocusSelectedBlock(UiView view, PlayerSaveMenu menu)
    {
        UiButton? selected = FindButtons(view.Root).FirstOrDefault(button => button.Id == "save-slot-" + menu.SelectedSlot);
        if (selected is not null)
        {
            view.Focus(selected);
        }
    }

    internal static UiGrid BuildBlocks(IReadOnlyList<PlayerSaveEntry> entries, bool loading)
    {
        UiGrid blocks = new() { Columns = 1, Rows = Math.Max(PlayerSaveMenu.EntriesPerPage, entries.Count), Gap = 3 };
        for (int index = 0; index < entries.Count; index++)
        {
            PlayerSaveEntry entry = entries[index];
            UiButton block = new() { GridRow = index, Id = "save-slot-" + entry.Slot, HighlightSelection = false, PointerSize = 20 };
            UiGrid metadata = new() { Margin = new UiThickness(35, 3, 8, 3), Gap = 6 };
            metadata.RowDefinitions.Add(UiGridLength.Star());
            metadata.ColumnDefinitions.Add(UiGridLength.Star());
            metadata.ColumnDefinitions.Add(UiGridLength.Pixel(200));
            metadata.ColumnDefinitions.Add(UiGridLength.Pixel(204));
            UiGrid description = new() { Height = 60, VerticalAlignment = UiAlignment.Center, Gap = 0 };
            description.ColumnDefinitions.Add(UiGridLength.Star());
            description.RowDefinitions.Add(UiGridLength.Pixel(30));
            description.RowDefinitions.Add(UiGridLength.Pixel(30));
            description.Children.Add(new UiLabel { Text = entry.Label, FontSize = 20, VerticalTextAlignment = UiAlignment.Center, Muted = loading && !entry.CanLoad });
            description.Children.Add(new UiLabel { GridRow = 1, Text = entry.Details, FontSize = 20, VerticalTextAlignment = UiAlignment.Center, Muted = loading && !entry.CanLoad });
            metadata.Children.Add(description);
            UiGrid times = new() { GridColumn = 1, Height = 60, VerticalAlignment = UiAlignment.Center, Gap = 0 };
            times.ColumnDefinitions.Add(UiGridLength.Star());
            times.RowDefinitions.Add(UiGridLength.Pixel(30));
            times.RowDefinitions.Add(UiGridLength.Pixel(30));
            times.Children.Add(new UiLabel { Text = entry.CanLoad ? entry.PlayTimeText : "", FontSize = 20, VerticalTextAlignment = UiAlignment.Center });
            times.Children.Add(new UiLabel { GridRow = 1, Text = entry.SavedAtText, FontSize = 16, VerticalTextAlignment = UiAlignment.Center });
            metadata.Children.Add(times);
            UiGrid portraits = new() { GridColumn = 2, Margin = new UiThickness(4), Gap = 4 };
            portraits.RowDefinitions.Add(UiGridLength.Star());
            for (int member = 0; member < entry.PartyPortraits.Count; member++)
            {
                portraits.ColumnDefinitions.Add(UiGridLength.Pixel(62));
                portraits.Children.Add(new UiImage { GridColumn = member, Source = new UiImageSource(UiImageKind.CharacterPortrait, entry.PartyPortraits[member]) });
            }
            metadata.Children.Add(portraits);
            block.Children.Add(metadata);
            blocks.Children.Add(block);
        }
        return blocks;
    }
}
