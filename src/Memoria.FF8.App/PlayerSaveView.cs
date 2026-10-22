using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>One save browser for manual saving, loading, Continue and quick actions.</summary>
internal sealed partial class PlayerSaveView : UiElement
{
    private readonly PlayerSaveMenu menu;
    private readonly float textScale;
    private readonly UiGrid content;
    private readonly UiGrid information;
    private readonly UiGrid header;
    private readonly UiPanel help;
    private readonly UiLabel pageLabel;
    private readonly string navigationSuffix;
    private readonly UiGrid actions;
    private UiGrid blocks;

    private PlayerSaveView(PlayerSaveMenu menu, string transferHint, string navigationHint, string confirmHint, string cancelHint, float textScale)
    {
        this.menu = menu;
        this.textScale = textScale;
        navigationSuffix = menu.IsQuick ? "" : "    " + navigationHint;
        UiAnchorPanel root = new();
        content = new() { Anchors = new UiAnchors(0.02f, 0.02f, 0.98f, 0.98f), Gap = 5 };
        content.ColumnDefinitions.Add(UiGridLength.Star());
        content.RowDefinitions.Add(UiGridLength.Pixel(BankHeight(textScale)));
        content.RowDefinitions.Add(UiGridLength.Auto);
        content.RowDefinitions.Add(UiGridLength.Star());
        content.RowDefinitions.Add(UiGridLength.Pixel(ActionHeight(textScale)));
        UiGrid banks = BuildBanks(menu.Bank, menu.IsQuick, textScale);
        foreach (UiButton tab in banks.Children.OfType<UiButton>())
        {
            int bank = tab.GridColumn;
            tab.Click = () => menu.SelectBank(bank);
        }
        content.Children.Add(banks);
        information = new UiGrid { GridRow = 1, Gap = 5 };
        information.ColumnDefinitions.Add(UiGridLength.Star());
        information.RowDefinitions.Add(UiGridLength.Auto);
        information.RowDefinitions.Add(UiGridLength.Auto);
        header = new() { Gap = 5 };
        header.RowDefinitions.Add(UiGridLength.Star());
        header.ColumnDefinitions.Add(UiGridLength.Star(3.7f));
        header.ColumnDefinitions.Add(UiGridLength.Star(1.3f));
        UiPanel info = new() { Caption = "INFO.", CaptionFontSize = UiTheme.DefaultCaptionFontSize * textScale, CaptionHeight = 13 * textScale, Padding = new UiThickness(8, 0) };
        pageLabel = new UiLabel { Text = $"Page {menu.Page + 1} / {menu.PageCount}" + navigationSuffix, FontSize = 16 * textScale, TextFlow = UiTextFlow.SingleLine, VerticalTextAlignment = UiAlignment.Center };
        info.Children.Add(pageLabel);
        header.Children.Add(info);
        UiPanel title = new() { GridColumn = 1, Padding = new UiThickness(8, 0) };
        string name = menu.Mode switch
        {
            PlayerSaveMenuMode.QuickLoad => "Quick Load",
            PlayerSaveMenuMode.QuickSave => "Quick Save",
            PlayerSaveMenuMode.Load => "Load",
            _ => "Save"
        };
        title.Children.Add(new UiLabel { Text = menu.IsTransferring ? "Transfer" : name, FontSize = 25 * textScale, TextFlow = UiTextFlow.SingleLine, VerticalTextAlignment = UiAlignment.Center });
        header.Children.Add(title);
        information.Children.Add(header);
        string instruction = menu.IsTransferring ? menu.ConfirmOverwrite ? "Confirm transfer overwrite" : "Choose destination block" : menu.ConfirmOverwrite ? "Confirm overwrite" : menu.IsSaving ? "Choose block to save" : "Choose block to load";
        string detail = menu.TransferSource is int source ? $"{PlayerSaveStore.GetLabel(source)}  →  {PlayerSaveStore.GetLabel(menu.SelectedSlot)}" : menu.ConfirmOverwrite ? "Confirm again to overwrite the selected save." : menu.IsQuick ? menu.IsSaving ? "Create a new quick save." : "Choose a quick save to load." : $"{transferHint}    {confirmHint}: Select    {cancelHint}: Back";
        if (menu.Message is string message)
        {
            detail = message;
        }
        help = new() { GridRow = 1, Caption = "HELP", CaptionFontSize = UiTheme.DefaultCaptionFontSize * textScale, CaptionHeight = 13 * textScale, Padding = new UiThickness(8, 0) };
        help.Children.Add(new UiLabel { Text = instruction + "\n" + detail, FontSize = 18 * textScale, TextFlow = UiTextFlow.SingleLine, VerticalTextAlignment = UiAlignment.Center });
        information.Children.Add(help);
        content.Children.Add(information);
        blocks = CreateVisibleBlocks();
        content.Children.Add(blocks);
        actions = new() { GridRow = 3, Columns = menu.IsQuick ? 4 : 5, Rows = 1, Gap = 5 };
        actions.Children.Add(new UiButton { Id = "save-confirm", GridColumn = 0, Text = menu.ConfirmOverwrite ? "Overwrite" : menu.IsTransferring ? "Copy here" : menu.IsSaving ? "Save" : "Load", FontSize = 18 * textScale, Enabled = menu.Entries.Count > 0 && (menu.IsTransferring || menu.IsSaving || menu.Entries[menu.Selection].CanLoad), Click = menu.RequestConfirm, PointerSize = 0 });
        if (!menu.IsQuick)
        {
            actions.Children.Add(new UiButton { Id = "save-transfer", GridColumn = 1, Text = menu.IsTransferring ? "Cancel copy" : menu.Bank == 0 ? "Copy to original" : "Copy to Memoria", FontSize = 16 * textScale, Enabled = menu.IsTransferring || menu.CanTransfer, Click = menu.IsTransferring ? menu.CancelTransfer : menu.RequestTransfer, PointerSize = 0 });
        }
        int pageColumn = menu.IsQuick ? 1 : 2;
        actions.Children.Add(new UiButton { Id = "save-previous", GridColumn = pageColumn, Text = "Previous", FontSize = 18 * textScale, Enabled = menu.Page > 0, Click = () => menu.RequestPage(-1), PointerSize = 0 });
        actions.Children.Add(new UiButton { Id = "save-next", GridColumn = pageColumn + 1, Text = "Next", FontSize = 18 * textScale, Enabled = menu.Page + 1 < menu.PageCount, Click = () => menu.RequestPage(1), PointerSize = 0 });
        actions.Children.Add(new UiButton { Id = "save-back", GridColumn = pageColumn + 2, Text = "Back", FontSize = 18 * textScale, Click = menu.RequestCancel, PointerSize = 0 });
        content.Children.Add(actions);
        root.Children.Add(content);
        Children.Add(root);
    }

