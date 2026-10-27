using Microsoft.Xna.Framework;

namespace Memoria.FF8;

/// <summary>Keeps the native swapchain drawing dimensions equal to the host client area.</summary>
internal sealed class NativeWindowBackBufferSynchronizer : IDisposable
{
    private readonly GameWindow window;
    private readonly GraphicsDeviceManager graphics;
    private readonly nint handle;
    private bool synchronizing;

    internal NativeWindowBackBufferSynchronizer(GameWindow window, GraphicsDeviceManager graphics)
    {
        this.window = window;
        this.graphics = graphics;
        handle = OperatingSystem.IsWindows() ? NativeWindowsGameWindow.ResolveHandle(window.Title) : 0;
        window.ClientSizeChanged += OnClientSizeChanged;
    }

    internal void Synchronize()
    {
        if (synchronizing)
        {
            return;
        }
        Point size = OperatingSystem.IsWindows() ? NativeWindowsGameWindow.GetClientSize(handle) : new Point(window.ClientBounds.Width, window.ClientBounds.Height);
        if (size.X < 1 || size.Y < 1 || graphics.GraphicsDevice.PresentationParameters.BackBufferWidth == size.X && graphics.GraphicsDevice.PresentationParameters.BackBufferHeight == size.Y)
        {
            return;
        }
        synchronizing = true;
        try
        {
            graphics.PreferredBackBufferWidth = size.X;
            graphics.PreferredBackBufferHeight = size.Y;
            graphics.ApplyChanges();
        }
        finally
        {
            synchronizing = false;
        }
    }

    private void OnClientSizeChanged(object? sender, EventArgs args)
    {
        Synchronize();
    }

    public void Dispose()
    {
        window.ClientSizeChanged -= OnClientSizeChanged;
    }
}
