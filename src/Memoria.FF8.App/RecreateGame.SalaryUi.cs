using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private UiView? salaryWindowUi;
    private SalaryText? salaryOldText;
    private SalaryText? salaryNewText;
    private SalaryText? salaryTitleText;

    private void DrawSalaryUi(Rectangle bounds, NativeSalaryPresentation salary)
    {
        // TODO(TD-003): native UI rasterization remains outside the procedural redesign.
        if (salaryWindowUi is null)
        {
            var root = new UiAnchorPanel();
            var panel = new UiPanel { Anchors = new UiAnchors(0.55f, 0.03f, 0.985f, 0.225f), Padding = new UiThickness(8, 4) };
            var content = new UiGrid { Gap = 4 };
            content.ColumnDefinitions.Add(UiGridLength.Star());
            content.RowDefinitions.Add(UiGridLength.Star());
            content.RowDefinitions.Add(UiGridLength.Star(1.8f));
            salaryTitleText = new SalaryText { FontSize = 8, Text = "SeeD salary" };
            salaryOldText = new SalaryText { GridRow = 1, FontSize = 8 };
            salaryNewText = new SalaryText { GridRow = 1, FontSize = 8 };
            content.Children.Add(salaryTitleText);
            content.Children.Add(salaryOldText);
            content.Children.Add(salaryNewText);
            panel.Children.Add(content);
            root.Children.Add(panel);
            salaryWindowUi = new UiView(root) { DesignWidth = 320, DesignHeight = 224 };
        }
        float progress = salary.Scale9A / 4096f;
        float blend = salary.RankBlend98 / 4096f;
        salaryTitleText!.Opacity = progress;
        salaryOldText!.Text = $"Rank {SalaryGlyphs(salary.OldRankGlyphs)}   {SalaryGlyphs(salary.OldPayGlyphs)} gil";
        salaryOldText.Opacity = progress * (1 - blend);
        salaryNewText!.Text = $"Rank {SalaryGlyphs(salary.NewRankGlyphs)}   {SalaryGlyphs(salary.NewPayGlyphs)} gil";
        salaryNewText.Opacity = progress * blend;
        salaryWindowUi.Resize(bounds.Width, bounds.Height);
        salaryWindowUi.Draw(uiCanvas!, new UiTheme { FontSize = 8 });
    }

    private static string SalaryGlyphs(System.Collections.Immutable.ImmutableArray<byte> glyphs)
    {
        return string.Concat(glyphs.TakeWhile(glyph => glyph != 0).Select(glyph => glyph == 7 ? ' ' : glyph == 178 ? 'A' : glyph is >= 96 and <= 105 ? (char)('0' + glyph - 96) : throw new NotSupportedException("Salary UI encountered an unsupported original formatted glyph."))).Trim();
    }

    private sealed class SalaryText : UiElement
    {
        public string Text { get; set; } = "";
        public float FontSize { get; init; }
        public float Opacity { get; set; }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            byte alpha = (byte)Math.Clamp((int)(Opacity * 255), 0, 255);
            canvas.Text(Text, FontSize, Bounds, theme.Foreground with { A = alpha });
        }
    }
}
