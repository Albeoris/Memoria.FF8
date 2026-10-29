using Microsoft.Xna.Framework;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal enum DialoguePageBoundary
{
    Initial,
    Page,
    Next
}

internal sealed record DialogueRun(string Text, Color Color, bool Blink = false, bool IsButton = false);

internal sealed record DialogueLine(IReadOnlyList<DialogueRun> Runs, int SourceLineIndex, string HangingIndent = "", bool IsQuoteStart = false, bool IsSpeaker = false, bool IsThought = false)
{
    public string Text => ButtonPromptText.PlainText(string.Concat(Runs.Select(run => run.Text)));
}

internal sealed record DialoguePage(IReadOnlyList<DialogueLine> Lines, string? Speaker = null, DialoguePageBoundary Boundary = DialoguePageBoundary.Initial);

internal sealed record DialogueDocument(IReadOnlyList<DialoguePage> Pages)
{
    public LocalizationTextBinding? Binding { get; init; }
    public string Text
    {
        get
        {
            string text = string.Join('\n', Pages.SelectMany(page => page.Lines).Select(line => line.Text));
            return Binding is null ? text : LocalizationTextTrace.Attach(text, Binding with { Text = text });
        }
    }

    public DialogueDocument WithLiteralLine(string text)
    {
        var pages = Pages.ToArray();
        int sourceLine = pages.SelectMany(page => page.Lines).Max(line => line.SourceLineIndex) + 1;
        var line = new DialogueLine([new DialogueRun(text, Color.White)], sourceLine);
        pages[^1] = pages[^1] with { Lines = pages[^1].Lines.Append(line).ToArray() };
        return new DialogueDocument(pages) { Binding = Binding };
    }
}
