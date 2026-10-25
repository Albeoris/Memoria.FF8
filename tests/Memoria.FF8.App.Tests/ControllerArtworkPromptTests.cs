using Microsoft.Xna.Framework.Input;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ControllerArtworkPromptTests
{
    [Theory]
    [InlineData(Buttons.A, 134)]
    [InlineData(Buttons.B, 133)]
    [InlineData(Buttons.X, 135)]
    [InlineData(Buttons.Y, 132)]
    [InlineData(Buttons.LeftTrigger, 128)]
    [InlineData(Buttons.RightTrigger, 129)]
    [InlineData(Buttons.LeftShoulder, 130)]
    [InlineData(Buttons.RightShoulder, 131)]
    [InlineData(Buttons.Back, 136)]
    [InlineData(Buttons.Start, 139)]
    public void PhysicalControllerGlyphsFollowResolvedBindingsAcrossPresentationNames(Buttons button, int expected)
    {
        InputChord chord = new([], [button]);
        foreach (ControllerPresentation presentation in new[] { ControllerPresentation.Xbox, ControllerPresentation.PlayStation, ControllerPresentation.Nintendo })
        {
            string prompt = chord.FormatPrompt(presentation);
            DialogueRun run = Assert.Single(ButtonPromptText.Runs(prompt));
            Assert.True(run.IsButton);
            Assert.Equal(expected, ButtonPrompt.ControllerIcon(run.Text));
            Assert.Equal(chord.FormatDisplayName(presentation), ButtonPromptText.PlainText(prompt));
            Assert.Equal(chord.FormatDisplayName(presentation), ButtonPrompt.Label(run.Text));
        }
    }

    [Fact]
    public void VectorControllerPromptsUseSquareAdvanceAndKeepKeyboardBindings()
    {
        string prompt = new InputChord([], [Buttons.A]).FormatPrompt(ControllerPresentation.Xbox);
        string label = ButtonPromptText.Mark("[A]");
        Assert.Equal(SystemTextLayout.LineHeight(20, "Arial") * 1.24f, ButtonPromptText.Measure(prompt, 20, 500, "Arial").Width, precision: 3);
        Assert.True(ButtonPromptText.Measure(label, 20, 500, "Arial").Width >= ButtonPromptText.Measure(prompt, 20, 500, "Arial").Width);
        DialogueRun keyboard = Assert.Single(ButtonPromptText.Runs(new InputChord([Keys.K], []).FormatPrompt(ControllerPresentation.Xbox)));
        Assert.Null(ButtonPrompt.ControllerIcon(keyboard.Text));
        Assert.Equal("K", keyboard.Text);
    }

    [Fact]
    public void NativeDialogueUsesTheRemappedPhysicalButtonAndKeepsPlainDiagnostics()
    {
        KeyboardInput input = new() { ControllerPresentation = ControllerPresentation.Xbox };
        input.Read(default, new GamePadState(Microsoft.Xna.Framework.Vector2.Zero, Microsoft.Xna.Framework.Vector2.Zero, 0, 0, Array.Empty<Buttons>()), true);
        input.Read(default, new GamePadState(Microsoft.Xna.Framework.Vector2.Zero, Microsoft.Xna.Framework.Vector2.Zero, 0, 0, Buttons.A), true);
        input.Profile = input.Profile.WithBinding(ControlAction.Confirm, ControlDevice.Controller, new([], [Buttons.X]), replaceConflicts: true);
        DialogueDocument document = DialogueTextFormatter.Parse("{Key Confirm}", buttonBindingHint: input.NativeButtonBindingPrompt);
        DialogueRun run = Assert.Single(Assert.Single(Assert.Single(document.Pages).Lines).Runs);
        Assert.Equal(135, ButtonPrompt.ControllerIcon(run.Text));
        Assert.Equal(input.BindingHint(ControlAction.Confirm), document.Text);
    }

    [Fact]
    public void ControllerPromptsUseOriginalVectorsWithoutAnActiveResourceSource()
    {
        using System.Drawing.Bitmap bitmap = new(200, 70);
        using GameUi.BattlePreviewCanvas canvas = new(bitmap, gameDirectory: "");
        string prompt = new InputChord([], [Buttons.A]).FormatPrompt(ControllerPresentation.Xbox);
        canvas.Text(prompt, 20, new UiRect(0, 0, 200, 70), new UiColor(255, 255, 255));
    }
    [Theory]
    [InlineData(ControllerPresentation.PlayStation)]
    [InlineData(ControllerPresentation.Xbox)]
    public void EveryBindableButtonHasScalableOriginalArtworkAndPreservesItsPresentation(ControllerPresentation presentation)
    {
        foreach (Buttons button in ControlsProfile.BindableButtons)
        {
            InputChord chord = new([], [button]);
            DialogueRun run = Assert.Single(ButtonPromptText.Runs(chord.FormatPrompt(presentation)));
            Assert.Equal(presentation, ButtonPrompt.Presentation(run.Text));
            int icon = Assert.IsType<int>(ButtonPrompt.ControllerIcon(run.Text));
            Assert.True(ControllerVectorArtwork.Supports(icon, presentation));
            using System.Drawing.Bitmap small = ControllerVectorArtwork.Render(icon, presentation, 24);
            using System.Drawing.Bitmap large = ControllerVectorArtwork.Render(icon, presentation, 192);
            Assert.Equal(24, small.Width);
            Assert.Equal(192, large.Width);
            Assert.Equal(0, large.GetPixel(0, 0).A);
            Assert.True(large.GetPixel(96, 96).A > 0);
            Assert.Equal(chord.FormatDisplayName(presentation), ButtonPromptText.PlainText(chord.FormatPrompt(presentation)));
        }
        string mixed = new InputChord([Keys.LeftControl], [Buttons.LeftShoulder, Buttons.A]).FormatPrompt(presentation);
        Assert.Equal(3, ButtonPromptText.Runs(mixed).Count(run => run.IsButton));
        Assert.DoesNotContain('\uF180', ButtonPromptText.PlainText(mixed));
    }

    [Theory]
    [InlineData(ControllerPresentation.PlayStation)]
    [InlineData(ControllerPresentation.Xbox)]
    public void VectorButtonSpacingDoesNotDependOnDiagnosticLabelLength(ControllerPresentation presentation)
    {
        string face = Assert.Single(ButtonPromptText.Runs(new InputChord([], [Buttons.A]).FormatPrompt(presentation))).Text;
        string back = Assert.Single(ButtonPromptText.Runs(new InputChord([], [Buttons.Back]).FormatPrompt(presentation))).Text;
        Assert.Equal(37.2f, ButtonPrompt.Measure(face, text => text.Length * 12, 30), precision: 3);
        Assert.Equal(ButtonPrompt.Measure(face, text => text.Length * 12, 30), ButtonPrompt.Measure(back, text => text.Length * 12, 30));
        string roundTrip = DialogueTextFormatter.FormatWithPrompts("{Key Confirm}", buttonBindingHint: _ => ButtonPromptText.Mark(face));
        Assert.Equal(presentation, ButtonPrompt.Presentation(Assert.Single(ButtonPromptText.Runs(roundTrip), run => run.IsButton).Text));
    }

    [Theory]
    [InlineData(ControllerPresentation.PlayStation, Buttons.Y, 84, 217, 161)]
    [InlineData(ControllerPresentation.PlayStation, Buttons.B, 255, 108, 120)]
    [InlineData(ControllerPresentation.PlayStation, Buttons.A, 112, 166, 255)]
    [InlineData(ControllerPresentation.PlayStation, Buttons.X, 243, 149, 204)]
    [InlineData(ControllerPresentation.Xbox, Buttons.A, 108, 212, 134)]
    [InlineData(ControllerPresentation.Xbox, Buttons.B, 255, 115, 119)]
    [InlineData(ControllerPresentation.Xbox, Buttons.X, 112, 176, 255)]
    [InlineData(ControllerPresentation.Xbox, Buttons.Y, 255, 207, 84)]
    public void FaceButtonsKeepTheirTraditionalColors(ControllerPresentation presentation, Buttons button, int red, int green, int blue)
    {
        string label = Assert.Single(ButtonPromptText.Runs(new InputChord([], [button]).FormatPrompt(presentation))).Text;
        using System.Drawing.Bitmap bitmap = ControllerVectorArtwork.Render(ButtonPrompt.ControllerIcon(label)!.Value, presentation, 192);
        Assert.Contains(Enumerable.Range(0, bitmap.Width).SelectMany(x => Enumerable.Range(0, bitmap.Height).Select(y => bitmap.GetPixel(x, y))), color =>
            color.R == red && color.G == green && color.B == blue && color.A == 255);
    }

    [Fact]
    public void ExportOriginalControllerAtlasPreviewsWhenRequested()
    {
        string? directory = Environment.GetEnvironmentVariable("FF8_CONTROLLER_PREVIEW_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        foreach (ControllerPresentation presentation in new[] { ControllerPresentation.PlayStation, ControllerPresentation.Xbox })
        {
            using System.Drawing.Bitmap bitmap = new(960, 640);
            using GameUi.BattlePreviewCanvas canvas = new(bitmap, gameDirectory: "");
            canvas.Fill(new UiRect(0, 0, 960, 640), new UiColor(17, 22, 30));
            canvas.Text($"{presentation} - original vector controller prompts", 27, new UiRect(32, 18, 900, 48), new UiColor(235, 240, 250));
            for (int index = 0; index < ControlsProfile.BindableButtons.Count; index++)
            {
                Buttons button = ControlsProfile.BindableButtons[index];
                string prompt = new InputChord([], [button]).FormatPrompt(presentation);
                float x = 32 + index % 4 * 232;
                float y = 88 + index / 4 * 110;
                DialogueRun run = Assert.Single(ButtonPromptText.Runs(prompt));
                Assert.True(canvas.TryDrawControllerButton(ButtonPrompt.ControllerIcon(run.Text)!.Value, presentation, new UiRect(x, y, 64, 64), 1));
                canvas.Text(InputChord.ButtonName(button, presentation), 17, new UiRect(x, y + 68, 220, 32), new UiColor(184, 196, 214));
            }
            string mixed = new InputChord([Keys.LeftControl], [Buttons.LeftShoulder, Buttons.A]).FormatPrompt(presentation);
            canvas.Text("Mixed binding: " + mixed, 25, new UiRect(32, 548, 896, 60), new UiColor(235, 240, 250));
            bitmap.Save(Path.Combine(directory, presentation == ControllerPresentation.PlayStation ? "sony-controller-atlas.png" : "xbox-controller-atlas.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
