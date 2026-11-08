using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed class DialoguePageCache
{
    private sealed record Entry(DialogueState Dialogue, DialogueWindow? Window, string Family, float Size, string ExpandedKey, bool ValueIdentity, IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager);

    private readonly Dictionary<int, Entry> entries = [];

    public (IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager) Get(DialogueState dialogue, DialogueWindow? window, string family, float size, string expandedKey, bool valueIdentity, Func<IReadOnlyList<DialogueVisualPage>> create, bool retainPosition = false)
    {
        if (!entries.TryGetValue(dialogue.Channel, out Entry? entry) || entry.ValueIdentity != valueIdentity || !SameOpening(entry.Dialogue, dialogue, valueIdentity) || entry.Window != window || entry.Family != family || entry.Size != size || entry.ExpandedKey != expandedKey)
        {
            DialoguePager? previous = retainPosition && entry is not null && SameOpening(entry.Dialogue, dialogue, valueIdentity) ? entry.Pager : null;
            entry = new Entry(dialogue, window, family, size, expandedKey, valueIdentity, create(), new DialoguePager());
            if (previous is not null)
            {
                entry.Pager.ReflowFrom(previous, entry.Pages);
            }
            entries[dialogue.Channel] = entry;
        }
        return (entry.Pages, entry.Pager);
    }

    private static bool SameOpening(DialogueState previous, DialogueState current, bool valueIdentity)
    {
        if (valueIdentity)
        {
            return previous == current;
        }
        // Confirmation and MESSYNC replace the dialogue record but retain the physical window.
        return previous.NativeWindow is not null ? ReferenceEquals(previous.NativeWindow, current.NativeWindow) : ReferenceEquals(previous, current);
    }

    public void RetainChannels(IEnumerable<int> channels)
    {
        HashSet<int> retained = channels.ToHashSet();
        foreach (int channel in entries.Keys.Where(channel => !retained.Contains(channel)).ToArray())
        {
            entries.Remove(channel);
        }
    }

    public void Complete(DialogueState dialogue)
    {
        if (dialogue.FirstChoice is not null || dialogue.IsNamePrompt)
        {
            if (dialogue.NativeWindow is null)
            {
                entries.Remove(dialogue.Channel);
            }
            return;
        }
        // Native windows remain visible during closing; RetainChannels removes them once hidden.
        foreach (int channel in entries.Where(pair => pair.Value.Dialogue is { CanConfirm: true, FirstChoice: null, IsNamePrompt: false, NativeWindow: null }).Select(pair => pair.Key).ToArray())
        {
            entries.Remove(channel);
        }
    }

    internal DialoguePager? ReadPager(int channel) => entries.GetValueOrDefault(channel)?.Pager;

    public void Clear()
    {
        entries.Clear();
    }
}
