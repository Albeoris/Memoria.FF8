namespace Memoria.FF8;

/// <summary>Forces a native swapchain resize while preserving the requested VSync setting.</summary>
internal sealed class NativeSwapchainRecreation
{
    private (int Width, int Height, int Samples)? recreatedSwapchain;

    internal void Synchronize(int width, int height, int samples, bool verticalSync, Action<bool> applyVerticalSync)
    {
        if (width < 1 || height < 1)
        {
            return;
        }
        var current = (width, height, samples);
        if (recreatedSwapchain == current)
        {
            return;
        }
        // TODO(TD-020): Remove the macOS present-interval toggle when MonoGame resizes its Vulkan swapchain directly.
        // A size-only reset leaves the old extent resident; changing the interval also recreates the MSAA attachments.
        recreatedSwapchain = null;
        try
        {
            applyVerticalSync(!verticalSync);
        }
        finally
        {
            applyVerticalSync(verticalSync);
        }
        recreatedSwapchain = current;
    }
}
