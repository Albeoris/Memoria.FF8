using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed class KeyboardInput
{
    private KeyboardState previous;

    public InputFrame Read(bool active)
    {
        return Read(Keyboard.GetState(), active);
    }

    internal InputFrame Read(KeyboardState current, bool active)
    {
        bool Pressed(Keys key)
        {
            return active && current.IsKeyDown(key) && previous.IsKeyUp(key);
        }
        var input = new InputFrame(active ? (current.IsKeyDown(Keys.Right) ? 1 : 0) - (current.IsKeyDown(Keys.Left) ? 1 : 0) : 0, active ? (current.IsKeyDown(Keys.Down) ? 1 : 0) - (current.IsKeyDown(Keys.Up) ? 1 : 0) : 0, Pressed(Keys.Enter) || Pressed(Keys.Space), Pressed(Keys.Escape), Pressed(Keys.Escape) || Pressed(Keys.Space), Pressed(Keys.F5), (Pressed(Keys.Down) ? 1 : 0) - (Pressed(Keys.Up) ? 1 : 0), Pressed(Keys.F9), Pressed(Keys.F6), Pressed(Keys.F10), (Pressed(Keys.Right) ? 1 : 0) - (Pressed(Keys.Left) ? 1 : 0), (Pressed(Keys.PageDown) ? 1 : 0) - (Pressed(Keys.PageUp) ? 1 : 0), Pressed(Keys.R));
        previous = current;
        return input;
    }
}
