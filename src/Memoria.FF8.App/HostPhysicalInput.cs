using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

/// <summary>A per-host gate shared by gameplay, menus and auxiliary windows.</summary>
internal sealed class HostPhysicalInput(bool enabled, Func<bool> hasFocus, Func<KeyboardState> readKeyboard, Func<GamePadState> readController, Func<MouseState> readMouse)
{
    private MouseState inactiveMouse;

    public bool CanRead => enabled && hasFocus();

    public KeyboardState ReadKeyboard() => CanRead ? readKeyboard() : default;
    public GamePadState ReadController() => CanRead ? readController() : default;
    public MouseState ReadMouse()
    {
        if (!CanRead)
        {
            return inactiveMouse;
        }
        MouseState mouse = readMouse();
        // Keep the last position and cumulative wheel count without carrying any held buttons.
        inactiveMouse = new MouseState(mouse.X, mouse.Y, mouse.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        return mouse;
    }
}
