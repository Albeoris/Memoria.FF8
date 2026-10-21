using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PartySelectionTextScaleTests
{
    private static NativePartyMenuState Menu => new(0, string.Empty, string.Empty, 0, 0, 0, 0, 0, NativePartyMenuPhase.Active, 2, [0, 1, 5], 0xFF, 1, 1, false, NativeFieldMenuKind.PartySelection, 2, 0, null, null, null, [2, 255, 3, 4, 255, 6, 7, 255], 2);

    [Theory]
    [InlineData(0.25f)]
    [InlineData(1)]
    [InlineData(1.5f)]
    [InlineData(3)]
    public void TextScalesWithinStablePartyCellsWithoutOverlappingAdjacentLabels(float scale)
    {
        NativePartyMenuState menu = Menu with { LockedMask = 9 };
        string before = JsonSerializer.Serialize(menu);
        MenuData data = new();
        for (int row = 0; row < 8; row++)
        {
            data.Characters.Add(new MenuCharacter { Id = $"character-{row}", NativeId = row, Name = $"Member {row} with a particularly long display name", Level = 100, CurrentHp = 9999, MaximumHp = 9999 });
        }
        int candidate = PartySelectionView.Navigate(menu, 0, 1, 0);
        UiView view = PartySelectionView.Build(menu, data, candidate, "Cancel binding", scale);
        view.Resize(960, 672);
        RecordingCanvas canvas = new();

        view.Draw(canvas, new UiTheme());

        Assert.All(canvas.Texts, text =>
        {
            Assert.Equal(UiTextFlow.SingleLine, text.Flow);
            Assert.True(text.Bounds.X >= 0 && text.Bounds.Y >= 0 && text.Bounds.Right <= 960 && text.Bounds.Bottom <= 672);
        });
        Assert.Contains(canvas.Texts, text => text.Text == data.Characters[0].Name && text.Size == 28 * scale);
        Assert.Contains(canvas.Texts, text => text.Text == data.Characters[2].Name && text.Size == 18 * scale);
        Assert.Contains(canvas.Texts, text => text.Text == "Cancel binding: clear selection" && text.Size == 19 * scale);
        for (int first = 0; first < canvas.Texts.Count; first++)
        {
            for (int second = first + 1; second < canvas.Texts.Count; second++)
            {
                Assert.True(canvas.Texts[first].Bounds.Intersect(canvas.Texts[second].Bounds).IsEmpty, $"Text viewports overlap: {canvas.Texts[first].Text} / {canvas.Texts[second].Text}");
            }
        }
        Assert.True(Assert.Single(canvas.Captions).Bounds.Bottom < canvas.Texts[0].Bounds.Y);
        Assert.Equal(new[] { 0, 1, 5, 2, 3, 4, 6, 7 }, canvas.Images.Select(image => image.Source.Id));
        Assert.Equal(new UiRect(62, 122, 88, 128), canvas.Images[0].Bounds);
        Assert.Equal(new UiRect(564, 124, 64, 96), canvas.Images[3].Bounds);
        Assert.Equal(2, canvas.Pointers.Count);
        Assert.Equal(2, NativePartyMenuProjection.Inputs(menu)[candidate].Value);
        Assert.Equal(0, PartySelectionView.Navigate(menu, candidate, -1, 0));
        Assert.False(view.Key(UiKey.Right));
        view.Pointer(600, 170, true);
        view.Pointer(600, 170, false);
        Assert.Equal(before, JsonSerializer.Serialize(menu));
    }

    [Fact]
    public void ExistingCancelHintArgumentKeepsDefaultTypography()
    {
        UiView view = PartySelectionView.Build(Menu, new MenuData(), 0, "Custom cancel");
        view.Resize(960, 672);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Text == "Custom cancel: clear selection" && text.Size == 19);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Text, float Size, UiRect Bounds, UiTextFlow Flow)> Texts { get; } = [];
        public List<(string Text, UiRect Bounds)> Captions { get; } = [];
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];
        public List<UiRect> Pointers { get; } = [];

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Texts.Add((text, fontSize, bounds, flow));
            }
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Text(text, fontSize, bounds, color, UiAlignment.Start, UiTextFlow.Auto);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color, verticalAlignment, UiTextFlow.Auto);
        }

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Captions.Add((text, bounds));
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add((source, bounds));
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers.Add(bounds);
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
