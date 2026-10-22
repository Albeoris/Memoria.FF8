using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class ListLayoutTests
{
    [Theory]
    [InlineData(300, 20)]
    [InlineData(228, 93)]
    public void LeadingMarkersKeepCategoryAndTextColumnsAlignedAcrossRowsAndPages(int width, int rowHeight)
    {
        UiImageSource marker = new(UiImageKind.Icon, 207);
        UiImageSource category = new(UiImageKind.Icon, 216);
        UiList list = new()
        {
            Items = ["First", "Learning", "Third"],
            SecondaryItems = ["0 / 40", "7 / 40", "0 / 40"],
            LeadingImages = [null, marker, null],
            Images = [category, category, category],
            Paginated = true,
            PageSize = 2,
            FitPageRows = true,
            AutoRowHeight = false,
            AutoSecondaryWidth = true,
            TextFlow = UiTextFlow.SingleLine,
            RowHeight = rowHeight,
            FontSize = rowHeight / 2f
        };
        list.Arrange(new UiRect(0, 0, width, rowHeight * 2 + 24));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        UiRect markerBounds = Assert.Single(canvas.Images, image => image.Source == marker).Bounds;
        var categories = canvas.Images.Where(image => image.Source == category).ToArray();
        Assert.Equal(categories[0].Bounds.X, categories[1].Bounds.X);
        Assert.True(markerBounds.Right <= categories[1].Bounds.X);
        Assert.Equal(canvas.Texts[0].Bounds.X, canvas.Texts[2].Bounds.X);
        Assert.True(categories[1].Bounds.Right <= canvas.Texts[2].Bounds.X);
        list.Key(UiKey.PageDown);
        canvas.Images.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.DoesNotContain(canvas.Images, image => image.Source == marker);
        Assert.Equal(categories[0].Bounds.X, Assert.Single(canvas.Images).Bounds.X);
    }

    [Fact]
    public void MeasuredValueColumnsKeepAFontScaledGapFromPrimaryNames()
    {
        UiList list = new() { Items = ["Geezard"], SecondaryItems = ["3"], TextFlow = UiTextFlow.SingleLine, FontSize = 72, RowHeight = 93, AutoSecondaryWidth = true, TextInset = 0 };
        list.Arrange(new UiRect(0, 0, 600, 120));
        MeasuringCanvas canvas = new();

        list.Draw(canvas, new UiTheme());

        Assert.Equal(14.4f, canvas.Texts[1].Clip.X - canvas.Texts[0].Bounds.Right, 3);
    }

    [Fact]
    public void SingleLineRowsPreserveOneLineAndUseMeasuredLineHeightForPagination()
    {
        UiList list = new() { Items = ["abcdefghijklmnop", "abcdefghijklmnop", "abcdefghijklmnop"], TextFlow = UiTextFlow.SingleLine, RowHeight = 20, FontSize = 48, TextInset = 0, Paginated = true };
        list.Arrange(new UiRect(0, 0, 104, 120));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(2, list.PageCount);
        Assert.Equal(2, list.EffectivePageSize);
        Assert.All(canvas.Texts.Where(text => !text.Text.StartsWith("P.")), text => Assert.Equal(48, text.Bounds.Height));
        Assert.Equal(new[] { UiTextFlow.SingleLine, UiTextFlow.SingleLine }, canvas.Flows);
        list.Pointer(20, 60);
        Assert.Equal(1, list.SelectedIndex);
        list.Key(UiKey.PageDown);
        Assert.Equal(2, list.SelectedIndex);
    }

    [Fact]
    public void OversizedArtworkAndPointerGuttersLeavePrimaryNamesReadable()
    {
        UiList list = new()
        {
            Items = ["A readable name"],
            SecondaryItems = ["100"],
            Images = [new UiImageSource(UiImageKind.Icon, 1)],
            TrailingImages = [new UiImageSource(UiImageKind.Icon, 2)],
            TextFlow = UiTextFlow.SingleLine,
            RowHeight = 93,
            FontSize = 72,
            TextInset = 105,
            PointerSize = 60
        };
        UiView view = new(list);
        view.Resize(228, 300);
        MeasuringCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        Assert.True(canvas.Texts[0].Bounds.Width >= 228 * 0.4f - 0.001f);
        Assert.All(canvas.Images, image => Assert.True(image.Bounds.Width <= 228 * 0.16f));
        float effectiveInset = canvas.Images[0].Bounds.X - 2;
        Assert.True(effectiveInset <= 228 * 0.12f);
        Assert.Equal(effectiveInset - 4, Assert.Single(canvas.Pointers).Right, 3);
        Assert.True(canvas.Pointers[0].X >= -0.001f);
        Assert.True(canvas.Pointers[0].Height <= canvas.Texts[0].Bounds.Height);
        Assert.True(canvas.Texts[0].Bounds.Right <= canvas.Texts[1].Bounds.X);
    }

    [Fact]
    public void Wrapped_rows_share_geometry_for_drawing_scrolling_and_pointer_selection()
    {
        UiList list = new() { Items = ["abcdefgh", "ab", "abcdefghijkl", "ab"], RowHeight = 20, FontSize = 20, TextInset = 0, ShowScrollbar = false };
        UiView view = new(list);
        view.Resize(44, 80);
        MeasuringCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        Assert.Equal(new[] { 40f, 20f, 60f }, canvas.Texts.Select(text => text.Bounds.Height));
        Assert.Equal(new[] { 0f, 40f, 60f }, canvas.Texts.Select(text => text.Bounds.Y));
        list.Pointer(10, 45);
        Assert.Equal(1, list.SelectedIndex);
        list.Key(UiKey.Down);
        Assert.Equal(1, list.ScrollOffset);
        canvas.Texts.Clear();
        canvas.Pointers.Clear();
        view.Draw(canvas, new UiTheme());

        Assert.Equal(new[] { "ab", "abcdefghijkl" }, canvas.Texts.Select(text => text.Text));
        Assert.Equal(new[] { 0f, 20f }, canvas.Texts.Select(text => text.Bounds.Y));
        Assert.Equal(50, Assert.Single(canvas.Pointers).Y + canvas.Pointers[0].Height / 2);
        list.Pointer(10, 75);
        Assert.Equal(2, list.SelectedIndex);
    }

    [Fact]
    public void Wrapped_pagination_preserves_full_rows_and_uses_the_same_hit_regions()
    {
        UiList list = new() { Paginated = true, Items = ["abcdefgh", "ab", "abcdefghijkl", "ab", "ab"], RowHeight = 20, FontSize = 20, TextInset = 0 };
        list.Arrange(new UiRect(0, 0, 44, 104));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(3, list.PageCount);
        Assert.Equal(2, list.EffectivePageSize);
        Assert.Equal(new[] { "abcdefgh", "ab", "P.1 / 3" }, canvas.Texts.Select(text => text.Text));
        list.Key(UiKey.PageDown);
        Assert.Equal(2, list.SelectedIndex);
        Assert.Equal(2, list.ScrollOffset);
        canvas.Texts.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(new[] { "abcdefghijkl", "ab", "P.2 / 3" }, canvas.Texts.Select(text => text.Text));
        list.Pointer(10, 65);
        Assert.Equal(3, list.SelectedIndex);
        list.Key(UiKey.PageDown);
        Assert.Equal(4, list.SelectedIndex);
        list.Key(UiKey.PageUp);
        Assert.Equal(2, list.SelectedIndex);
        list.Arrange(new UiRect(0, 0, 124, 104));
        list.Draw(new MeasuringCanvas(), new UiTheme());
        Assert.Equal(2, list.SelectedIndex);
        Assert.Equal(1, list.PageCount);
        Assert.Equal(0, list.ScrollOffset);
    }

    [Fact]
    public void Secondary_text_is_measured_in_its_own_column_and_can_align_to_the_right()
    {
        UiList list = new() { Items = ["ab", "ab"], SecondaryItems = ["abcdefgh", "x"], SecondaryWidth = 0.5f, SecondaryAlignment = UiAlignment.End, RowHeight = 20, FontSize = 20, TextInset = 0 };
        list.Arrange(new UiRect(0, 0, 84, 100));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(40, canvas.Texts[0].Bounds.Height);
        Assert.Equal(40, canvas.Texts[1].Bounds.Height);
        Assert.Equal(40, canvas.Texts[2].Bounds.Y);
        Assert.Equal(70, canvas.Texts[3].Bounds.X);
        Assert.Equal(80, canvas.Texts[3].Clip.Right);
        list.Pointer(10, 50);
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void Single_line_secondary_alignment_uses_unwrapped_width_for_overflowing_values()
    {
        UiList list = new() { Items = ["ab", "ab"], SecondaryItems = ["abcdefgh", "x"], SecondaryWidth = 0.7f, SecondaryAlignment = UiAlignment.End, TextFlow = UiTextFlow.SingleLine, RowHeight = 20, FontSize = 20, TextInset = 0 };
        list.Arrange(new UiRect(0, 0, 84, 100));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(24, canvas.Texts[1].Bounds.X);
        Assert.Equal(56, canvas.Texts[1].Bounds.Width);
        Assert.Equal(80, canvas.Texts[1].Clip.Right);
        Assert.Equal(70, canvas.Texts[3].Bounds.X);
        Assert.Equal(10, canvas.Texts[3].Bounds.Width);
        Assert.Equal(80, canvas.Texts[3].Clip.Right);
    }

    [Fact]
    public void Row_measurements_are_cached_and_refresh_when_text_or_columns_change()
    {
        UiList list = new() { Items = ["abcdefgh"], RowHeight = 20, FontSize = 20, TextInset = 0 };
        list.Arrange(new UiRect(0, 0, 84, 100));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        int calls = canvas.Measurements;
        list.Draw(canvas, new UiTheme());
        Assert.Equal(calls, canvas.Measurements);
        list.SecondaryItems = ["x"];
        list.SecondaryWidth = 0.5f;
        canvas.Texts.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(40, canvas.Texts[0].Bounds.Height);
        list.Items[0] = "ab";
        canvas.Texts.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(20, canvas.Texts[0].Bounds.Height);
    }

    [Fact]
    public void A_row_taller_than_a_page_is_clipped_before_the_footer()
    {
        UiList list = new() { Paginated = true, Items = ["abcdefghijkl", "ab"], RowHeight = 20, FontSize = 20, TextInset = 0 };
        list.Arrange(new UiRect(0, 0, 44, 64));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(40, canvas.Texts[0].Clip.Bottom);
        List<int> confirmed = [];
        list.Confirmed = confirmed.Add;
        list.Pointer(22, 50);
        Assert.Empty(confirmed);
        Assert.Equal(1, list.SelectedIndex);
    }

    [Theory]
    [InlineData(true, 2, 40)]
    [InlineData(false, 3, 20)]
    public void Fitted_pages_can_expand_wrapped_rows_or_explicitly_keep_fixed_capacity(bool automatic, int expectedCapacity, float expectedHeight)
    {
        UiList list = new() { Paginated = true, PageSize = 3, FitPageRows = true, AutoRowHeight = automatic, Items = ["abcdefgh", "ab", "ab", "ab", "ab", "ab", "ab"], RowHeight = 30, FontSize = 20, TextInset = 0 };
        list.Arrange(new UiRect(0, 0, 44, 84));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(expectedHeight, canvas.Texts[0].Bounds.Height);
        Assert.Equal(expectedCapacity, list.EffectivePageSize);
        Assert.All(canvas.Texts.Where(text => !text.Text.StartsWith("P.")), text => Assert.True(text.Bounds.Bottom <= 60));
    }

    [Fact]
    public void Right_alignment_preserves_wrap_width_when_visible_glyphs_are_narrower_than_their_advances()
    {
        UiList list = new() { Items = ["ab"], SecondaryItems = ["abc"], SecondaryWidth = 0.5f, SecondaryAlignment = UiAlignment.End, RowHeight = 20, FontSize = 20, TextInset = 0 };
        list.Arrange(new UiRect(0, 0, 84, 100));
        MeasuringCanvas canvas = new() { GlyphInset = 2 };
        list.Draw(canvas, new UiTheme());
        var secondary = canvas.Texts[1];
        Assert.Equal(52, secondary.Bounds.X);
        Assert.Equal(40, secondary.Bounds.Width);
        Assert.Equal(20, secondary.WrappedHeight);
        Assert.Equal(80, secondary.Clip.Right);
    }

    [Fact]
    public void Automatic_value_width_and_mixed_icon_gutters_stay_fixed_across_pages()
    {
        UiImageSource icon = new(UiImageKind.Icon, 1);
        UiImageSource marker = new(UiImageKind.Icon, 2);
        UiList list = new()
        {
            Items = ["Fire", "Blizzard", "Thunder"],
            SecondaryItems = ["9", "100", "99999"],
            Images = [icon, null, icon],
            TrailingImages = [marker, null],
            AutoSecondaryWidth = true,
            SecondaryAlignment = UiAlignment.End,
            TrailingImagesBeforeSecondary = true,
            Paginated = true,
            PageSize = 2,
            RowHeight = 20,
            FontSize = 20,
            TextInset = 0
        };
        list.Arrange(new UiRect(0, 0, 224, 64));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(20, canvas.Texts[0].Bounds.X);
        Assert.Equal(20, canvas.Texts[2].Bounds.X);
        Assert.Equal(130, canvas.Texts[0].Bounds.Width);
        Assert.Equal(canvas.Texts[0].Bounds.Width, canvas.Texts[2].Bounds.Width);
        Assert.Equal(170, canvas.Texts[1].Clip.X);
        Assert.Equal(canvas.Texts[1].Clip.X, canvas.Texts[3].Clip.X);
        Assert.Equal(220, canvas.Texts[1].Bounds.X + 10);
        Assert.Equal(220, canvas.Texts[3].Bounds.X + 30);
        Assert.True(canvas.Texts[0].Bounds.Right < canvas.Images[1].Bounds.X);
        Assert.True(canvas.Images[1].Bounds.Right < canvas.Texts[1].Clip.X);
        Assert.Equal(2, list.PageCount);

        list.Key(UiKey.PageDown);
        canvas.Texts.Clear();
        canvas.Images.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal("Thunder", canvas.Texts[0].Text);
        Assert.Equal(20, canvas.Texts[0].Bounds.X);
        Assert.Equal(130, canvas.Texts[0].Bounds.Width);
        Assert.Equal(170, canvas.Texts[1].Clip.X);
        Assert.Equal(220, canvas.Texts[1].Bounds.X + 50);
        Assert.Single(canvas.Images);
    }

    [Fact]
    public void Shared_leading_gutter_expands_unmarked_rows_and_updates_pagination_and_hit_testing()
    {
        UiList list = new()
        {
            Items = ["abcdefgh", "abcdefgh", "ab"],
            Images = [new UiImageSource(UiImageKind.Icon, 1)],
            RowHeight = 20,
            FontSize = 20,
            TextInset = 0,
            Paginated = true
        };
        list.Arrange(new UiRect(0, 0, 104, 64));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(1, list.PageCount);
        Assert.Equal(20, canvas.Texts[0].Bounds.X);
        Assert.Equal(20, canvas.Texts[1].Bounds.X);
        Assert.All(canvas.Texts.Where(text => !text.Text.StartsWith("P.")), text => Assert.Equal(20, text.Bounds.Height));

        list.Arrange(new UiRect(0, 0, 84, 84));
        canvas.Texts.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(2, list.PageCount);
        Assert.Equal(40, canvas.Texts[0].Bounds.Height);
        list.Key(UiKey.PageDown);
        canvas.Texts.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(20, canvas.Texts[0].Bounds.X);
        Assert.Equal(40, canvas.Texts[0].Bounds.Height);
        list.Pointer(30, 25);
        Assert.Equal(1, list.SelectedIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Automatic_columns_measure_wrapped_names_with_the_same_icon_gutters_as_drawing(bool markerBeforeValue)
    {
        UiList list = new()
        {
            Items = ["abcdef", "abcdef", "abc"],
            SecondaryItems = ["100", "9", "50"],
            Images = [new UiImageSource(UiImageKind.Icon, 1)],
            TrailingImages = [new UiImageSource(UiImageKind.Icon, 2)],
            AutoSecondaryWidth = true,
            SecondaryAlignment = UiAlignment.End,
            TrailingImagesBeforeSecondary = markerBeforeValue,
            RowHeight = 20,
            FontSize = 20,
            TextInset = 0,
            Paginated = true
        };
        list.Arrange(new UiRect(0, 0, 124, 104));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(1, list.PageCount);
        Assert.Equal(3, list.EffectivePageSize);
        Assert.Equal(40, canvas.Texts[0].Bounds.Height);
        Assert.Equal(40, canvas.Texts[2].Bounds.Height);
        Assert.Equal(40, canvas.Texts[2].Bounds.Y);
        Assert.Equal(canvas.Texts[0].Bounds.Width, canvas.Texts[2].Bounds.Width);
        Assert.Equal(canvas.Texts[0].WrappedHeight, canvas.Texts[0].Bounds.Height);
        Assert.Equal(canvas.Texts[2].WrappedHeight, canvas.Texts[2].Bounds.Height);
        list.Pointer(30, 60);
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void Automatic_value_column_preserves_glyph_advance_wrap_width_and_refreshes_changed_values()
    {
        UiList list = new()
        {
            Items = ["abc", "abc"],
            SecondaryItems = ["100", "9"],
            AutoSecondaryWidth = true,
            SecondaryAlignment = UiAlignment.End,
            RowHeight = 20,
            FontSize = 20,
            TextInset = 0
        };
        list.Arrange(new UiRect(0, 0, 104, 100));
        MeasuringCanvas canvas = new() { GlyphInset = 2 };
        list.Draw(canvas, new UiTheme());
        Assert.Equal(72, canvas.Texts[1].Clip.X);
        Assert.Equal(20, canvas.Texts[1].WrappedHeight);
        Assert.Equal(20, canvas.Texts[1].Bounds.Height);
        Assert.Equal(100, canvas.Texts[1].Bounds.X + 28);
        Assert.True(canvas.Texts[0].Bounds.Right <= canvas.Texts[1].Clip.X - 4);

        list.SecondaryItems[1] = "10000";
        canvas.Texts.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(52, canvas.Texts[1].Clip.X);
        Assert.Equal(52, canvas.Texts[3].Clip.X);
        Assert.Equal(20, canvas.Texts[3].WrappedHeight);
    }

    [Theory]
    [InlineData(9, 180, 1, 9)]
    [InlineData(9, 179, 2, 7)]
    [InlineData(10, 180, 2, 7)]
    public void FooterIsReservedOnlyWhenRowsOverflowTheFullViewport(int count, int height, int pages, int visible)
    {
        UiList list = new() { Paginated = true, AutoRowHeight = false, RowHeight = 20, Items = Enumerable.Range(0, count).Select(index => $"row{index}").ToList() };
        list.Arrange(new UiRect(0, 0, 160, height));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(pages, list.PageCount);
        Assert.Equal(visible, list.VisibleRows);
        Assert.Equal(visible, canvas.Texts.Count(text => text.Text.StartsWith("row")));
        Assert.Equal(pages > 1, canvas.Texts.Any(text => text.Text.StartsWith("P.")));
        list.Pointer(30, 175);
        if (pages == 1)
        {
            Assert.Equal(8, list.SelectedIndex);
        }
    }

    [Theory]
    [InlineData(100, 1, 3)]
    [InlineData(99, 2, 2)]
    public void WrappedRowsUseTheirMeasuredHeightToDecideWhetherTheFooterIsNeeded(int height, int pages, int visible)
    {
        UiList list = new() { Paginated = true, RowHeight = 20, FontSize = 20, TextInset = 0, Items = ["abcdefgh", "ab", "abcdefgh"] };
        list.Arrange(new UiRect(0, 0, 44, height));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(pages, list.PageCount);
        Assert.Equal(visible, list.VisibleRows);
        Assert.Equal(40, canvas.Texts[0].Bounds.Height);
        Assert.Equal(pages > 1, canvas.Texts.Any(text => text.Text.StartsWith("P.")));
    }

    [Fact]
    public void ResizeFromWrappedPagesToOnePagePreservesTheLastSelectionAndRestoresBottomHitTesting()
    {
        UiList list = new() { Paginated = true, RowHeight = 20, FontSize = 20, TextInset = 0, Items = ["abcdefgh", "ab", "abcdefghijkl", "ab", "ab"], SelectedIndex = 4 };
        list.Arrange(new UiRect(0, 0, 44, 104));
        list.Draw(new MeasuringCanvas(), new UiTheme());
        Assert.Equal(3, list.PageCount);
        Assert.Equal(2, list.CurrentPage);

        list.Arrange(new UiRect(0, 0, 124, 104));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(1, list.PageCount);
        Assert.Equal(0, list.CurrentPage);
        Assert.Equal(0, list.ScrollOffset);
        Assert.Equal(4, list.SelectedIndex);
        Assert.Equal(list.Items, canvas.Texts.Select(text => text.Text));
        list.Key(UiKey.PageUp);
        list.Key(UiKey.PageDown);
        Assert.Equal(4, list.SelectedIndex);
        list.SelectedIndex = 0;
        list.Pointer(20, 95);
        Assert.Equal(4, list.SelectedIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitPageSizeStillRequiresPagesWhenAllNaturalRowsFit(bool fitRows)
    {
        UiList list = new() { Paginated = true, AutoRowHeight = false, PageSize = 2, FitPageRows = fitRows, RowHeight = 20, Items = ["a", "b", "c"] };
        list.Arrange(new UiRect(0, 0, 160, 200));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());

        Assert.Equal(2, list.PageCount);
        Assert.Equal(2, list.EffectivePageSize);
        Assert.Contains(canvas.Texts, text => text.Text == "P.1 / 2");
        list.Key(UiKey.PageDown);
        Assert.Equal(2, list.SelectedIndex);
        Assert.Equal(2, list.ScrollOffset);
    }

    [Fact]
    public void FittedRowsRecomputeTheirMinimumHeightWhenWrappedContentNeedsOrReleasesTheFooter()
    {
        UiList list = new() { Paginated = true, PageSize = 3, FitPageRows = true, RowHeight = 20, FontSize = 20, TextInset = 0, Items = ["abcdefgh", "ab", "ab"] };
        list.Arrange(new UiRect(0, 0, 44, 90));
        list.Draw(new MeasuringCanvas(), new UiTheme());
        Assert.Equal(2, list.PageCount);
        Assert.Equal(2, list.EffectivePageSize);
        list.SelectedIndex = 2;

        list.Arrange(new UiRect(0, 0, 44, 120));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(1, list.PageCount);
        Assert.Equal(0, list.ScrollOffset);
        Assert.Equal(2, list.SelectedIndex);
        Assert.Equal(3, canvas.Texts.Count);
        Assert.All(canvas.Texts, text => Assert.Equal(40, text.Bounds.Height));
        Assert.Equal(120, canvas.Texts[^1].Bounds.Bottom);
    }

    [Fact]
    public void FittedIconGuttersKeepWrappingIndependentOfTheFooter()
    {
        UiList list = new()
        {
            Paginated = true,
            PageSize = 3,
            FitPageRows = true,
            RowHeight = 20,
            FontSize = 20,
            TextInset = 0,
            Images = [new UiImageSource(UiImageKind.Icon, 1)],
            Items = ["abcdefgh", "ab", "ab"]
        };
        list.Arrange(new UiRect(0, 0, 110, 90));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(2, list.PageCount);
        Assert.Equal(2, list.EffectivePageSize);
        Assert.Equal(30, canvas.Texts[0].Bounds.X);
        Assert.Equal(40, canvas.Texts[0].Bounds.Height);
        Assert.Equal("P.1 / 2", canvas.Texts[^1].Text);
        int measurements = canvas.Measurements;
        canvas.Texts.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(measurements, canvas.Measurements);
        Assert.Equal("P.1 / 2", canvas.Texts[^1].Text);
        list.Key(UiKey.PageDown);
        Assert.Equal(2, list.SelectedIndex);

        list.Arrange(new UiRect(0, 0, 110, 120));
        canvas.Texts.Clear();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(1, list.PageCount);
        Assert.Equal(0, list.ScrollOffset);
        Assert.Equal(2, list.SelectedIndex);
        Assert.Equal(list.Items, canvas.Texts.Select(text => text.Text));
        Assert.All(canvas.Texts, text => Assert.Equal(40, text.Bounds.Height));
    }

    [Fact]
    public void FractionalFittedRowHeightDoesNotCreateItsOwnFooter()
    {
        UiList list = new() { Paginated = true, AutoRowHeight = false, PageSize = 9, FitPageRows = true, Items = Enumerable.Repeat("a", 9).ToList() };
        list.Arrange(new UiRect(0, 0, 160, 181));
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(1, list.PageCount);
        Assert.Equal(9, canvas.Texts.Count);
        Assert.Equal(181, canvas.Texts[^1].Bounds.Bottom, 3);
    }

    [Fact]
    public void ChangedTextAndRowHeightReleaseTheFooterWithoutRearranging()
    {
        UiList list = new() { Paginated = true, RowHeight = 20, FontSize = 20, TextInset = 0, Items = ["abcdefgh", "abcdefgh", "ab"] };
        list.Arrange(new UiRect(0, 0, 44, 80));
        list.Draw(new MeasuringCanvas(), new UiTheme());
        Assert.Equal(3, list.PageCount);
        list.SelectedIndex = 2;
        list.Items[0] = "ab";
        list.Items[1] = "ab";
        MeasuringCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        Assert.Equal(1, list.PageCount);
        Assert.Equal(0, list.ScrollOffset);
        Assert.Equal(2, list.SelectedIndex);
        Assert.Equal(3, canvas.Texts.Count);

        list.RowHeight = 30;
        list.Draw(new MeasuringCanvas(), new UiTheme());
        Assert.Equal(3, list.PageCount);
        list.RowHeight = 20;
        list.Draw(new MeasuringCanvas(), new UiTheme());
        Assert.Equal(1, list.PageCount);
        Assert.Equal(2, list.SelectedIndex);
    }

    private sealed class MeasuringCanvas : IUiCanvas
    {
        private readonly Stack<UiRect> _clips = new();
        public int Measurements { get; private set; }
        public float GlyphInset { get; init; }
        public List<(string Text, UiRect Bounds, UiRect Clip, float WrappedHeight)> Texts { get; } = [];
        public List<UiRect> Pointers { get; } = [];
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];
        public List<UiTextFlow> Flows { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            Measurements++;
            return Layout(text, fontSize, maximumWidth);
        }

        private UiSize Layout(string text, float fontSize, float maximumWidth)
        {
            if (float.IsPositiveInfinity(maximumWidth))
            {
                return new UiSize(Math.Max(0, text.Length * fontSize / 2 - GlyphInset), fontSize);
            }
            int columns = Math.Max(1, (int)(maximumWidth / (fontSize / 2)));
            int lines = Math.Max(1, (text.Length + columns - 1) / columns);
            return new UiSize(Math.Max(0, Math.Min(text.Length, columns) * fontSize / 2 - GlyphInset), lines * fontSize);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, bounds, _clips.Peek(), Layout(text, fontSize, bounds.Width).Height));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
        {
            Flows.Add(flow);
            Text(text, fontSize, bounds, color);
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers.Add(bounds);
        }

        public void PushClip(UiRect bounds)
        {
            _clips.Push(_clips.Count == 0 ? bounds : _clips.Peek().Intersect(bounds));
        }

        public void PopClip()
        {
            _clips.Pop();
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add((source, bounds));
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
    }
}
