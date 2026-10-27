using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

/// <summary>Uses MonoGame's owned native window and desktop fullscreen lifecycle on Linux and macOS.</summary>
internal sealed class NativeDesktopGameWindow(GameWindow window, GraphicsDeviceManager graphics) : IGameWindowPlatform
{
    private Point windowedSize;
    private Point windowedPosition;

    public void Maximize()
    {
        DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        graphics.PreferredBackBufferWidth = display.Width;
        graphics.PreferredBackBufferHeight = display.Height;
        graphics.ApplyChanges();
    }

    public void EnterBorderless()
    {
        windowedSize = new Point(graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight);
        windowedPosition = window.Position;
        window.IsBorderless = true;
        graphics.HardwareModeSwitch = false;
        graphics.IsFullScreen = true;
        graphics.ApplyChanges();
    }

    public void RestoreWindowed()
    {
        graphics.IsFullScreen = false;
        window.IsBorderless = false;
        graphics.PreferredBackBufferWidth = windowedSize.X;
        graphics.PreferredBackBufferHeight = windowedSize.Y;
        graphics.ApplyChanges();
        window.Position = windowedPosition;
    }
}
