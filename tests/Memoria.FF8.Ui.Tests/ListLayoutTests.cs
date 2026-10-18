using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class ListLayoutTests
{
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
        Assert.Equal(2, list.PageCount);
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

    private sealed class MeasuringCanvas : IUiCanvas
    {
        private readonly Stack<UiRect> _clips = new();
        public int Measurements { get; private set; }
        public float GlyphInset { get; init; }
        public List<(string Text, UiRect Bounds, UiRect Clip, float WrappedHeight)> Texts { get; } = [];
        public List<UiRect> Pointers { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            Measurements++;
            return Layout(text, fontSize, maximumWidth);
        }

        private UiSize Layout(string text, float fontSize, float maximumWidth)
        {
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
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
    }
}
