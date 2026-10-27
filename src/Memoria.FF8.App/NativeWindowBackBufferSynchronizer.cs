using System.Runtime.Versioning;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

/// <summary>Keeps the native swapchain's drawing dimensions equal to the Windows client area.</summary>
[SupportedOSPlatform("windows")]
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
        handle = NativeWindowsGameWindow.ResolveHandle(window.Title);
        window.ClientSizeChanged += OnClientSizeChanged;
    }

    internal void Synchronize()
    {
        if (synchronizing)
        {
            return;
        }
        Point size = NativeWindowsGameWindow.GetClientSize(handle);
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
