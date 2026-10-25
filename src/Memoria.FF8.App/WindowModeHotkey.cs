using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed class WindowModeHotkey
{
    private bool previousEnter;
    private bool previousActive = true;
    private bool consumeEnter;

    public bool Read(KeyboardState keys, bool active)
    {
        bool enter = keys.IsKeyDown(Keys.Enter);
        bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
        bool toggle = active && previousActive && enter && !previousEnter && alt;
        consumeEnter = enter && (consumeEnter || alt);
        previousEnter = enter;
        previousActive = active;
        return toggle;
    }

    public KeyboardState Filter(KeyboardState keys)
    {
        return consumeEnter ? new KeyboardState(keys.GetPressedKeys().Where(key => key != Keys.Enter).ToArray()) : keys;
    }
}
