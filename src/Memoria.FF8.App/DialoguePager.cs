using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed class DialoguePager
{
    public int PageIndex { get; private set; }
    private double elapsed;

    public bool Advance(int pageCount)
    {
        if (PageIndex + 1 >= pageCount)
        {
            return false;
        }
        PageIndex++;
        elapsed = 0;
        return true;
    }

    public void UpdatePopup(TimeSpan delta, IReadOnlyList<DialogueVisualPage> pages)
    {
        if (pages.Count < 2)
        {
            return;
        }
        elapsed += delta.TotalSeconds;
        int characters = pages[PageIndex].Lines.Sum(line => line.Runs.Sum(run => run.Run.Text.Length));
        if (elapsed >= Math.Max(3, 1 + characters / 20d))
        {
            PageIndex = (PageIndex + 1) % pages.Count;
            elapsed = 0;
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
