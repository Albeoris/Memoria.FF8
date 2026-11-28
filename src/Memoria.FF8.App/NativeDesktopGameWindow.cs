using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

/// <summary>Uses MonoGame's native Vulkan exports on Linux and macOS so the SDL2 window stays with the runtime that owns it.</summary>
internal sealed class NativeDesktopGameWindow : IGameWindowPlatform
{
    private readonly GameWindow window;
    private readonly GraphicsDeviceManager graphics;
    private Point windowedSize;
    private Point windowedPosition;
    private WindowTransferPlacement? windowedPlacement;
    private WindowTransferPlacement? transferPlacement;
    private WindowTransferState? deferredTransfer;
    private bool transferHidden;

    internal NativeDesktopGameWindow(GameWindow window, GraphicsDeviceManager graphics)
    {
        this.window = window;
        this.graphics = graphics;
    }

    public bool SupportsHiddenPresentation => ReadGeometry().SupportsHiddenPresentation != 0;

    internal bool IsHidden => ReadGeometry().Hidden != 0;

    public void Maximize()
    {
        windowedPlacement = ReadGeometry().Placement;
        MaximizeWindow(window.Handle);
    }

    public void EnterBorderless()
    {
        windowedSize = new Point(graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight);
        windowedPosition = window.Position;
        windowedPlacement = ReadGeometry().Placement;
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
        if (windowedPlacement is not null)
        {
            RestorePlacement(windowedPlacement);
        }
    }

    public WindowTransferState CaptureTransfer()
    {
        WindowGeometry geometry = ReadGeometry();
        var presentation = graphics.GraphicsDevice.PresentationParameters;
        return new WindowTransferState(window.IsBorderless, geometry.Bounds.ToTransferRectangle(), geometry.Placement, window.IsBorderless ? windowedPlacement : null, geometry.PixelWidth, geometry.PixelHeight, presentation.BackBufferWidth, presentation.BackBufferHeight);
    }

    public void RestoreTransfer(WindowTransferState state)
    {
        if (state.ClientWidth <= 0 || state.ClientHeight <= 0 || state.BackBufferWidth <= 0 || state.BackBufferHeight <= 0 || state.Bounds.Width <= 0 || state.Bounds.Height <= 0)
        {
            throw new InvalidDataException("Transferred window dimensions must be positive.");
        }
        if (state.Borderless && state.WindowedPlacement is null)
        {
            throw new InvalidDataException("A borderless transfer requires its saved windowed placement.");
        }
        if (transferHidden && !SupportsHiddenPresentation)
        {
            // MonoGame can present the current frame as part of ApplyChanges.
            deferredTransfer = state;
            return;
        }

        windowedPlacement = state.WindowedPlacement ?? state.Placement;
        WindowTransferRectangle normal = windowedPlacement.NormalBounds;
        windowedSize = new Point(normal.Width, normal.Height);
        windowedPosition = new Point(normal.X, normal.Y);
        graphics.HardwareModeSwitch = false;
        graphics.IsFullScreen = state.Borderless;
        window.IsBorderless = state.Borderless;
        graphics.PreferredBackBufferWidth = state.BackBufferWidth;
        graphics.PreferredBackBufferHeight = state.BackBufferHeight;
        graphics.ApplyChanges();
        transferPlacement = state.Placement;
        if (!state.Borderless)
        {
            RestorePlacement(state.Placement);
        }
    }

    public void PrepareTransferChild()
    {
        SetTransferHidden(window.Handle, 1);
        transferHidden = true;
    }

    public void RevealTransferChild()
    {
        if (!transferHidden)
        {
            return;
        }
        SetTransferHidden(window.Handle, 0);
        transferHidden = false;
        if (deferredTransfer is { } state)
        {
            deferredTransfer = null;
            RestoreTransfer(state);
            return;
        }
        if (!window.IsBorderless && transferPlacement is not null)
        {
            RestorePlacement(transferPlacement);
        }
    }

    private WindowGeometry ReadGeometry()
    {
        if (GetWindowGeometry(window.Handle, out WindowGeometry geometry) == 0)
        {
            throw new InvalidOperationException("Could not read the MonoGame desktop window geometry.");
        }

        return geometry;
    }

    private void RestorePlacement(WindowTransferPlacement value)
    {
        WindowTransferRectangle bounds = value.NormalBounds;
        RestoreWindowPlacement(window.Handle, bounds.X, bounds.Y, bounds.Width, bounds.Height, value.Maximized ? 1 : 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public readonly WindowTransferRectangle ToTransferRectangle() => new(X, Y, Width, Height);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowGeometry
    {
        public WindowRectangle Bounds;
        public WindowRectangle NormalBounds;
        public int PixelWidth;
        public int PixelHeight;
        public int Maximized;
        public int Hidden;
        public int SupportsHiddenPresentation;

        public readonly WindowTransferPlacement Placement => new(NormalBounds.ToTransferRectangle(), Maximized != 0);
    }

    [DllImport("mgruntime", EntryPoint = "Memoria_Window_GetGeometry", CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetWindowGeometry(nint window, out WindowGeometry geometry);

    [DllImport("mgruntime", EntryPoint = "Memoria_Window_Maximize", CallingConvention = CallingConvention.Cdecl)]
    private static extern void MaximizeWindow(nint window);

    [DllImport("mgruntime", EntryPoint = "Memoria_Window_RestorePlacement", CallingConvention = CallingConvention.Cdecl)]
    private static extern void RestoreWindowPlacement(nint window, int x, int y, int width, int height, int maximized);

    [DllImport("mgruntime", EntryPoint = "Memoria_Window_SetTransferHidden", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetTransferHidden(nint window, int hidden);
}
