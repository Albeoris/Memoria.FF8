using Microsoft.Xna.Framework;

namespace Memoria.FF8;

/// <summary>Host composition supplies window operations; shared mode policy does not inspect native handles.</summary>
internal interface IGameWindowPlatform
{
    void Maximize();
    void EnterBorderless();
    void RestoreWindowed();
    WindowTransferState CaptureTransfer() => throw new NotSupportedException("The window adapter does not support process transfer.");
    void RestoreTransfer(WindowTransferState state) => throw new NotSupportedException("The window adapter does not support process transfer.");
    void PrepareTransferChild() => throw new NotSupportedException("The window adapter does not support hidden process transfer.");
    void RevealTransferChild() => throw new NotSupportedException("The window adapter does not support hidden process transfer.");
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
            return new WindowsGameWindow(window, NativeWindowsGameWindow.ResolveHandle(window.Title), graphics);
#else
            return new WindowsGameWindow(window, window.Handle, graphics);
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

    public WindowTransferState CaptureTransfer() => platform.CaptureTransfer();

    public void RestoreTransfer(WindowTransferState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        platform.RestoreTransfer(state);
        borderless = state.Borderless;
    }

    public void PrepareTransferChild() => platform.PrepareTransferChild();

    public void RevealTransferChild() => platform.RevealTransferChild();

    internal static WindowModeController PrepareTransferChild(GameWindow window, GraphicsDeviceManager graphics)
    {
        WindowModeController controller = new(CreatePlatform(window, graphics));
        controller.PrepareTransferChild();
        return controller;
    }
}
