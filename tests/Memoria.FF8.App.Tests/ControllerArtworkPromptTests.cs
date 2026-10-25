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
    public void ControllerMetadataDoesNotChangePromptMeasurementOrKeyboardBindings()
    {
        string prompt = new InputChord([], [Buttons.A]).FormatPrompt(ControllerPresentation.Xbox);
        string label = ButtonPromptText.Mark("[A]");
        Assert.Equal(ButtonPromptText.Measure(label, 20, 500, "Arial"), ButtonPromptText.Measure(prompt, 20, 500, "Arial"));
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
    public void ControllerPromptsKeepTheirProceduralFallbackWithoutAnActiveResourceSource()
    {
        using System.Drawing.Bitmap bitmap = new(200, 70);
        using GameUi.BattlePreviewCanvas canvas = new(bitmap, gameDirectory: "");
        string prompt = new InputChord([], [Buttons.A]).FormatPrompt(ControllerPresentation.Xbox);
        canvas.Text(prompt, 20, new UiRect(0, 0, 200, 70), new UiColor(255, 255, 255));
    }
}