    public override void Arrange(UiRect bounds)
    {
        float width = bounds.Width * 0.96f;
        float height = bounds.Height * 0.96f;
        float blockHeight = blocks.Children.OfType<UiButton>().Select(button => button.Children[0].Measure(new UiSize(width, height)).Height + button.Children[0].Margin.Vertical).DefaultIfEmpty(60 * textScale + 6).Max();
        float controlsHeight = BankHeight(textScale) + ActionHeight(textScale) + content.Gap * 3;
        UiSize available = new(width, height);
        float stackedHeight = header.Measure(available).Height + help.Measure(available).Height + information.Gap;
        bool compact = height - controlsHeight - stackedHeight < blockHeight;
        information.ColumnDefinitions.Clear();
        information.RowDefinitions.Clear();
        information.ColumnDefinitions.Add(UiGridLength.Star());
        information.RowDefinitions.Add(UiGridLength.Auto);
        header.GridColumn = 0;
        header.GridRow = 0;
        help.GridColumn = compact ? 1 : 0;
        help.GridRow = compact ? 0 : 1;
        if (compact)
        {
            information.ColumnDefinitions.Add(UiGridLength.Star());
        }
        else
        {
            information.RowDefinitions.Add(UiGridLength.Auto);
        }
        float informationHeight = information.Measure(available).Height;
        float remaining = Math.Max(0, height - controlsHeight - informationHeight);
        int capacity = Math.Clamp((int)((remaining + 3) / (blockHeight + 3)), 1, PlayerSaveMenu.EntriesPerPage);
        if (capacity != menu.PageCapacity)
        {
            menu.SetPageCapacity(capacity);
            UiGrid replacement = CreateVisibleBlocks();
            // Keep the selected button instance so measurement-driven pagination preserves focus.
            for (int index = 0; index < replacement.Children.Count; index++)
            {
                UiElement next = replacement.Children[index];
                UiElement? existing = blocks.Children.FirstOrDefault(child => child.Id == next.Id);
                if (existing is not null)
                {
                    existing.GridRow = next.GridRow;
                    replacement.Children[index] = existing;
                }
            }
            int position = content.Children.IndexOf(blocks);
            content.Children[position] = replacement;
            blocks = replacement;
        }
        pageLabel.Text = $"Page {menu.Page + 1} / {menu.PageCount}" + navigationSuffix;
        foreach (UiButton action in actions.Children.OfType<UiButton>())
        {
            if (action.Id == "save-previous")
            {
                action.Enabled = menu.Page > 0;
            }
            else if (action.Id == "save-next")
            {
                action.Enabled = menu.Page + 1 < menu.PageCount;
            }
        }
        base.Arrange(bounds);
    }

