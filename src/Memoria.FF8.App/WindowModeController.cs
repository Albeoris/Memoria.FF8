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
            return new WindowsGameWindow(window);
        }
        throw new PlatformNotSupportedException("The DX11 game host requires Windows. A native host must supply its own window adapter.");
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
