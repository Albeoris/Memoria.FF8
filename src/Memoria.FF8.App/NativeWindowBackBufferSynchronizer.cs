using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

/// <summary>Keeps the native swapchain drawing dimensions equal to the host client area.</summary>
internal sealed class NativeWindowBackBufferSynchronizer : IDisposable
{
    private readonly GameWindow window;
    private readonly GraphicsDeviceManager graphics;
    private readonly nint handle;
    private readonly NativeSwapchainRecreation? swapchainRecreation;
    private bool synchronizing;

    internal NativeWindowBackBufferSynchronizer(GameWindow window, GraphicsDeviceManager graphics)
    {
        this.window = window;
        this.graphics = graphics;
        handle = OperatingSystem.IsWindows() ? NativeWindowsGameWindow.ResolveHandle(window.Title) : 0;
        swapchainRecreation = OperatingSystem.IsMacOS() ? new NativeSwapchainRecreation() : null;
        window.ClientSizeChanged += OnClientSizeChanged;
    }

    internal void Synchronize()
    {
        if (synchronizing)
        {
            return;
        }
        Point size = OperatingSystem.IsWindows() ? NativeWindowsGameWindow.GetClientSize(handle) : new Point(window.ClientBounds.Width, window.ClientBounds.Height);
        if (size.X < 1 || size.Y < 1)
        {
            return;
        }
        synchronizing = true;
        try
        {
            PresentationParameters presentation = graphics.GraphicsDevice.PresentationParameters;
            if (presentation.BackBufferWidth != size.X || presentation.BackBufferHeight != size.Y)
            {
                graphics.PreferredBackBufferWidth = size.X;
                graphics.PreferredBackBufferHeight = size.Y;
                graphics.ApplyChanges();
            }
            RecreateStaleSwapchain();
        }
        finally
        {
            synchronizing = false;
        }
    }

    private void RecreateStaleSwapchain()
    {
        if (swapchainRecreation is null)
        {
            return;
        }
        PresentationParameters presentation = graphics.GraphicsDevice.PresentationParameters;
        swapchainRecreation.Synchronize(presentation.BackBufferWidth, presentation.BackBufferHeight, presentation.MultiSampleCount, graphics.SynchronizeWithVerticalRetrace, ApplyVerticalSync);
    }

    private void ApplyVerticalSync(bool enabled)
    {
        graphics.SynchronizeWithVerticalRetrace = enabled;
        graphics.ApplyChanges();
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
