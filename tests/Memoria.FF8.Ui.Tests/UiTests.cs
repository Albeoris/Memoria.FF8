using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class UiTests
{
    [Fact]
    public void Translucent_panel_bands_do_not_blend_twice_at_shared_pixel_edges()
    {
        RecordingCanvas canvas = new();
        UiTheme theme = new() { PanelBackgroundAlpha = 127 };
        theme.Panel(canvas, new UiRect(0.25f, 0, 733.5f, 400));
        Assert.Equal(128, canvas.Fills.Count);
        Assert.All(canvas.Fills, fill => Assert.Equal(127, fill.Color.A));
        for (int index = 1; index < canvas.Fills.Count; index++)
        {
            Assert.Equal(canvas.Fills[index - 1].Bounds.Right, canvas.Fills[index].Bounds.X);
        }
        Assert.True(canvas.Lines > 0);
    }

    [Fact]
    public void Paginated_lists_keep_fixed_page_offsets_and_clamp_rows_on_the_last_page()
    {
        UiList list = At(new UiList { Paginated = true, RowHeight = 20, Items = Enumerable.Range(0, 8).Select(index => index.ToString()).ToList() }, new UiRect(0, 0, 160, 84));
        List<int> changes = new();
        list.SelectionChanged = changes.Add;
        list.SelectedIndex = 2;

        Assert.Equal(3, list.VisibleRows);
        Assert.Equal(3, list.EffectivePageSize);
        Assert.Equal(3, list.PageCount);
        Assert.True(list.Key(UiKey.Right));
        Assert.Equal(5, list.SelectedIndex);
        Assert.Equal(1, list.CurrentPage);
        Assert.Equal(3, list.ScrollOffset);
        list.Key(UiKey.PageDown);
        Assert.Equal(7, list.SelectedIndex);
        Assert.Equal(6, list.ScrollOffset);
        list.Key(UiKey.Right);
        Assert.Equal(7, list.SelectedIndex);
        list.Key(UiKey.Left);
        Assert.Equal(4, list.SelectedIndex);
        list.Key(UiKey.PageUp);
        Assert.Equal(1, list.SelectedIndex);
        list.Key(UiKey.Up);
        list.Key(UiKey.Down);
        Assert.Equal(new[] { 5, 7, 4, 1, 0, 1 }, changes);
        list.SelectedIndex = 2;
        list.Key(UiKey.Down);
        Assert.Equal(3, list.SelectedIndex);
        Assert.Equal(3, list.ScrollOffset);
    }

    [Theory]
    [InlineData(2, 2, 4, 2)]
    [InlineData(10, 3, 3, 3)]
    public void Explicit_page_size_is_capped_to_full_visible_rows_without_fitting(int pageSize, int capacity, int pageCount, int drawnRows)
    {
        UiList list = At(new UiList { Paginated = true, PageSize = pageSize, RowHeight = 20, Items = Enumerable.Range(0, 8).Select(index => index.ToString()).ToList() }, new UiRect(0, 0, 160, 94));
        list.Key(UiKey.PageDown);
        RecordingCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(capacity, list.EffectivePageSize);
        Assert.Equal(pageCount, list.PageCount);
        Assert.Equal(capacity, list.ScrollOffset);
        Assert.Equal(drawnRows + 1, canvas.Texts.Count);
        Assert.Equal(capacity.ToString(), canvas.Texts[0]);
        Assert.Equal($"P.2 / {pageCount}", canvas.Texts[^1]);
        Assert.All(canvas.AlignedTexts.Take(drawnRows), text => Assert.Equal(20, text.Bounds.Height));
    }

    [Fact]
    public void Paginated_lists_draw_only_full_rows_and_keep_footer_separate_from_item_clicks()
    {
        UiList list = At(new UiList { Paginated = true, RowHeight = 20, ShowSelection = false, Items = Enumerable.Range(0, 8).Select(index => index.ToString()).ToList() }, new UiRect(10, 10, 160, 94));
        List<int> confirmed = new();
        List<int> changes = new();
        list.Confirmed = confirmed.Add;
        list.SelectionChanged = changes.Add;
        RecordingCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(new[] { "0", "1", "2", "P.1 / 3" }, canvas.Texts);
        Assert.All(canvas.AlignedTexts.Take(3), text => Assert.True(text.Bounds.Bottom <= 70));
        Assert.Equal(80, canvas.AlignedTexts[^1].Bounds.Y);
        Assert.Equal(104, canvas.AlignedTexts[^1].Bounds.Bottom);
        Assert.Equal(90, canvas.AlignedTexts[^1].Bounds.X + canvas.AlignedTexts[^1].Bounds.Width / 2);
        Assert.Empty(canvas.Fills);
        Assert.Equal(4, canvas.Lines);
        list.Pointer(30, 75);
        list.Pointer(90, 92);
        Assert.Empty(confirmed);
        Assert.Empty(changes);
        list.Pointer(150, 92);
        Assert.Equal(3, list.SelectedIndex);
        Assert.Equal(new[] { 3 }, changes);
        Assert.Empty(confirmed);
        list.Pointer(60, 35);
        Assert.Equal(new[] { 4 }, confirmed);
        list.Pointer(30, 92);
        Assert.Equal(1, list.SelectedIndex);
        Assert.Equal(new[] { 3, 4, 1 }, changes);
    }

    [Fact]
    public void Final_page_does_not_repeat_previous_items_or_activate_empty_rows()
    {
        UiList list = At(new UiList { Paginated = true, RowHeight = 20, Items = Enumerable.Range(0, 8).Select(index => index.ToString()).ToList() }, new UiRect(0, 0, 160, 84));
        List<int> confirmed = new();
        list.Confirmed = confirmed.Add;
        list.CurrentPage = 2;
        RecordingCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(new[] { "6", "7", "P.3 / 3" }, canvas.Texts);
        Assert.Equal(6, list.ScrollOffset);
        list.Pointer(60, 50);
        Assert.Empty(confirmed);
        Assert.Equal(6, list.SelectedIndex);
        list.Pointer(150, 72);
        Assert.Equal(6, list.SelectedIndex);
        Assert.Empty(confirmed);
        list.Pointer(60, 30);
        Assert.Equal(new[] { 7 }, confirmed);
    }

    [Fact]
    public void Fitted_pages_render_requested_row_count_and_keep_selection_when_resized()
    {
        UiList list = new() { Paginated = true, PageSize = 10, FitPageRows = true, RowHeight = 40, Items = Enumerable.Range(0, 23).Select(index => index.ToString()).ToList() };
        UiView view = new(list);
        view.Resize(300, 224);
        list.SelectedIndex = 17;
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        Assert.Equal(10, list.VisibleRows);
        Assert.Equal(3, list.PageCount);
        Assert.Equal(10, list.ScrollOffset);
        Assert.Equal(Enumerable.Range(10, 10).Select(index => index.ToString()).Append("P.2 / 3"), canvas.Texts);
        Assert.All(canvas.AlignedTexts.Take(10), text => Assert.Equal(20, text.Bounds.Height));
        Assert.Equal(200, canvas.AlignedTexts[9].Bounds.Bottom);
        view.Resize(300, 324);
        canvas = new RecordingCanvas();
        view.Draw(canvas, new UiTheme());
        Assert.Equal(17, list.SelectedIndex);
        Assert.Equal(10, list.ScrollOffset);
        Assert.All(canvas.AlignedTexts.Take(10), text => Assert.Equal(30, text.Bounds.Height));
        Assert.Equal(300, canvas.AlignedTexts[9].Bounds.Bottom);
        list.Pointer(60, 45);
        Assert.Equal(11, list.SelectedIndex);
    }

    [Fact]
    public void Automatic_page_capacity_reflows_selection_after_resize_and_handles_empty_lists()
    {
        UiList list = At(new UiList { Paginated = true, RowHeight = 20, Items = Enumerable.Range(0, 8).Select(index => index.ToString()).ToList() }, new UiRect(0, 0, 160, 84));
        list.SelectedIndex = 7;
        list.Arrange(new UiRect(0, 0, 160, 104));
        Assert.Equal(4, list.EffectivePageSize);
        Assert.Equal(1, list.CurrentPage);
        Assert.Equal(4, list.ScrollOffset);
        list.ScrollOffset = 0;
        Assert.Equal(3, list.SelectedIndex);
        list.Items.Clear();
        Assert.Equal(-1, list.SelectedIndex);
        Assert.Equal(0, list.CurrentPage);
        Assert.Equal(0, list.PageCount);
        Assert.Equal(0, list.ScrollOffset);
        Assert.False(list.Key(UiKey.Right));
        list.Pointer(140, 92);
        RecordingCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Empty(canvas.Texts);
        Assert.Equal(0, canvas.Lines);
        list.Items.Add("only");
        list.Arrange(new UiRect(0, 0, 160, 30));
        canvas = new RecordingCanvas();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(1, list.VisibleRows);
        Assert.Equal(new[] { "only" }, canvas.Texts);
        list.Pointer(60, 2);
        list.Arrange(new UiRect(0, 0, 160, 64));
        canvas = new RecordingCanvas();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(new[] { "only" }, canvas.Texts);
        Assert.Equal(0, canvas.Lines);
    }

    [Fact]
    public void Secondary_text_uses_one_aligned_column_across_icons_and_pages()
    {
        UiList list = At(new UiList
        {
            Paginated = true,
            RowHeight = 30,
            SecondaryWidth = 0.4f,
            Items = ["Junction", "Ability", "Other"],
            SecondaryItems = ["Complete!", "20/30", "0/40"],
            Images = [new UiImageSource(UiImageKind.Icon, 1), null, new UiImageSource(UiImageKind.Icon, 2)],
            TrailingImages = [new UiImageSource(UiImageKind.Icon, 3)],
            EnabledItems = [true, false]
        }, new UiRect(0, 0, 300, 84));
        RecordingCanvas canvas = new();
        UiTheme theme = new();
        list.Draw(canvas, theme);

        Assert.Equal(new[] { "Junction", "Complete!", "Ability", "20/30", "P.1 / 2" }, canvas.Texts);
        Assert.Equal(canvas.AlignedTexts[1].Bounds.X, canvas.AlignedTexts[3].Bounds.X);
        Assert.True(canvas.AlignedTexts[0].Bounds.Right <= canvas.AlignedTexts[1].Bounds.X);
        Assert.True(canvas.AlignedTexts[1].Bounds.Right < canvas.Images[1].Bounds.X);
        Assert.True(canvas.Images[0].Bounds.Right < canvas.AlignedTexts[0].Bounds.X);
        Assert.Equal(theme.Muted, canvas.TextDetails[3].Color);
        float columnX = canvas.AlignedTexts[1].Bounds.X;
        list.Key(UiKey.Right);
        canvas = new RecordingCanvas();
        list.Draw(canvas, theme);
        Assert.Equal(new[] { "Other", "0/40", "P.2 / 2" }, canvas.Texts);
        Assert.Equal(columnX, canvas.AlignedTexts[1].Bounds.X);
    }

    [Fact]
    public void Mixed_trailing_markers_reserve_one_secondary_column_on_every_page()
    {
        UiImageSource marker = new(UiImageKind.Icon, 192);
        UiList list = At(new UiList
        {
            Paginated = true,
            PageSize = 2,
            RowHeight = 30,
            Items = ["Fire", "Blizzard", "Thunder"],
            SecondaryItems = ["100", "100", "100"],
            TrailingImages = [marker, null]
        }, new UiRect(0, 0, 240, 84));
        RecordingCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        UiRect marked = canvas.AlignedTexts[1].Bounds;
        UiRect unmarked = canvas.AlignedTexts[3].Bounds;
        Assert.Equal(marked.X, unmarked.X);
        Assert.Equal(marked.Width, unmarked.Width);
        Assert.True(marked.Right < Assert.Single(canvas.Images).Bounds.X);
        Assert.True(canvas.AlignedTexts[0].Bounds.Right <= marked.X);
        Assert.True(canvas.AlignedTexts[2].Bounds.Right <= unmarked.X);
        list.Key(UiKey.Right);
        canvas = new RecordingCanvas();
        list.Draw(canvas, new UiTheme());
        Assert.Empty(canvas.Images);
        Assert.Equal(marked.X, canvas.AlignedTexts[1].Bounds.X);
        Assert.Equal(marked.Width, canvas.AlignedTexts[1].Bounds.Width);
    }

    [Fact]
    public void Page_and_secondary_column_configuration_reject_invalid_values()
    {
        UiList list = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => list.PageSize = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.SecondaryWidth = -0.1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.SecondaryWidth = 1.1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.SecondaryWidth = float.NaN);
    }

    [Fact]
    public void Larger_menu_pointer_keeps_text_and_row_images_clear_of_the_hand()
    {
        UiList list = new() { Items = ["Potion"], Images = [new UiImageSource(UiImageKind.Icon, 1)], RowHeight = 40, PointerSize = 20, TextInset = 35 };
        UiView view = new(list);
        view.Resize(200, 40);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        UiRect pointer = Assert.Single(canvas.Pointers);
        UiRect image = Assert.Single(canvas.Images).Bounds;
        UiRect text = Assert.Single(canvas.AlignedTexts).Bounds;
        Assert.Equal(new UiRect(1, 10, 30, 20), pointer);
        Assert.True(pointer.Right < image.X);
        Assert.True(image.Right < text.X);
        Assert.Equal(20, pointer.Y + pointer.Height / 2);
    }

    [Fact]
    public void Horizontal_commands_share_width_and_route_navigation_and_scaled_pointer_input()
    {
        UiList list = new() { Orientation = UiOrientation.Horizontal, Items = ["Use", "Rearrange", "Sort"], RowHeight = 20 };
        List<int> selected = new();
        List<int> confirmed = new();
        list.SelectionChanged = selected.Add;
        list.Confirmed = confirmed.Add;
        UiView view = new(list) { DesignWidth = 300, DesignHeight = 60 };
        view.Resize(600, 120);

        Assert.True(view.Key(UiKey.Right));
        Assert.Equal(1, list.SelectedIndex);
        Assert.False(list.Key(UiKey.Down));
        view.Pointer(400, 100, true);
        view.Pointer(450, 100, true);
        Assert.Equal(new[] { 1, 2 }, selected);
        Assert.Equal(new[] { 2 }, confirmed);
        Assert.Equal(0, list.ScrollOffset);
        Assert.Equal(3, list.VisibleRows);

        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Equal(new[] { 46f, 246f, 446f }, canvas.AlignedTexts.Select(text => text.Bounds.X));
        Assert.All(canvas.AlignedTexts, text => Assert.Equal(120, text.Bounds.Height));
        Assert.Single(canvas.Fills);
        Assert.Equal(new UiRect(408, 50, 30, 20), Assert.Single(canvas.Pointers));

        list.Key(UiKey.Left);
        list.Key(UiKey.PageUp);
        Assert.Equal(0, list.SelectedIndex);
        list.Key(UiKey.PageDown);
        Assert.Equal(2, list.SelectedIndex);
        list.ScrollOffset = 20;
        Assert.Equal(0, list.ScrollOffset);
        view.Resize(900, 180);
        canvas = new RecordingCanvas();
        view.Draw(canvas, new UiTheme());
        Assert.Equal(new[] { 69f, 369f, 669f }, canvas.AlignedTexts.Select(text => text.Bounds.X));
        list.Items.Clear();
        list.Pointer(0, 0);
        Assert.False(list.Key(UiKey.Confirm));
    }

    [Fact]
    public void Muted_rows_remain_selectable_and_pointer_is_independent_of_selection_fill()
    {
        UiList list = new() { Items = ["Use", "Unavailable", "Sort"], EnabledItems = [true, false], HighlightSelection = false, RowHeight = 20 };
        List<int> confirmed = new();
        list.Confirmed = confirmed.Add;
        UiView view = new(list);
        view.Resize(120, 60);
        view.Key(UiKey.Down);
        view.Key(UiKey.Confirm);
        RecordingCanvas canvas = new();
        UiTheme theme = new();
        view.Draw(canvas, theme);

        Assert.Equal(new[] { 1 }, confirmed);
        Assert.Equal(new[] { theme.Foreground, theme.Muted, theme.Foreground }, canvas.TextDetails.Select(text => text.Color));
        Assert.Empty(canvas.Fills);
        Assert.Single(canvas.Pointers);
        view.Pointer(40, 25, true);
        Assert.Equal(new[] { 1, 1 }, confirmed);

        list.ShowSelection = false;
        canvas = new RecordingCanvas();
        view.Draw(canvas, theme);
        Assert.Empty(canvas.Pointers);
        list.Enabled = false;
        canvas = new RecordingCanvas();
        view.Draw(canvas, theme);
        Assert.All(canvas.TextDetails, text => Assert.Equal(theme.Muted, text.Color));
    }

    [Fact]
    public void Button_can_render_simple_text_with_custom_font_and_retains_native_pointer()
    {
        UiButton button = new() { Text = "Status", DrawBackground = false, HighlightSelection = false, FontSize = 22 };
        UiView view = new(button);
        view.Resize(120, 40);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Empty(canvas.Fills);
        Assert.Equal(0, canvas.Lines);
        Assert.Single(canvas.Pointers);
        Assert.Equal(22, Assert.Single(canvas.TextDetails).FontSize);

        button.DrawBackground = true;
        button.HighlightSelection = true;
        button.FontSize = 0;
        canvas = new RecordingCanvas();
        UiTheme theme = new();
        view.Draw(canvas, theme);
        Assert.Contains(canvas.Fills, fill => fill.Color == theme.Highlight);
        Assert.True(canvas.Lines > 0);
        Assert.Equal(theme.FontSize, Assert.Single(canvas.TextDetails).FontSize);

        UiAnchorPanel root = new();
        UiPanel panel = new() { Anchors = new UiAnchors(0.25f, 0.25f, 0.75f, 0.75f) };
        UiButton composite = new() { DrawBackground = false, HighlightSelection = false };
        UiLabel label = new() { Text = "Status" };
        composite.Children.Add(label);
        panel.Children.Add(composite);
        root.Children.Add(panel);
        view = new UiView(root);
        view.Resize(200, 120);
        canvas = new RecordingCanvas();
        view.Draw(canvas, theme);
        UiRect pointer = Assert.Single(canvas.Pointers);
        Assert.Equal(panel.Bounds.X, label.Bounds.X);
        Assert.Equal(label.Bounds.X - 4, pointer.Right);
        Assert.True(pointer.X < panel.Bounds.X);
        Assert.Equal(new UiRect(0, 0, 200, 120), Assert.Single(canvas.PointerClips));
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void Images_follow_anchor_bounds_and_letterbox_scaling_without_changing_identity()
    {
        UiImageSource source = new(UiImageKind.CharacterPortrait, 3);
        UiAnchorPanel root = new();
        root.Children.Add(new UiImage { Source = source, Anchors = new UiAnchors(0.25f, 0, 0.75f, 1) });
        UiView view = new(root) { DesignWidth = 100, DesignHeight = 100 };
        view.Resize(300, 200);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        Assert.Equal((source, new UiRect(100, 0, 100, 200)), Assert.Single(canvas.Images));
        Assert.Equal(new UiRect(100, 25, 100, 150), UiImage.Fit(canvas.Images[0].Bounds, 64, 96));
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void Scrolled_list_images_keep_their_row_identity_and_text_does_not_overlap()
    {
        UiImageSource first = new(UiImageKind.Icon, 1);
        UiImageSource second = new(UiImageKind.Icon, 2);
        UiList list = new() { Items = ["first", "second", "third"], Images = [first, second, null], RowHeight = 30 };
        UiView view = new(list);
        view.Resize(120, 30);
        list.SelectedIndex = 1;
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        var image = Assert.Single(canvas.Images);
        Assert.Equal(second, image.Source);
        Assert.True(image.Bounds.Right < Assert.Single(canvas.AlignedTexts).Bounds.X);
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void Trailing_markers_reserve_space_after_the_label()
    {
        UiImageSource marker = new(UiImageKind.Icon, 192);
        UiList list = new() { Items = ["Fire  100"], TrailingImages = [marker], RowHeight = 30 };
        UiView view = new(list);
        view.Resize(180, 30);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        var image = Assert.Single(canvas.Images);
        Assert.Equal(marker, image.Source);
        Assert.True(Assert.Single(canvas.AlignedTexts).Bounds.Right < image.Bounds.X);
    }

    [Fact]
    public void Selected_rows_forward_centered_text_and_the_native_pointer_through_view_scaling()
    {
        UiList list = new() { Items = ["Refresh"], RowHeight = 40, FontSize = 16 };
        UiView view = new(list) { DesignWidth = 100, DesignHeight = 100 };
        view.Resize(200, 200);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        var text = Assert.Single(canvas.AlignedTexts);
        Assert.Equal(UiAlignment.Center, text.Alignment);
        Assert.Equal(new UiRect(46, 0, 146, 80), text.Bounds);
        UiRect pointer = Assert.Single(canvas.Pointers);
        Assert.Equal(new UiRect(8, 30, 30, 20), pointer);
        Assert.Equal(text.Bounds.Y + text.Bounds.Height / 2, pointer.Y + pointer.Height / 2);
        Assert.Equal(0, canvas.Lines);
    }

    [Fact]
    public void RectangleUsesHalfOpenHitBoundariesAndEmptyIntersections()
    {
        UiRect bounds = new(10, 20, 30, 40);
        Assert.True(bounds.Contains(10, 20));
        Assert.True(bounds.Contains(39.9f, 59.9f));
        Assert.False(bounds.Contains(40, 20));
        Assert.False(bounds.Contains(10, 60));
        Assert.True(bounds.Intersect(new UiRect(40, 20, 10, 10)).IsEmpty);
        Assert.False(new UiRect(0, 0, -1, 10).Contains(0, 0));
    }

    [Fact]
    public void FocusSkipsDisabledHiddenClippedAndDisabledDescendants()
    {
        UiPanel root = At(new UiPanel(), new UiRect(0, 0, 100, 100));
        UiButton first = Button(0);
        UiButton disabled = Button(20);
        disabled.Enabled = false;
        UiButton hidden = Button(40);
        hidden.Visible = false;
        UiButton clipped = Button(200);
        UiPanel disabledPanel = At(new UiPanel { Enabled = false }, new UiRect(0, 0, 100, 100));
        disabledPanel.Children.Add(Button(60));
        UiButton last = Button(80);
        root.Children.Add(first);
        root.Children.Add(disabled);
        root.Children.Add(hidden);
        root.Children.Add(clipped);
        root.Children.Add(disabledPanel);
        root.Children.Add(last);
        UiView view = new(root);

        Assert.Same(first, view.FocusedElement);
        Assert.False(view.Focus(disabled));
        Assert.True(view.Key(UiKey.Tab));
        Assert.Same(last, view.FocusedElement);
        last.Enabled = false;
        Assert.Same(first, view.FocusedElement);
        Assert.False(last.IsFocused);
    }

    [Fact]
    public void KeyboardPagesScrollSelectedRowIntoViewAndClampsAtEnds()
    {
        UiList list = At(new UiList { RowHeight = 20, Items = Enumerable.Range(0, 10).Select(index => index.ToString()).ToList() }, new UiRect(0, 0, 100, 60));
        List<int> changes = new();
        list.SelectionChanged = changes.Add;
        UiView view = new(list);

        view.Key(UiKey.PageDown);
        Assert.Equal(3, list.SelectedIndex);
        Assert.Equal(1, list.ScrollOffset);
        view.Key(UiKey.PageDown);
        view.Key(UiKey.PageDown);
        Assert.Equal(9, list.SelectedIndex);
        Assert.Equal(7, list.ScrollOffset);
        view.Key(UiKey.Down);
        Assert.Equal(new[] { 3, 6, 9 }, changes);
        view.Key(UiKey.PageUp);
        view.Key(UiKey.PageUp);
        Assert.Equal(3, list.SelectedIndex);
        Assert.Equal(3, list.ScrollOffset);
        list.Items.Clear();
        Assert.Equal(-1, list.SelectedIndex);
        Assert.Equal(0, list.ScrollOffset);
        Assert.False(list.Key(UiKey.Confirm));
    }

    [Fact]
    public void PointerMapsLetterboxedDesignCoordinatesAndActivatesOncePerPress()
    {
        int count = 0;
        UiAnchorPanel root = new();
        UiButton button = new() { Anchors = new UiAnchors(0.1f, 0.1f, 0.4f, 0.3f), Click = () => count++ };
        root.Children.Add(button);
        UiView view = new(root) { DesignWidth = 100, DesignHeight = 100 };
        view.Resize(400, 200);

        view.Pointer(120, 20, true);
        view.Pointer(121, 21, true);
        Assert.Equal(1, count);
        view.Pointer(120, 20, false);
        view.Pointer(180, 20, true);
        Assert.Equal(1, count);
        view.Pointer(180, 20, false);
        view.Pointer(120, 20, true);
        Assert.Equal(2, count);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Clips, clip => clip.X == 120 && clip.Y == 20 && clip.Width == 60 && Math.Abs(clip.Height - 40) < 0.001f);
    }

    [Fact]
    public void RenderingBalancesIntersectingClipsAndProceduralThemeIsGrayscale()
    {
        UiPanel root = At(new UiPanel { Caption = "Menu" }, new UiRect(0, 0, 100, 100));
        root.Children.Add(At(new UiLabel { Text = "Clipped" }, new UiRect(80, 80, 100, 40)));
        RecordingCanvas canvas = new();
        new UiView(root).Draw(canvas, new UiTheme());

        Assert.Equal(0, canvas.ClipDepth);
        Assert.Contains(new UiRect(80, 80, 20, 20), canvas.Clips);
        Assert.Equal(128, canvas.Fills.Count);
        Assert.All(canvas.Fills, fill =>
        {
            Assert.Equal(fill.Color.R, fill.Color.G);
            Assert.Equal(fill.Color.G, fill.Color.B);
        });
        Assert.True(canvas.Fills[0].Color.R > canvas.Fills[^1].Color.R);
        Assert.Equal(8, canvas.Lines);
        Assert.Contains("Menu", canvas.Texts);
    }

    [Fact]
    public void ClippingIsRestoredWhenAChildRendererThrows()
    {
        UiElement root = At(new UiElement(), new UiRect(0, 0, 100, 100));
        root.Children.Add(At(new ThrowingElement(), root.Bounds));
        RecordingCanvas canvas = new();
        Assert.Throws<InvalidOperationException>(() => root.Draw(canvas, new UiTheme()));
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void HitTestingUsesTopmostChildAndParentClip()
    {
        UiElement root = At(new UiElement(), new UiRect(0, 0, 100, 100));
        UiButton lower = At(new UiButton(), new UiRect(0, 0, 100, 100));
        UiButton upper = At(new UiButton(), new UiRect(50, 50, 100, 100));
        root.Children.Add(lower);
        root.Children.Add(upper);
        Assert.Same(upper, root.HitTest(50, 50));
        Assert.Null(root.HitTest(110, 60));
        upper.Enabled = false;
        Assert.Same(lower, root.HitTest(50, 50));
    }

    [Fact]
    public void GridAndStackArrangeNestedControlsWithoutNegativeSizes()
    {
        UiGrid grid = new() { Columns = 2, Gap = 10 };
        UiStack stack = new() { Gap = 4 };
        stack.Children.Add(new UiButton());
        stack.Children.Add(new UiButton());
        grid.Children.Add(stack);
        grid.Children.Add(new UiButton());
        grid.Arrange(new UiRect(0, 0, 210, 100));
        Assert.Equal(new UiRect(0, 0, 100, 100), stack.Bounds);
        Assert.Equal(new UiRect(0, 52, 100, 48), stack.Children[1].Bounds);
        grid.Arrange(new UiRect(0, 0, 1, 1));
        Assert.Equal(0, stack.Bounds.Width);
    }

    [Fact]
    public void SliderAndToggleRouteKeyboardAndOnlyNotifyOnInputChanges()
    {
        UiSlider slider = At(new UiSlider { Max = 100, Step = 25, Value = 50 }, new UiRect(0, 0, 100, 40));
        List<float> values = new();
        slider.ValueChanged = values.Add;
        slider.Value = 75;
        Assert.Empty(values);
        slider.Key(UiKey.Right);
        slider.Key(UiKey.Right);
        slider.Key(UiKey.Left);
        Assert.Equal(new[] { 100f, 75f }, values);
        slider.Pointer(24, 20);
        Assert.Equal(0, slider.Value);
        slider.Enabled = false;
        Assert.False(slider.Key(UiKey.Right));
        slider.Pointer(99, 20);
        Assert.Equal(0, slider.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => slider.Step = 0);

        UiToggle toggle = At(new UiToggle(), new UiRect(0, 0, 100, 30));
        List<bool> toggles = new();
        toggle.ValueChanged = toggles.Add;
        toggle.Key(UiKey.Right);
        toggle.Key(UiKey.Right);
        toggle.Key(UiKey.Left);
        toggle.Pointer(10, 10);
        Assert.Equal(new[] { true, false, true }, toggles);
    }

    [Fact]
    public void ProgressBarClampsValuesAndDoesNotJoinFocusNavigation()
    {
        UiProgressBar bar = At(new UiProgressBar { Max = 100, Value = 150 }, new UiRect(0, 0, 100, 10));
        RecordingCanvas canvas = new();
        UiView view = new(bar);
        view.Draw(canvas, new UiTheme());
        Assert.Null(view.FocusedElement);
        Assert.Equal(new UiRect(1, 1, 98, 8), canvas.Fills[^1].Bounds);
        bar.Value = -1;
        bar.Draw(canvas, new UiTheme());
        Assert.Equal(0, canvas.Fills[^1].Bounds.Width);
        bar.Value = float.NaN;
        bar.Draw(canvas, new UiTheme());
        Assert.Equal(0, canvas.Fills[^1].Bounds.Width);
    }

    [Fact]
    public void WeightedGridResizesWithoutTemplateCoordinatesAndRecursivelyArrangesPanelContent()
    {
        UiGrid root = new() { Gap = 0 };
        root.ColumnDefinitions.Add(UiGridLength.Star(2));
        root.ColumnDefinitions.Add(UiGridLength.Star());
        root.RowDefinitions.Add(UiGridLength.Star());
        root.RowDefinitions.Add(UiGridLength.Star(2));
        UiPanel panel = new() { Caption = "Party", CaptionHeight = 20, Padding = 5, GridRowSpan = 2, Margin = new UiThickness(5, 10) };
        UiLabel label = new() { Text = "Squall", Margin = 2 };
        panel.Children.Add(label);
        UiButton button = new() { GridColumn = 1, GridRow = 1 };
        root.Children.Add(panel);
        root.Children.Add(button);
        UiView view = new(root);
        view.Resize(300, 150);
        Assert.Equal(new UiRect(5, 10, 190, 130), panel.Bounds);
        Assert.Equal(new UiRect(12, 37, 176, 96), label.Bounds);
        Assert.Equal(new UiRect(200, 50, 100, 100), button.Bounds);
        view.Resize(600, 300);
        Assert.Equal(new UiRect(5, 10, 390, 280), panel.Bounds);
        Assert.Equal(new UiRect(400, 100, 200, 200), button.Bounds);
        Assert.Same(button, root.HitTest(400, 100));
        Assert.Null(root.HitTest(600, 100));
    }

    [Fact]
    public void GridCombinesPixelsAutoAndStarWithSpanAndAlignedFixedDimensions()
    {
        UiGrid root = new() { Gap = 5 };
        root.ColumnDefinitions.Add(UiGridLength.Pixel(40));
        root.ColumnDefinitions.Add(UiGridLength.Auto);
        root.ColumnDefinitions.Add(UiGridLength.Star());
        root.RowDefinitions.Add(UiGridLength.Star());
        UiElement auto = new() { GridColumn = 1, Width = 60, Height = 20, VerticalAlignment = UiAlignment.Center };
        UiElement star = new() { GridColumn = 2, Width = 30, Height = 10, HorizontalAlignment = UiAlignment.End, VerticalAlignment = UiAlignment.End, Margin = 5 };
        root.Children.Add(auto);
        root.Children.Add(star);
        root.Arrange(new UiRect(0, 0, 300, 100));
        Assert.Equal(new UiRect(45, 40, 60, 20), auto.Bounds);
        Assert.Equal(new UiRect(265, 85, 30, 10), star.Bounds);
        UiElement span = new() { GridColumnSpan = 3, Height = 15 };
        root.Children.Add(span);
        root.Arrange(new UiRect(0, 0, 300, 100));
        Assert.Equal(new UiRect(0, 0, 300, 15), span.Bounds);
    }

    [Fact]
    public void NormalizedAnchorsResizeAndAlignOverlaysInsidePadding()
    {
        UiAnchorPanel root = new() { Padding = 10 };
        UiElement overlay = new() { Anchors = new UiAnchors(0.5f, 0.5f, 1, 1), Width = 40, Height = 20, HorizontalAlignment = UiAlignment.End, VerticalAlignment = UiAlignment.Center, Margin = 5 };
        root.Children.Add(overlay);
        root.Arrange(new UiRect(0, 0, 220, 120));
        Assert.Equal(new UiRect(165, 75, 40, 20), overlay.Bounds);
        root.Arrange(new UiRect(0, 0, 420, 220));
        Assert.Equal(new UiRect(365, 150, 40, 20), overlay.Bounds);
        overlay.Anchors = new UiAnchors(0.8f, 0, 0.2f, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => root.Arrange(new UiRect(0, 0, 100, 100)));
    }

    [Fact]
    public void ExplicitGridRejectsInvalidSpansAndSmallViewportsKeepTrackSizesNonnegative()
    {
        UiGrid grid = new() { Gap = 0 };
        grid.ColumnDefinitions.Add(UiGridLength.Pixel(100));
        grid.ColumnDefinitions.Add(UiGridLength.Auto);
        grid.RowDefinitions.Add(UiGridLength.Star());
        UiElement first = new();
        UiElement second = new() { GridColumn = 1, Width = 100 };
        grid.Children.Add(first);
        grid.Children.Add(second);
        grid.Arrange(new UiRect(0, 0, 50, 20));
        Assert.True(first.Bounds.Width >= 0);
        Assert.True(second.Bounds.Width >= 0);
        Assert.True(second.Bounds.Right <= 50.001f);
        second.GridColumnSpan = 2;
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Arrange(new UiRect(0, 0, 100, 100)));
    }

    [Fact]
    public void ReadOnlyListCanScrollWithoutDrawingSelectionHighlight()
    {
        UiList list = new() { ShowSelection = false, Items = Enumerable.Range(0, 10).Select(index => index.ToString()).ToList(), RowHeight = 20 };
        UiView view = new(list);
        view.Resize(100, 40);
        view.Key(UiKey.PageDown);
        Assert.Equal(1, list.ScrollOffset);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Single(canvas.Fills);
        Assert.Equal(2, canvas.Fills[0].Bounds.Width);
        Assert.Equal(0, canvas.Lines);
    }

    [Fact]
    public void ViewCachesLayoutUntilViewportChangesOrLayoutIsInvalidated()
    {
        CountingElement root = new();
        UiView view = new(root);
        view.Resize(100, 100);
        view.Resize(100, 100);
        view.Draw(new RecordingCanvas(), new UiTheme());
        Assert.Equal(1, root.Arrangements);
        view.InvalidateLayout();
        view.Draw(new RecordingCanvas(), new UiTheme());
        Assert.Equal(2, root.Arrangements);
        view.Resize(200, 100);
        Assert.Equal(3, root.Arrangements);
        view.DesignWidth = 300;
        view.Draw(new RecordingCanvas(), new UiTheme());
        Assert.Equal(4, root.Arrangements);
        Assert.Equal(300, root.Bounds.Width);
    }

    private static UiButton Button(float y)
    {
        return At(new UiButton(), new UiRect(0, y, 100, 20));
    }

    private static T At<T>(T element, UiRect bounds) where T : UiElement
    {
        element.Arrange(bounds);
        return element;
    }

    private sealed class ThrowingElement : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            throw new InvalidOperationException("Deliberate test failure.");
        }
    }

    [Fact]
    public void Offscreen_parent_relative_anchors_preserve_native_positions_and_parent_clipping()
    {
        var root = new UiAnchorPanel();
        var child = new UiLabel { Text = "Timer", Anchors = new UiAnchors(0.25f, 1.05f, 0.75f, 1.2f) };
        root.Children.Add(child);
        root.Arrange(new UiRect(0, 0, 400, 200));
        Assert.Equal(210f, child.Bounds.Y, 3);
        Assert.Null(root.HitTest(110, 220));
        var canvas = new RecordingCanvas();
        root.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Clips, clip => clip.IsEmpty);
        Assert.Equal(0, canvas.ClipDepth);
    }

    private sealed class CountingElement : UiElement
    {
        public int Arrangements { get; private set; }

        public override void Arrange(UiRect bounds)
        {
            Arrangements++;
            base.Arrange(bounds);
        }
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        private readonly Stack<UiRect> _clips = new();
        public int ClipDepth => _clips.Count;
        public int Lines { get; private set; }
        public List<UiRect> Clips { get; } = new();
        public List<(UiRect Bounds, UiColor Color)> Fills { get; } = new();
        public List<string> Texts { get; } = new();
        public List<(float FontSize, UiColor Color)> TextDetails { get; } = new();
        public List<(UiRect Bounds, UiAlignment Alignment)> AlignedTexts { get; } = new();
        public List<UiRect> Pointers { get; } = new();
        public List<UiRect> PointerClips { get; } = new();
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = new();

        public void Fill(UiRect bounds, UiColor color)
        {
            Fills.Add((bounds, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
            TextDetails.Add((fontSize, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            AlignedTexts.Add((bounds, verticalAlignment));
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add((source, bounds));
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers.Add(bounds);
            PointerClips.Add(_clips.Count > 0 ? _clips.Peek() : default);
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Lines++;
        }

        public void PushClip(UiRect bounds)
        {
            UiRect clip = _clips.Count == 0 ? bounds : _clips.Peek().Intersect(bounds);
            _clips.Push(clip);
            Clips.Add(clip);
        }

        public void PopClip()
        {
            _clips.Pop();
        }
    }
}
