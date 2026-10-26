using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed record FieldVoiceDialogue(DialogueState Dialogue, int AuthoredPage, int? SelectedChoice = null);

/// <summary>Tracks transient per-window voice playback independently of VM sound channels and visual wrapping.</summary>
internal sealed class FieldVoicePresentation(Func<string, int, int, int, int?, ExternalVoiceSelection?> resolve, Func<int, ResourceKey, double, bool> play, Action<int> stop, Func<int, bool> playing)
{
    private readonly Dictionary<int, Window> windows = [];
    private object? owner;
    private string? field;

    public void Synchronize(string? nextField, object? nextOwner, IReadOnlyList<FieldVoiceDialogue> dialogues)
    {
        if (!ReferenceEquals(owner, nextOwner) || field != nextField)
        {
            Clear();
            owner = nextOwner;
            field = nextField;
        }
        HashSet<int> retained = dialogues.Select(item => item.Dialogue.Channel).ToHashSet();
        foreach (int channel in windows.Keys.Where(channel => !retained.Contains(channel)).ToArray())
        {
            stop(channel);
            windows.Remove(channel);
        }
        if (field is null)
        {
            return;
        }
        foreach (FieldVoiceDialogue item in dialogues)
        {
            DialogueState dialogue = item.Dialogue;
            object identity = (object?)dialogue.NativeWindow ?? dialogue;
            if (!windows.TryGetValue(dialogue.Channel, out Window? window) || !ReferenceEquals(window.Identity, identity))
            {
                stop(dialogue.Channel);
                window = new Window(identity, dialogues.Count(other => other.Dialogue.FirstChoice is null && !other.Dialogue.IsNamePrompt) <= 1);
                windows[dialogue.Channel] = window;
            }
            if (dialogue.IsNamePrompt || dialogue.NativeWindow is { Progress: 0, Delta: >= 0 })
            {
                continue;
            }
            if (dialogue.NativeWindow is { Delta: < 0 })
            {
                stop(dialogue.Channel);
                window.AutoPending = false;
                window.Started = false;
                continue;
            }
            if (window.Page != item.AuthoredPage)
            {
                window.Page = item.AuthoredPage;
                window.Choice = null;
                Start(window, dialogue, item.AuthoredPage, null);
            }
            else if (item.SelectedChoice is int choice && window.Choice != choice)
            {
                window.Choice = choice;
                Start(window, dialogue, item.AuthoredPage, choice);
            }
            if (window.Started && !playing(dialogue.Channel))
            {
                window.Started = false;
                window.AutoPending = window.AutoAllowed && dialogue.CanConfirm && dialogue.FirstChoice is null && item.SelectedChoice is null && dialogues.Count == 1;
            }
        }
    }

    private void Start(Window window, DialogueState dialogue, int page, int? choice)
    {
        window.AutoPending = false;
        window.Started = false;
        ExternalVoiceSelection? selection = resolve(field!, dialogue.Channel, dialogue.MessageId, page, choice);
        // A missing next-page/choice clip leaves an earlier clip audible, as in the source voice layer.
        window.Started = selection is not null && play(dialogue.Channel, selection.Key, selection.Gain);
    }

    public bool CanAutoAdvance(int channel)
    {
        if (!windows.TryGetValue(channel, out Window? window) || !window.AutoPending)
        {
            return false;
        }
        return true;
    }

    public void Clear()
    {
        foreach (int channel in windows.Keys)
        {
            stop(channel);
        }
        windows.Clear();
        owner = null;
        field = null;
    }

    private sealed class Window(object identity, bool autoAllowed)
    {
        public object Identity { get; } = identity;
        public bool AutoAllowed { get; } = autoAllowed;
        public int Page { get; set; } = -1;
        public int? Choice { get; set; }
        public bool Started { get; set; }
        public bool AutoPending { get; set; }
    }
}
