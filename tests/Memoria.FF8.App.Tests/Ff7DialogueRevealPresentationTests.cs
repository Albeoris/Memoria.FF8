using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.Tests;

public sealed class Ff7DialogueRevealPresentationTests
{
    [Theory]
    [InlineData(FieldTextTokenKind.DecimalVariable, "10")]
    [InlineData(FieldTextTokenKind.HexVariable, "FFFF")]
    [InlineData(FieldTextTokenKind.RightAlignedVariable, "   10")]
    public void NumericExpansionUsesTheSharedDocumentAndPartialRevealWithoutChangingLayout(FieldTextTokenKind kind, string value)
    {
        FieldTextToken[] tokens = [new(FieldTextTokenKind.Glyph, 0, 0, 0, 0, "Cost "), new(kind, 5, 0, 0, 0, ""), new(FieldTextTokenKind.Glyph, 7, 0, 0, 0, " gil")];
        Ff7Dialogue partial = Dialogue(1, expansionOffset: 1);
        Ff7CommonDialogueText before = Ff7CommonDialogueText.Create(partial, tokens, token => token.Kind == kind ? value : token.Text, _ => "");
        Ff7CommonDialogueText after = Ff7CommonDialogueText.Create(partial with { TokenPosition = 3 }, tokens, token => token.Kind == kind ? value : token.Text, _ => "");
        Assert.Equal("Cost " + value + " gil", before.Document.Text);
        Assert.Equal(6, before.VisibleCharacters);
        Assert.Equal(9 + value.Length, after.VisibleCharacters);
        Assert.Equal(Widths(before), Widths(after));
    }

    [Fact]
    public void PartialNameRevealKeepsTheFullNativeDocumentAndLineWidths()
    {
        FieldTextToken[] tokens = [Token(FieldTextTokenKind.Glyph, "W"), Token(FieldTextTokenKind.CharacterName), Token(FieldTextTokenKind.NewLine), Token(FieldTextTokenKind.Glyph, "Tail")];
        Ff7Dialogue partial = Dialogue(1, expansionOffset: 3);
        Ff7CommonDialogueText text = Create(partial, tokens);
        Ff7CommonDialogueText complete = Create(partial with { TokenPosition = tokens.Length }, tokens);
        Assert.Equal("WSquall\nTail", text.Document.Text);
        Assert.Equal(4, text.VisibleCharacters);
        Assert.Equal(11, complete.VisibleCharacters);
        Assert.Equal(complete.Document.Text, text.Document.Text);
        Assert.Equal(Widths(complete), Widths(text));
        Assert.Equal(new[] { 0, 1 }, text.Document.Pages[0].Lines.Select(line => line.SourceLineIndex));
    }

    [Fact]
    public void NativePageBoundaryKeepsFuturePagesOutOfLayout()
    {
        FieldTextToken[] tokens = [Token(FieldTextTokenKind.Glyph, "First"), Token(FieldTextTokenKind.NewPage), Token(FieldTextTokenKind.Glyph, "Second"), Token(FieldTextTokenKind.CharacterName)];
        Ff7CommonDialogueText first = Create(Dialogue(1), tokens);
        Ff7CommonDialogueText second = Create(Dialogue(2, expansionOffset: 2) with { Page = 1 }, tokens);
        Assert.Equal("First", first.Document.Text);
        Assert.Equal(5, first.VisibleCharacters);
        Assert.Equal("SecondSquall", second.Document.Text);
        Assert.Equal(2, second.VisibleCharacters);
        Assert.Single(first.Document.Pages);
        Assert.Single(second.Document.Pages);
    }

