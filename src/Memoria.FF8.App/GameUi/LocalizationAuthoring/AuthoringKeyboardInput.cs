using System.Text.Json.Serialization;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.GameUi;

public sealed record AuthoringKeyChord([property: JsonConverter(typeof(JsonStringEnumConverter<Keys>))] Keys Key, bool? Control = false, bool? Shift = false, bool? Alt = false)
{
    public bool Matches(KeyboardState keys)
    {
        bool control = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
        return keys.IsKeyDown(Key) && (Control is null || Control == control) && (Shift is null || Shift == shift) && (Alt is null || Alt == alt);
    }
    public override string ToString() => (Control == true ? "Ctrl+" : "") + (Shift == true ? "Shift+" : "") + (Alt == true ? "Alt+" : "") + Key;
}

/// <summary>Serializable workspace shortcuts; no gameplay bindings are changed.</summary>
public sealed record AuthoringShortcutSettings
{
    public IReadOnlyDictionary<UiEditCommand, AuthoringKeyChord> Bindings { get; init; } = Defaults();
    public int RepeatDelayMilliseconds { get; init; } = 400;
    public int RepeatIntervalMilliseconds { get; init; } = 40;
    public void Validate()
    {
        if (RepeatDelayMilliseconds is < 100 or > 2000 || RepeatIntervalMilliseconds is < 15 or > 500)
        {
            throw new InvalidDataException("Authoring keyboard repeat timing is outside the supported range.");
        }
        if (Bindings is null || Bindings.Any(pair => pair.Value is null))
        {
            throw new InvalidDataException("Authoring keyboard bindings cannot be absent.");
        }
        if (Bindings.Any(pair => !Enum.IsDefined(pair.Key) || !Enum.IsDefined(pair.Value.Key) || pair.Value.Key is Keys.None or Keys.LeftControl or Keys.RightControl or Keys.LeftShift or Keys.RightShift or Keys.LeftAlt or Keys.RightAlt))
        {
            throw new InvalidDataException("Authoring shortcuts require a valid command and a non-modifier key.");
        }
        AuthoringKeyChord[] chords = Bindings.Values.ToArray();
        for (int first = 0; first < chords.Length; first++)
        {
            for (int second = first + 1; second < chords.Length; second++)
            {
                bool Overlap(bool? a, bool? b) => a is null || b is null || a == b;
                if (chords[first].Key == chords[second].Key && Overlap(chords[first].Control, chords[second].Control) && Overlap(chords[first].Shift, chords[second].Shift) && Overlap(chords[first].Alt, chords[second].Alt))
                {
                    throw new InvalidDataException("Two authoring commands have overlapping keyboard shortcuts.");
                }
            }
        }
    }
    public AuthoringShortcutSettings Rebind(UiEditCommand command, AuthoringKeyChord chord)
    {
        Dictionary<UiEditCommand, AuthoringKeyChord> bindings = Bindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[command] = chord;
        AuthoringShortcutSettings updated = this with { Bindings = bindings };
        updated.Validate();
        return updated;
    }
    private static Dictionary<UiEditCommand, AuthoringKeyChord> Defaults()
    {
        return new()
        {
            [UiEditCommand.TogglePicking] = new(Keys.F10),
            [UiEditCommand.Left] = new(Keys.Left, null, null),
            [UiEditCommand.Right] = new(Keys.Right, null, null),
            [UiEditCommand.Up] = new(Keys.Up, null, null),
            [UiEditCommand.Down] = new(Keys.Down, null, null),
            [UiEditCommand.Home] = new(Keys.Home, null, null),
            [UiEditCommand.End] = new(Keys.End, null, null),
            [UiEditCommand.Backspace] = new(Keys.Back, null, null),
            [UiEditCommand.Delete] = new(Keys.Delete, null, null),
            [UiEditCommand.Confirm] = new(Keys.Enter),
            [UiEditCommand.Cancel] = new(Keys.Escape),
            [UiEditCommand.LineBreak] = new(Keys.Enter, Shift: true),
            [UiEditCommand.PageBreak] = new(Keys.Enter, Control: true),
            [UiEditCommand.Complete] = new(Keys.Space, Control: true),
            [UiEditCommand.AcceptCompletion] = new(Keys.Tab),
            [UiEditCommand.SelectAll] = new(Keys.A, Control: true),
            [UiEditCommand.Copy] = new(Keys.C, Control: true),
            [UiEditCommand.Cut] = new(Keys.X, Control: true),
            [UiEditCommand.Paste] = new(Keys.V, Control: true),
            [UiEditCommand.Undo] = new(Keys.Z, Control: true),
            [UiEditCommand.Redo] = new(Keys.Y, Control: true),
            [UiEditCommand.PreviewPreviousPage] = new(Keys.PageUp),
            [UiEditCommand.PreviewNextPage] = new(Keys.PageDown),
            [UiEditCommand.PreviewPreviousChoice] = new(Keys.PageUp, Control: true),
            [UiEditCommand.PreviewNextChoice] = new(Keys.PageDown, Control: true)
        };
    }
}

public readonly record struct AuthoringKeyCommand(UiEditCommand Command, bool Shift, bool Control);

public sealed class AuthoringKeyboardInput
{
    private KeyboardState previous;
    private readonly Dictionary<UiEditCommand, double> nextRepeat = [];
    private double elapsedMilliseconds;
    public AuthoringShortcutSettings Settings { get; private set; } = new();
    public void Configure(AuthoringShortcutSettings settings)
    {
        settings.Validate();
        Settings = settings;
        nextRepeat.Clear();
    }
    public void Reset(KeyboardState currentlyHeld)
    {
        previous = currentlyHeld;
        nextRepeat.Clear();
    }
    public IReadOnlyList<AuthoringKeyCommand> Poll(KeyboardState keys, TimeSpan elapsed)
    {
        elapsedMilliseconds += Math.Max(0, elapsed.TotalMilliseconds);
        List<AuthoringKeyCommand> commands = [];
        foreach ((UiEditCommand command, AuthoringKeyChord chord) in Settings.Bindings)
        {
            bool down = chord.Matches(keys);
            bool wasDown = chord.Matches(previous);
            bool repeating = command is UiEditCommand.Left or UiEditCommand.Right or UiEditCommand.Up or UiEditCommand.Down or UiEditCommand.Home or UiEditCommand.End or UiEditCommand.Backspace or UiEditCommand.Delete;
            bool activate = down && !wasDown;
            if (activate && repeating)
            {
                nextRepeat[command] = elapsedMilliseconds + Settings.RepeatDelayMilliseconds;
            }
            else if (down && repeating && nextRepeat.TryGetValue(command, out double next) && elapsedMilliseconds >= next)
            {
                activate = true;
                nextRepeat[command] = elapsedMilliseconds + Settings.RepeatIntervalMilliseconds;
            }
            if (!down)
            {
                nextRepeat.Remove(command);
            }
            if (activate)
            {
                commands.Add(new(command, keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift), keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl)));
            }
        }
        previous = keys;
        return commands;
    }
    public bool IsHeld(UiEditCommand command, KeyboardState keys) => Settings.Bindings.TryGetValue(command, out AuthoringKeyChord? chord) && chord.Matches(keys);
}
