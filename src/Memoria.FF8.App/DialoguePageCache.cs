using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed class DialoguePageCache
{
    private sealed record Entry(DialogueState Dialogue, DialogueWindow? Window, string Family, float Size, string ExpandedKey, bool ValueIdentity, IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager);

    private readonly Dictionary<int, Entry> entries = [];

    public (IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager) Get(DialogueState dialogue, DialogueWindow? window, string family, float size, string expandedKey, bool valueIdentity, Func<IReadOnlyList<DialogueVisualPage>> create)
    {
        if (!entries.TryGetValue(dialogue.Channel, out Entry? entry) || entry.ValueIdentity != valueIdentity || !(valueIdentity ? entry.Dialogue == dialogue : ReferenceEquals(entry.Dialogue, dialogue)) || entry.Window != window || entry.Family != family || entry.Size != size || entry.ExpandedKey != expandedKey)
        {
            entry = new Entry(dialogue, window, family, size, expandedKey, valueIdentity, create(), new DialoguePager());
            entries[dialogue.Channel] = entry;
        }
        return (entry.Pages, entry.Pager);
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
            entries.Remove(dialogue.Channel);
            return;
        }
        foreach (int channel in entries.Where(pair => pair.Value.Dialogue is { CanConfirm: true, FirstChoice: null, IsNamePrompt: false }).Select(pair => pair.Key).ToArray())
        {
            entries.Remove(channel);
        }
    }

    public void Clear()
    {
        entries.Clear();
    }
}
