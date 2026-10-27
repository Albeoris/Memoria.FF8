using Microsoft.Xna.Framework;

namespace Memoria.FF8;

/// <summary>Host composition supplies window operations; shared mode policy does not inspect native handles.</summary>
internal interface IGameWindowPlatform
{
    void Maximize();
    void EnterBorderless();
    void RestoreWindowed();
}

internal sealed class WindowModeController
{
    private readonly IGameWindowPlatform platform;
    private bool borderless;

    public WindowModeController(GameWindow window) : this(CreatePlatform(window))
    {
        borderless = window.IsBorderless;
    }

    internal WindowModeController(IGameWindowPlatform platform)
    {
        this.platform = platform;
    }

    internal static IGameWindowPlatform CreatePlatform(GameWindow window)
    {
        if (OperatingSystem.IsWindows())
        {
#if MEMORIA_VULKAN
            return new WindowsGameWindow(window, NativeWindowsGameWindow.ResolveHandle(window.Title));
#else
            return new WindowsGameWindow(window, window.Handle);
#endif
        }
        throw new PlatformNotSupportedException("This game host currently requires the Windows window adapter.");
    }

    public void Apply(bool requestedBorderless)
    {
        if (borderless == requestedBorderless)
        {
            return;
        }
        if (requestedBorderless)
        {
            platform.EnterBorderless();
        }
        else
        {
            platform.RestoreWindowed();
        }
        borderless = requestedBorderless;
    }
}