    private UiGrid CreateVisibleBlocks()
    {
        UiGrid result = BuildBlocks(menu.VisibleEntries, !menu.IsSaving && !menu.IsTransferring, textScale, menu.PageCapacity);
        result.GridRow = 2;
        foreach (UiButton block in result.Children.OfType<UiButton>())
        {
            int slot = int.Parse(block.Id![10..], System.Globalization.CultureInfo.InvariantCulture);
            block.Click = () => menu.SelectSlot(slot);
        }
        return result;
    }

    private static float BankHeight(float scale) => Math.Max(72, 36 * scale);
    private static float HeaderHeight(float scale) => Math.Max(40, 37 * scale);
    private static float HelpHeight(float scale) => Math.Max(70, 46 * scale);
    private static float ActionHeight(float scale) => Math.Max(34, 27 * scale);

    internal static void ConfigureTextScale(PlayerSaveMenu menu, float textScale)
    {
        if (!float.IsFinite(textScale) || textScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textScale));
        }
        if (menu.ConfiguredTextScale == textScale)
        {
            return;
        }
        menu.ConfiguredTextScale = textScale;
        float available = 672 * 0.96f - BankHeight(textScale) - HeaderHeight(textScale) - HelpHeight(textScale) - ActionHeight(textScale) - 20;
        int capacity = Math.Clamp((int)(available / (60 * textScale + 9)), 1, PlayerSaveMenu.EntriesPerPage);
        menu.SetPageCapacity(capacity);
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

    internal static UiView Build(PlayerSaveMenu menu, string transferHint, string navigationHint, string? confirmHint = null, string? cancelHint = null, float textScale = 1)
    {
        ConfigureTextScale(menu, textScale);
        UiView view = new(new PlayerSaveView(menu, transferHint, navigationHint, confirmHint ?? new KeyboardInput().BindingPrompt(ControlAction.Confirm), cancelHint ?? new KeyboardInput().BindingPrompt(ControlAction.Cancel), textScale)) { DesignWidth = 960, DesignHeight = 672 };
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

    internal static UiGrid BuildBlocks(IReadOnlyList<PlayerSaveEntry> entries, bool loading, float textScale = 1, int pageCapacity = PlayerSaveMenu.EntriesPerPage)
    {
        UiGrid blocks = new() { Columns = 1, Rows = Math.Max(pageCapacity, entries.Count), Gap = 3 };
        for (int index = 0; index < entries.Count; index++)
        {
            PlayerSaveEntry entry = entries[index];
            UiButton block = new() { GridRow = index, Id = "save-slot-" + entry.Slot, HighlightSelection = false, PointerSize = 20 };
            UiGrid metadata = new() { Margin = new UiThickness(35, 3, 8, 3), VerticalAlignment = UiAlignment.Center, Gap = 0 };
            metadata.RowDefinitions.Add(UiGridLength.Auto);
            metadata.RowDefinitions.Add(UiGridLength.Auto);
            metadata.ColumnDefinitions.Add(UiGridLength.Star(2.1f));
            metadata.ColumnDefinitions.Add(UiGridLength.Star());
            metadata.ColumnDefinitions.Add(UiGridLength.Pixel(204));
            UiThickness columnGap = new(0, 0, 6 * textScale, 0);
            metadata.Children.Add(new UiLabel { Text = entry.Label, FontSize = 20 * textScale, Margin = columnGap, TextFlow = UiTextFlow.SingleLine, VerticalTextAlignment = UiAlignment.Center, Muted = loading && !entry.CanLoad });
            metadata.Children.Add(new UiLabel { GridRow = 1, Text = entry.Details, FontSize = 20 * textScale, Margin = columnGap, TextFlow = UiTextFlow.SingleLine, VerticalTextAlignment = UiAlignment.Center, Muted = loading && !entry.CanLoad });
            metadata.Children.Add(new UiLabel { GridColumn = 1, Text = entry.CanLoad ? entry.PlayTimeText : "", FontSize = 20 * textScale, Margin = columnGap, TextFlow = UiTextFlow.SingleLine, VerticalTextAlignment = UiAlignment.Center });
            metadata.Children.Add(new UiLabel { GridRow = 1, GridColumn = 1, Text = entry.SavedAtText, FontSize = 16 * textScale, Margin = columnGap, TextFlow = UiTextFlow.SingleLine, VerticalTextAlignment = UiAlignment.Center });
            UiGrid portraits = new() { GridColumn = 2, GridRowSpan = 2, Margin = new UiThickness(4), Gap = 4 };
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