    [Fact]
    public void BoxedConfiguredButtonRevealsAtomicallyWithoutChangingLayout()
    {
        FieldTextToken[] tokens = [Token(FieldTextTokenKind.Glyph, "Press "), Token(FieldTextTokenKind.ControllerButton), Token(FieldTextTokenKind.Glyph, " now")];
        Ff7CommonDialogueText before = Create(Dialogue(1, expansionOffset: 20), tokens);
        Ff7CommonDialogueText after = Create(Dialogue(2), tokens);
        Assert.Equal("Press N4 now", before.Document.Text);
        Assert.Equal(before.Document.Text, after.Document.Text);
        Assert.Equal(Widths(before), Widths(after));
        Assert.Equal(6, before.VisibleCharacters);
        Assert.Equal(7, after.VisibleCharacters);
        DialogueRun button = Assert.Single(after.Document.Pages[0].Lines[0].Runs, run => run.IsButton);
        int remaining = 1;
        Assert.Equal("N4", ButtonPrompt.VisibleText(button, ref remaining));
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void AnimatedNativePanelKeepsHalfPixelCenterAndUsesInteriorClip()
    {
        Ff7Window window = new(0, 10, 20, 212, 57);
        Ff7Dialogue dialogue = Dialogue(0) with { Presentation = new(2, 101, 25) };
        var panel = Ff7DialoguePresentationPolicy.AnimatedPanel(new PixelBounds(10, 20, 212, 57), dialogue, window);
        Assert.Equal(65.5f, panel.X);
        Assert.Equal(36f, panel.Y);
        Assert.Equal(101f, panel.Width);
        Assert.Equal(25f, panel.Height);
        Assert.Equal(new Rectangle(66, 36, 100, 25), Ff7DialoguePresentationPolicy.Clip(panel, new Rectangle(0, 0, 320, 240)));
        var doubled = Ff7DialoguePresentationPolicy.NativePanel(dialogue, window, new Rectangle(7, 11, 640, 480));
        Assert.Equal(138f, doubled.X);
        Assert.Equal(83f, doubled.Y);
        Assert.Equal(202f, doubled.Width);
        Assert.Equal(50f, doubled.Height);
    }

    [Theory]
    [InlineData(128, 1, 192)]
    [InlineData(128, 3, 128)]
    [InlineData(128, 5, 64)]
    [InlineData(240, 1, 255)]
    [InlineData(16, 5, 0)]
    public void SharedMessageSpeedUsesNativeBandsAndClamps(int native, int shared, int expected)
    {
        Assert.Equal((byte)expected, Ff7DialoguePresentationPolicy.MessageSpeed((byte)native, shared));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void InvalidSharedMessageSpeedIsRejected(int shared)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ff7DialoguePresentationPolicy.MessageSpeed(128, shared));
    }

    [Theory]
    [InlineData(-2, 1, 0, -1.5f)]
    [InlineData(-14, 1, 0, -10.5f)]
    [InlineData(-16, 1, 1, 0)]
    [InlineData(-18, 2, 1, -1.5f)]
    [InlineData(-32, 2, 2, 0)]
    public void CommonScrollRetiresOnlyCompletedRowsAndPreservesSourceIndices(int offset, int scrollRows, int retiredRows, float translation)
    {
        FieldTextToken[] tokens = [Token(FieldTextTokenKind.Color) with { Argument = 2 }, Token(FieldTextTokenKind.Glyph, "First"), Token(FieldTextTokenKind.NewLine), Token(FieldTextTokenKind.Glyph, "Second"), Token(FieldTextTokenKind.NewLine), Token(FieldTextTokenKind.Glyph, "Third")];
        Ff7Dialogue dialogue = Dialogue(tokens.Length) with { Presentation = new(8, 212, 57) { ScrollOffset = offset, ScrollRows = scrollRows } };
        Ff7CommonDialogueText text = Create(dialogue, tokens);
        Assert.Equal(retiredRows, text.RetiredRows);
        Assert.Equal(new[] { "First", "Second", "Third" }.Skip(retiredRows), text.Document.Pages[0].Lines.Select(line => line.Text));
        Assert.Equal(Enumerable.Range(retiredRows, 3 - retiredRows), text.Document.Pages[0].Lines.Select(line => line.SourceLineIndex));
        Assert.Equal(new[] { 16, 11, 5 }[retiredRows], text.VisibleCharacters);
        Assert.All(text.Document.Pages[0].Lines.SelectMany(line => line.Runs), run => Assert.Equal(Color.Red, run.Color));
        Assert.Equal(translation, Ff7DialoguePresentationPolicy.CommonScrollOffset(dialogue, 12));
    }

