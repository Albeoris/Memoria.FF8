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

    internal static IGameWindowPlatform CreatePlatform(GameWindow window, GraphicsDeviceManager? graphics = null)
    {
        if (OperatingSystem.IsWindows())
        {
#if MEMORIA_NATIVE
            return new WindowsGameWindow(window, NativeWindowsGameWindow.ResolveHandle(window.Title));
#else
            return new WindowsGameWindow(window, window.Handle);
#endif
        }
#if MEMORIA_NATIVE
        if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && graphics is not null)
        {
            return new NativeDesktopGameWindow(window, graphics);
        }
#endif
        throw new PlatformNotSupportedException("This game host requires a supported desktop window adapter and graphics manager.");
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
