using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.GameUi;

public enum LightingEditorCommand
{
    OpenEditor,
    ToggleVisibility,
    AddLight,
    DeleteLight,
    Translate,
    Rotate,
    Orbit,
    AxisX,
    AxisY,
    AxisZ,
    Confirm,
    Undo,
    Redo,
    Save
}

/// <summary>User preferences, deliberately separate from distributed scene lighting documents.</summary>
public sealed record LightingShortcutSettings
{
    public IReadOnlyDictionary<LightingEditorCommand, AuthoringKeyChord> Bindings { get; init; } = DefaultBindings();

    public void Validate()
    {
        if (Bindings is null || !Bindings.ContainsKey(LightingEditorCommand.OpenEditor) || !Bindings.ContainsKey(LightingEditorCommand.ToggleVisibility))
        {
            throw new InvalidDataException("Opening the light editor and showing its interface require usable shortcuts.");
        }
        foreach ((LightingEditorCommand command, AuthoringKeyChord chord) in Bindings)
        {
            if (!Enum.IsDefined(command) || chord is null || !Enum.IsDefined(chord.Key) || chord.Key is Keys.None or Keys.LeftControl or Keys.RightControl or Keys.LeftAlt or Keys.RightAlt or Keys.LeftShift or Keys.RightShift)
            {
                throw new InvalidDataException("A lighting shortcut must contain a valid command and a non-modifier key.");
            }
        }
        KeyValuePair<LightingEditorCommand, AuthoringKeyChord>[] entries = Bindings.ToArray();
        for (int first = 0; first < entries.Length; first++)
        {
            for (int second = first + 1; second < entries.Length; second++)
            {
                if (Overlaps(entries[first].Value, entries[second].Value))
                {
                    throw new InvalidDataException($"{Label(entries[first].Key)} and {Label(entries[second].Key)} use overlapping shortcuts.");
                }
            }
        }
    }

    public LightingShortcutSettings Rebind(LightingEditorCommand command, AuthoringKeyChord? chord)
    {
        Dictionary<LightingEditorCommand, AuthoringKeyChord> bindings = Bindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (chord is null)
        {
            bindings.Remove(command);
        }
        else
        {
            bindings[command] = chord;
        }
        LightingShortcutSettings result = this with { Bindings = bindings };
        result.Validate();
        return result;
    }

    public string Describe(LightingEditorCommand command)
    {
        return Bindings.TryGetValue(command, out AuthoringKeyChord? chord) ? chord.ToString() : "Unassigned";
    }

    public static bool Overlaps(AuthoringKeyChord first, AuthoringKeyChord second)
    {
        return first.Key == second.Key && ModifierOverlaps(first.Control, second.Control) && ModifierOverlaps(first.Shift, second.Shift) && ModifierOverlaps(first.Alt, second.Alt);
    }

    public static string Label(LightingEditorCommand command)
    {
        return command switch
        {
            LightingEditorCommand.OpenEditor => "Open current scene editor",
            LightingEditorCommand.ToggleVisibility => "Show / hide editor UI",
            LightingEditorCommand.AddLight => "Add spotlight",
            LightingEditorCommand.DeleteLight => "Delete selected light",
            LightingEditorCommand.Translate => "Move light",
            LightingEditorCommand.Rotate => "Aim light",
            LightingEditorCommand.Orbit => "Rotate light around pivot",
            LightingEditorCommand.AxisX => "Constrain X",
            LightingEditorCommand.AxisY => "Constrain Y",
            LightingEditorCommand.AxisZ => "Constrain Z (height)",
            LightingEditorCommand.Confirm => "Confirm edit",
            LightingEditorCommand.Undo => "Undo",
            LightingEditorCommand.Redo => "Redo",
            LightingEditorCommand.Save => "Save field",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }

    private static bool ModifierOverlaps(bool? first, bool? second)
    {
        return first is null || second is null || first == second;
    }

    private static Dictionary<LightingEditorCommand, AuthoringKeyChord> DefaultBindings()
    {
        return new()
        {
            [LightingEditorCommand.OpenEditor] = new(Keys.L, Control: true),
            [LightingEditorCommand.ToggleVisibility] = new(Keys.Escape),
            [LightingEditorCommand.AddLight] = new(Keys.Insert),
            [LightingEditorCommand.DeleteLight] = new(Keys.Delete),
            [LightingEditorCommand.Translate] = new(Keys.G),
            [LightingEditorCommand.Rotate] = new(Keys.R),
            [LightingEditorCommand.AxisX] = new(Keys.X),
            [LightingEditorCommand.AxisY] = new(Keys.Y),
            [LightingEditorCommand.AxisZ] = new(Keys.Z),
            [LightingEditorCommand.Confirm] = new(Keys.Enter),
            [LightingEditorCommand.Undo] = new(Keys.Z, Control: true),
            [LightingEditorCommand.Redo] = new(Keys.Y, Control: true),
            [LightingEditorCommand.Save] = new(Keys.S, Control: true)
        };
    }
}

/// <summary>Press edges only: held Insert/Delete never repeat scene mutations.</summary>
public sealed class LightingEditorKeyboard
{
    private KeyboardState previous;

    public void Reset(KeyboardState held)
    {
        previous = held;
    }

    public IReadOnlyList<LightingEditorCommand> Poll(KeyboardState current, LightingShortcutSettings settings)
    {
        List<LightingEditorCommand> commands = [];
        foreach ((LightingEditorCommand command, AuthoringKeyChord chord) in settings.Bindings)
        {
            if (chord.Matches(current) && !chord.Matches(previous))
            {
                commands.Add(command);
            }
        }
        previous = current;
        return commands;
    }
}
