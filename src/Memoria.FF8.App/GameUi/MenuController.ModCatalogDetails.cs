using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed class CatalogDetailsElement : UiElement
    {
        private readonly MenuController menus;
        private readonly UiScrollPanel scroll;
        private readonly UiButton back;
        private readonly string text;
        private float measuredWidth = -1;
        private float measuredFont;

        public CatalogDetailsElement(MenuController menus)
        {
            this.menus = menus;
            text = string.Join("\n\n", menus.LibraryDetails());
            scroll = new() { Id = "library-details-scroll", OffsetChanged = offset => menus.catalogDetailsOffset = offset };
            scroll.Children.Add(new UiLabel { Text = text, TextFlow = UiTextFlow.Wrap });
            back = new() { Id = "library-details-back", Text = "Back", Click = menus.Back, DrawBackground = true };
            Children.Add(scroll);
            Children.Add(back);
            menus.catalogDetailsScroll = scroll;
        }

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            UiRect content = bounds.Inset(12);
            float buttonHeight = 28 * menus.TextScale;
            scroll.Arrange(new(content.X, content.Y, content.Width, Math.Max(0, content.Height - buttonHeight - 8)));
            back.Arrange(new(content.X, content.Bottom - buttonHeight, Math.Min(content.Width, 112 * menus.TextScale), buttonHeight));
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            theme.Panel(canvas, scroll.Bounds);
            float font = 17 * menus.TextScale;
            float width = Math.Max(1, scroll.Bounds.Width);
            if (width == measuredWidth && font == measuredFont)
            {
                return;
            }
            UiLabel label = (UiLabel)scroll.Children[0];
            label.FontSize = font;
            float height = MeasureCatalogText(canvas, text, font, width);
            if (height > scroll.Bounds.Height)
            {
                height = MeasureCatalogText(canvas, text, font, Math.Max(1, width - scroll.ScrollbarWidth));
            }
            float offset = menus.catalogDetailsOffset;
            scroll.ContentHeight = height;
            scroll.Offset = offset;
            measuredWidth = width;
            measuredFont = font;
        }

    }

    private static float MeasureCatalogText(IUiCanvas canvas, string text, float font, float width)
    {
        try
        {
            return canvas.MeasureText(text, font, width).Height + font;
        }
        catch (NotSupportedException)
        {
            return text.Split('\n').Sum(line => Math.Max(1, MathF.Ceiling(line.Length * font * 0.55f / width))) * font * 1.4f;
        }
    }
}
