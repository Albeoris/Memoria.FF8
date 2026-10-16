using Memoria.FF8.Events;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed class DialoguePager
{
    public int PageIndex { get; private set; }
    private double elapsed;
    private readonly UiTextReveal reveal = new();

    public int VisibleCharacters => reveal.VisibleCharacters;

    public void UpdateReveal(TimeSpan delta, DialogueVisualPage page, int speed, bool instant = false)
    {
        reveal.Update(delta, CharacterCount(page), CharactersPerSecond(speed), instant);
    }

    public bool RevealCurrentPage(DialogueVisualPage page)
    {
        return reveal.RevealAll(CharacterCount(page));
    }

    internal static int CharactersPerSecond(int speed) => speed switch
    {
        1 => 15,
        2 => 30,
        3 => 45,
        4 => 60,
        5 => 90,
        _ => throw new ArgumentOutOfRangeException(nameof(speed))
    };

    internal static string VisiblePrefix(string text, ref int remaining)
    {
        return UiTextReveal.VisiblePrefix(text, ref remaining);
    }

    private static int CharacterCount(DialogueVisualPage page) => page.Lines.Sum(line => line.Runs.Sum(run => UiTextReveal.CharacterCount(run.Run.Text)));

    public bool Advance(int pageCount)
    {
        if (PageIndex + 1 >= pageCount)
        {
            return false;
        }
        PageIndex++;
        elapsed = 0;
        reveal.Reset();
        return true;
    }

    public void UpdatePopup(TimeSpan delta, IReadOnlyList<DialogueVisualPage> pages, int charactersPerSecond = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(charactersPerSecond, 1);
        if (pages.Count < 2)
        {
            return;
        }
        elapsed += delta.TotalSeconds;
        int characters = CharacterCount(pages[PageIndex]);
        if (elapsed >= Math.Max(3, 1 + characters / (double)charactersPerSecond))
        {
            PageIndex = (PageIndex + 1) % pages.Count;
            elapsed = 0;
            reveal.Reset();
        }
    }

    public static bool AdvanceConfirmable(IEnumerable<(DialogueState Dialogue, DialoguePager Pager, int Count)> channels)
    {
        bool advanced = false;
        foreach (var channel in channels)
        {
            if (channel.Dialogue.CanConfirm)
            {
                advanced |= channel.Pager.Advance(channel.Count);
            }
        }
        return advanced;
    }
}