    [Fact]
    public void RetiredRowKeepsChoiceSourceIndicesAndPartialReveal()
    {
        FieldTextToken[] tokens = [Token(FieldTextTokenKind.Glyph, "Old"), Token(FieldTextTokenKind.NewLine), Token(FieldTextTokenKind.Glyph, "Answer"), Token(FieldTextTokenKind.NewLine), Token(FieldTextTokenKind.CharacterName)];
        Ff7Dialogue dialogue = Dialogue(4, expansionOffset: 2) with { FirstChoice = 1, LastChoice = 2, Presentation = new(2, 212, 57, ExpansionOffset: 2) { ScrollOffset = -16, ScrollRows = 1 } };
        Ff7CommonDialogueText text = Create(dialogue, tokens);
        IReadOnlyList<DialogueVisualPage> pages = Layout(text, dialogue);
        Assert.Equal("Answer\nSquall", text.Document.Text);
        Assert.Equal(8, text.VisibleCharacters);
        Assert.All(pages.SelectMany(page => page.Lines), line => Assert.True(line.IsChoice));
        Assert.Equal(new[] { 1, 2 }, pages.SelectMany(page => page.Lines).Select(line => line.SourceLineIndex));
    }

    [Fact]
    public void NativeScrollConfirmDoesNotAdvanceIntoAnUnrevealedAdaptivePage()
    {
        FieldTextToken[] tokens = Rows(10);
        Ff7Dialogue dialogue = Dialogue(15) with { AwaitingInput = true, Presentation = new(4, 212, 57) };
        Ff7CommonDialogueText text = Create(dialogue, tokens);
        IReadOnlyList<DialogueVisualPage> pages = Layout(text, dialogue);
        Assert.Equal(2, pages.Count);
        DialoguePager pager = new();
        Assert.Equal(8, text.VisibleCharacters);
        Assert.False(Ff7DialoguePresentationPolicy.AdvanceCommonPage(dialogue, text, pages, pager));
        Assert.Equal(0, pager.PageIndex);
        text = Create(dialogue with { TokenPosition = 17 }, tokens);
        Assert.True(Ff7DialoguePresentationPolicy.AdvanceCommonPage(dialogue, text, pages, pager));
        Assert.Equal(1, pager.PageIndex);
    }

    [Fact]
    public void RowRetirementReflowsAdaptivePagerToItsSourceRow()
    {
        FieldTextToken[] tokens = Rows(18);
        Ff7Dialogue dialogue = Dialogue(tokens.Length);
        IReadOnlyList<DialogueVisualPage> before = Layout(Create(dialogue, tokens), dialogue);
        DialoguePager pager = new();
        Assert.True(pager.Advance(before.Count));
        Assert.Equal(8, before[pager.PageIndex].Lines[0].SourceLineIndex);
        dialogue = dialogue with { Presentation = new(2, 212, 57) { ScrollOffset = -16, ScrollRows = 1 } };
        IReadOnlyList<DialogueVisualPage> after = Layout(Create(dialogue, tokens), dialogue);
        DialoguePager reflowed = Ff7DialoguePresentationPolicy.ReflowCommonPager(pager, before, after, true);
        Assert.Equal(0, reflowed.PageIndex);
        Assert.Contains(after[reflowed.PageIndex].Lines, line => line.SourceLineIndex == 8);
    }

    private static FieldTextToken[] Rows(int count) => Enumerable.Range(0, count).SelectMany(row => row == count - 1 ? new[] { Token(FieldTextTokenKind.Glyph, "X") } : new[] { Token(FieldTextTokenKind.Glyph, "X"), Token(FieldTextTokenKind.NewLine) }).ToArray();

    private static IReadOnlyList<DialogueVisualPage> Layout(Ff7CommonDialogueText text, Ff7Dialogue dialogue) => DialogueLayout.Create(text.Document, new(dialogue.Window, dialogue.Text, text.Document.Text, 0, 0, dialogue.FirstChoice, dialogue.LastChoice, dialogue.Selected), new(dialogue.Window, 0, 0, 212, 57), 12, value => value.Length * 6);

    private static FieldTextToken Token(FieldTextTokenKind kind, string text = "") => new(kind, 0, 0, 0, 0, text);

    private static Ff7Dialogue Dialogue(int tokenPosition, int expansionOffset = 0) => new(1, 0, 0, null, null, 0, TokenPosition: tokenPosition, Presentation: new(2, 212, 57, ExpansionOffset: expansionOffset));

    private static Ff7CommonDialogueText Create(Ff7Dialogue dialogue, FieldTextToken[] tokens) => Ff7CommonDialogueText.Create(dialogue, tokens, token => token.Kind == FieldTextTokenKind.CharacterName ? "Squall" : token.Text, _ => ButtonPromptText.Mark("N4"));

    private static float[] Widths(Ff7CommonDialogueText text) => text.Document.Pages[0].Lines.Select(line => line.Runs.Sum(run => DialogueLayout.MeasureRun(run, value => value.Length * 6, 12))).ToArray();
}
