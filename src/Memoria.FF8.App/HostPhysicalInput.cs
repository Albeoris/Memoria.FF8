using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

/// <summary>A per-host gate shared by gameplay, menus and auxiliary windows.</summary>
internal sealed class HostPhysicalInput(bool enabled, Func<KeyboardState> readKeyboard, Func<GamePadState> readController, Func<MouseState> readMouse)
{
    public bool Enabled { get; } = enabled;

    public KeyboardState ReadKeyboard() => Enabled ? readKeyboard() : default;
    public GamePadState ReadController() => Enabled ? readController() : default;
    public MouseState ReadMouse() => Enabled ? readMouse() : default;
}
