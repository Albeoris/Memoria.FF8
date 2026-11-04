using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

/// <summary>Keeps the native swapchain drawing dimensions equal to the host client area.</summary>
internal sealed class NativeWindowBackBufferSynchronizer : IDisposable
{
    private readonly GameWindow window;
    private readonly GraphicsDeviceManager graphics;
    private readonly nint handle;
    private readonly bool forceSwapchainRecreation;
    private (int Width, int Height, int Samples)? recreatedSwapchain;
    private bool synchronizing;

    internal NativeWindowBackBufferSynchronizer(GameWindow window, GraphicsDeviceManager graphics)
    {
        this.window = window;
        this.graphics = graphics;
        handle = OperatingSystem.IsWindows() ? NativeWindowsGameWindow.ResolveHandle(window.Title) : 0;
        forceSwapchainRecreation = OperatingSystem.IsMacOS();
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

    /// <summary>
    /// MonoGame's native Vulkan ResizeSwapchain ignores size-only changes and relies on out-of-date presentation, which then
    /// recreates at the previous extent clamped to the surface limits. MoltenVK reports open-ended extent limits, so the
    /// swapchain keeps its first size and is stretched over the window. A changed present interval is the only managed
    /// request that reaches a full recreation using the current presentation size and sample count.
    /// </summary>
    private void RecreateStaleSwapchain()
    {
        if (!forceSwapchainRecreation)
        {
            return;
        }
        PresentationParameters presentation = graphics.GraphicsDevice.PresentationParameters;
        var current = (presentation.BackBufferWidth, presentation.BackBufferHeight, presentation.MultiSampleCount);
        if (recreatedSwapchain == current)
        {
            return;
        }
        bool vsync = graphics.SynchronizeWithVerticalRetrace;
        graphics.SynchronizeWithVerticalRetrace = !vsync;
        graphics.ApplyChanges();
        graphics.SynchronizeWithVerticalRetrace = vsync;
        graphics.ApplyChanges();
        recreatedSwapchain = current;
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
