using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed class KeyboardInput
{
    private KeyboardState previous;

    public bool SpeedToggleRequested { get; private set; }

    public ushort HeldFieldButtons { get; private set; }

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
        SpeedToggleRequested = Pressed(Keys.F1);
        bool confirmPressed = active && (current.IsKeyDown(Keys.Enter) || current.IsKeyDown(Keys.Space)) && !previous.IsKeyDown(Keys.Enter) && !previous.IsKeyDown(Keys.Space);
        HeldFieldButtons = active ? (ushort)((current.IsKeyDown(Keys.Up) ? 0x1000 : 0) | (current.IsKeyDown(Keys.Right) ? 0x2000 : 0) | (current.IsKeyDown(Keys.Down) ? 0x4000 : 0) | (current.IsKeyDown(Keys.Left) ? 0x8000 : 0) | (current.IsKeyDown(Keys.Enter) || current.IsKeyDown(Keys.Space) ? 0x40 : 0) | (current.IsKeyDown(Keys.Escape) ? 0x10 : 0)) : (ushort)0;
        var input = new InputFrame(active ? (current.IsKeyDown(Keys.Right) ? 1 : 0) - (current.IsKeyDown(Keys.Left) ? 1 : 0) : 0, active ? (current.IsKeyDown(Keys.Down) ? 1 : 0) - (current.IsKeyDown(Keys.Up) ? 1 : 0) : 0, confirmPressed, Pressed(Keys.Escape), Pressed(Keys.Escape) || Pressed(Keys.Space), Pressed(Keys.F5), (Pressed(Keys.Down) ? 1 : 0) - (Pressed(Keys.Up) ? 1 : 0), Pressed(Keys.F9), Pressed(Keys.F6), Pressed(Keys.F10), (Pressed(Keys.Right) ? 1 : 0) - (Pressed(Keys.Left) ? 1 : 0), (Pressed(Keys.PageDown) ? 1 : 0) - (Pressed(Keys.PageUp) ? 1 : 0), Pressed(Keys.R));
        previous = current;
        return input;
    }
}
