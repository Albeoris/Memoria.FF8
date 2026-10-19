using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveBankLayoutTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BankPlaquesAreDistinctClickableAndDrawTheTitleCrestAndRomanNumerals(int selectedBank)
    {
        UiGrid banks = PlayerSaveView.BuildBanks(selectedBank);
        UiView view = new(banks);
        view.Resize(922, 72);
        UiButton[] buttons = banks.Children.OfType<UiButton>().ToArray();
        Assert.Equal(new[] { "save-bank-0", "save-bank-1", "save-bank-2" }, buttons.Select(button => button.Id));
        for (int index = 0; index < buttons.Length; index++)
        {
            UiRect bounds = buttons[index].Bounds;
            Assert.True(bounds.Width > 250 && bounds.Height == 72);
            Assert.Same(buttons[index], banks.HitTest(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
            if (index > 0)
            {
                Assert.True(bounds.X > buttons[index - 1].Bounds.Right);
            }
        }
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Single(canvas.Images, image => image.Kind == UiImageKind.SilverCrest);
        Assert.Contains("I", canvas.Texts);
        Assert.Contains("II", canvas.Texts);
        Assert.Contains("Memoria", canvas.Texts);
        Assert.Equal(2, canvas.Texts.Count(text => text == "Original"));
        Assert.Contains(selectedBank == 0 ? "ACTIVE  ·  100 slots" : "100 slots", canvas.Texts);
        Assert.Equal(2, canvas.Texts.Count(text => text.EndsWith("30 slots", StringComparison.Ordinal)));
        Assert.Single(canvas.Texts, text => text.StartsWith("ACTIVE", StringComparison.Ordinal));
    }

    [Fact]
    public void TitleCrestArtworkIsAvailableWithoutAnInstalledGame()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                MenuArtworkProvider artwork = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
                var image = artwork.Read(new UiImageSource(UiImageKind.SilverCrest, 0));
                Assert.Equal(256, image.Height);
                Assert.InRange(image.Width, 100, 256);
                Assert.True(image.Pixels.Count(pixel => pixel.A > 0) > image.Width * image.Height / 8);
                Assert.Contains(image.Pixels, pixel => pixel.A == 0);
                Assert.True(image.Pixels.Count(pixel => pixel.R > 180 && pixel.A > 128) > 100);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        Assert.Null(failure);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<string> Texts { get; } = [];
        public List<UiImageSource> Images { get; } = [];

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Texts.Add(text);
            }
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment alignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add(source);
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
