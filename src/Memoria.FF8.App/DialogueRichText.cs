using Microsoft.Xna.Framework;

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
    public string Text => string.Concat(Runs.Select(run => run.Text));
}

internal sealed record DialoguePage(IReadOnlyList<DialogueLine> Lines, string? Speaker = null, DialoguePageBoundary Boundary = DialoguePageBoundary.Initial);

internal sealed record DialogueDocument(IReadOnlyList<DialoguePage> Pages)
{
    public string Text => string.Join('\n', Pages.SelectMany(page => page.Lines).Select(line => line.Text));

    public DialogueDocument WithLiteralLine(string text)
    {
        var pages = Pages.ToArray();
        int sourceLine = pages.SelectMany(page => page.Lines).Max(line => line.SourceLineIndex) + 1;
        var line = new DialogueLine([new DialogueRun(text, Color.White)], sourceLine);
        pages[^1] = pages[^1] with { Lines = pages[^1].Lines.Append(line).ToArray() };
        return new DialogueDocument(pages);
    }
}
