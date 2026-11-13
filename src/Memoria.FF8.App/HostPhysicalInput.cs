using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

/// <summary>A per-host gate shared by gameplay, menus and auxiliary windows.</summary>
internal sealed class HostPhysicalInput(bool enabled, Func<bool> hasFocus, Func<KeyboardState> readKeyboard, Func<GamePadState> readController, Func<MouseState> readMouse)
{
    private KeyboardState? keyboard;
    private GamePadState? controller;
    private MouseState? mouse;
    private MouseState inactiveMouse;
    private bool suppressLeftButton;

    public bool CanRead => enabled && hasFocus();

    public void BeginFrame()
    {
        keyboard = null;
        controller = null;
        mouse = null;
        if (!CanRead)
        {
            suppressLeftButton = true;
        }
    }

    public void LoseFocus()
    {
        BeginFrame();
        suppressLeftButton = true;
    }

    public KeyboardState ReadKeyboard()
    {
        if (!CanRead)
        {
            keyboard = null;
            return default;
        }
        return keyboard ??= readKeyboard();
    }

    public GamePadState ReadController()
    {
        if (!CanRead)
        {
            controller = null;
            return default;
        }
        return controller ??= readController();
    }

    public MouseState ReadMouse()
    {
        if (!CanRead)
        {
            suppressLeftButton = true;
            mouse = null;
            return inactiveMouse;
        }
        if (mouse is { } sample)
        {
            return sample;
        }
        MouseState current = readMouse();
        // Keep the last position and cumulative wheel count without carrying any held buttons.
        inactiveMouse = new MouseState(current.X, current.Y, current.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        suppressLeftButton &= current.LeftButton == ButtonState.Pressed;
        sample = suppressLeftButton ? new MouseState(current.X, current.Y, current.ScrollWheelValue, ButtonState.Released, current.MiddleButton, current.RightButton, current.XButton1, current.XButton2) : current;
        mouse = sample;
        return sample;
    }
}
