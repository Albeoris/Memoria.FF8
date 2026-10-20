using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class ButtonPromptTests
{
    [Fact]
    public void Prompt_raster_has_white_label_graphite_face_and_respects_canvas_clip()
    {
        using var bitmap = new System.Drawing.Bitmap(160, 60);
        using BattlePreviewCanvas canvas = new(bitmap);
        canvas.PushClip(new UiRect(10, 10, 45, 35));
        ButtonPrompt.DrawCanvas(canvas, "N5", 22, new UiRect(10, 10, 80, 32), 1, null);
        canvas.PopClip();
        bool white = false;
        bool graphite = false;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                white |= color.R > 220 && color.G > 220 && color.B > 220;
                graphite |= color.R is >= 35 and <= 65 && color.G is >= 35 and <= 65 && color.B is >= 40 and <= 70;
                if (x >= 55 || y >= 45 || x < 10 || y < 10)
                {
                    Assert.Equal(System.Drawing.Color.Black.ToArgb(), color.ToArgb());
                }
            }
        }
        Assert.True(white);
        Assert.True(graphite);
    }

    [Fact]
    public void Train_legend_separates_remapped_keys_from_code_digits_and_keeps_authored_lines()
    {
        KeyboardInput input = new();
        input.Profile = input.Profile.WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.NumPad5], [])).WithBinding(ControlAction.Menu, ControlDevice.Keyboard, new([Keys.NumPad8], [])).WithBinding(ControlAction.Card, ControlDevice.Keyboard, new([Keys.NumPad6], [])).WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new([Keys.NumPad4], []));
        const string source = "{Key Triangle}4{Line}{Key Square}3 {Key Circle}1{Line}{Key Cross}2";
        DialogueDocument document = DialogueTextFormatter.Parse(source, buttonBindingHint: input.NativeButtonBindingHint);
        var pages = DialogueLayout.Create(document, new DialogueState(0, 1, source, 0, 0), null, 10, value => value.Length * 5);
        var lines = Assert.Single(pages).Lines;
        Assert.Equal(3, lines.Count);
        Assert.Equal(new[] { "N5", "N6", "N8", "N4" }, lines.SelectMany(line => line.Runs).Where(run => run.Run.IsButton).Select(run => run.Run.Text));
        Assert.Equal(new[] { "4", "3", "1", "2" }, lines.SelectMany(line => line.Runs).Where(run => !run.Run.IsButton && !string.IsNullOrWhiteSpace(run.Run.Text)).Select(run => run.Run.Text.Trim()));
        foreach (var line in lines)
        {
            for (int index = 0; index < line.Runs.Count - 1; index++)
            {
                var placed = line.Runs[index];
                if (placed.Run.IsButton)
                {
                    Assert.True(line.Runs[index + 1].X >= placed.X + ButtonPrompt.Advance(placed.Run.Text.Length * 5, 10));
                }
            }
        }
    }

    [Fact]
    public void Long_chord_wraps_as_one_keycap_without_merging_adjacent_prompts()
    {
        DialogueDocument document = DialogueTextFormatter.Parse("before {Key Triangle}{Key Cross} after", buttonBindingHint: buttons => buttons == 0x10 ? "Left Ctrl + N5" : "N4");
        DialogueLine source = document.Pages[0].Lines[0];
        var lines = DialogueLayout.Wrap(source, 85, value => value.Length * 5, false, 10).ToArray();
        var buttons = lines.SelectMany(line => line.Runs).Where(run => run.Run.IsButton).ToArray();
        Assert.Equal(new[] { "Left Ctrl + N5", "N4" }, buttons.Select(run => run.Run.Text));
        Assert.All(lines, line => Assert.True(line.Width <= 85));
    }

    [Fact]
    public void Thought_decoration_preserves_prompt_semantics_and_reveal_shows_whole_key()
    {
        DialogueDocument document = DialogueTextFormatter.Parse("({Color GreenBlink}{Key Triangle}4)", buttonBindingHint: _ => "N5");
        DialogueRun button = Assert.Single(document.Pages[0].Lines[0].Runs, run => run.IsButton);
        Assert.True(button.Blink);
        int remaining = 0;
        Assert.Equal("", ButtonPrompt.VisibleText(button, ref remaining));
        remaining = 1;
        Assert.Equal("N5", ButtonPrompt.VisibleText(button, ref remaining));
        Assert.Equal(0, remaining);
        Assert.Equal("", ButtonPrompt.VisibleText(new DialogueRun("4", Color.White), ref remaining));
        var page = DialogueLayout.Create(document, new DialogueState(0, 1, "", 0, 0), null, 10, value => value.Length)[0];
        DialoguePager pager = new();
        pager.RevealCurrentPage(page);
        Assert.Equal(4, pager.VisibleCharacters);
    }
}
