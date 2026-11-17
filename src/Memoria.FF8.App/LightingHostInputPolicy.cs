using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

/// <summary>Host-reserved chords and gameplay bindings that would compete with opening the editor.</summary>
internal static class LightingHostInputPolicy
{
    internal static string? FindConflict(ControlsProfile profile, LightingEditorCommand command, AuthoringKeyChord chord)
    {
        if (chord.Key == Keys.Enter && chord.Alt != false)
        {
            return "Alt+Enter is reserved for the window mode. Choose another lighting shortcut.";
        }
        if (command != LightingEditorCommand.OpenEditor)
        {
            return null;
        }
        if (chord.Key == Keys.F7)
        {
            return "F7 is reserved for menu presentation. Choose another opening shortcut.";
        }
        foreach (ControlAction action in Enum.GetValues<ControlAction>())
        {
            foreach (InputChord binding in profile.GetBindings(action, ControlDevice.Keyboard).Concat(ControlsProfile.GetPermanentBindings(action, ControlDevice.Keyboard)))
            {
                if (!binding.Keys.Contains(chord.Key))
                {
                    continue;
                }
                // Gameplay chords allow extra modifier keys. Test a complete possible candidate state, not only the existing binding's minimum state.
                List<Keys> keys = [.. binding.Keys];
                AddModifier(keys, chord.Control, Keys.LeftControl, Keys.RightControl);
                AddModifier(keys, chord.Shift, Keys.LeftShift, Keys.RightShift);
                AddModifier(keys, chord.Alt, Keys.LeftAlt, Keys.RightAlt);
                KeyboardState candidate = new(keys.Distinct().ToArray());
                if (chord.Matches(candidate) && binding.Matches(candidate, default))
                {
                    return "Opening shortcut conflicts with " + action + ". Choose another chord.";
                }
            }
        }
        return null;
    }

    private static void AddModifier(List<Keys> keys, bool? requested, Keys left, Keys right)
    {
        if (requested == true && !keys.Contains(left) && !keys.Contains(right))
        {
            keys.Add(left);
        }
    }
}
