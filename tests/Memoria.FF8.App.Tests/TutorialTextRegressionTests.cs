using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TutorialTextRegressionTests
{
    [Fact]
    public void Installed_information_keeps_color_boundary_space_and_does_not_indent_embedded_quotes()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialExamCatalog catalog = assets.ReadNativeTutorialExamCatalog();
        NativeTutorialInformationNode node = Assert.Single(catalog.Information, node => node.Title == "Basic Terms/Junction");
        NativeTutorialInformationPage page = catalog.InformationPage(node.Id, new HashSet<int>(), new(0, 7, 8));
        DialogueDocument parsed = DialogueTextFormatter.Parse(page.Text);
        DialogueLine first = parsed.Pages[0].Lines[0];
        Assert.True(first.Runs.Count >= 2);
        Assert.Equal(Microsoft.Xna.Framework.Color.Lime, first.Runs[0].Color);
        Assert.Equal(Microsoft.Xna.Framework.Color.White, first.Runs[1].Color);
        Assert.StartsWith(" ", first.Runs[1].Text);
        Assert.All(parsed.Pages.SelectMany(page => page.Lines), line =>
        {
            Assert.False(line.IsSpeaker);
            Assert.False(line.IsQuoteStart);
            Assert.Empty(line.HangingIndent);
        });
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TUTORIAL_RENDER_DIR");
        if (output is null)
        {
            return;
        }
        Directory.CreateDirectory(output);
        NativeTutorialDocumentElement document = new(page.Text, 48, "Arial");
        document.Arrange(new UiRect(0, 0, 1400, 680));
        using Bitmap bitmap = new(1400, 680);
        using (SkiaSharp.SKCanvas graphics = new(bitmap))
        {
            graphics.Clear(Color.FromArgb(88, 88, 88));
        }
        using (BattlePreviewCanvas canvas = new(bitmap, assets.GameDirectory))
        {
            document.Draw(canvas, new UiTheme());
        }
        bitmap.SavePng(Path.Combine(output, "information-color-and-quotes.png"));
    }
}
