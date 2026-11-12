using Microsoft.Xna.Framework;
using SDL3;

namespace Memoria.FF8;

/// <summary>Uses MonoGame's owned native window and desktop fullscreen lifecycle on Linux and macOS.</summary>
internal sealed class NativeDesktopGameWindow : IGameWindowPlatform
{
    private readonly GameWindow window;
    private readonly GraphicsDeviceManager graphics;
    private readonly SDL.EventFilter eventWatch;
    private readonly uint windowId;
    private WindowTransferRectangle normalBounds;
    private Point windowedSize;
    private Point windowedPosition;
    private WindowTransferPlacement? windowedPlacement;
    private WindowTransferPlacement? transferPlacement;
    private bool transferHidden;
    private Exception? eventWatchFailure;

    internal NativeDesktopGameWindow(GameWindow window, GraphicsDeviceManager graphics)
    {
        this.window = window;
        this.graphics = graphics;
        windowId = SDL.GetWindowID(window.Handle);
        Check(SDL.GetWindowPosition(window.Handle, out int x, out int y));
        Check(SDL.GetWindowSize(window.Handle, out int width, out int height));
        normalBounds = new WindowTransferRectangle(x, y, width, height);
        eventWatch = TrackWindow;
        Check(SDL.AddEventWatch(eventWatch, 0));
    }

    public void Maximize()
    {
        windowedPlacement = ReadPlacement();
        Check(SDL.MaximizeWindow(window.Handle));
    }

    public void EnterBorderless()
    {
        windowedSize = new Point(graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight);
        windowedPosition = window.Position;
        windowedPlacement = ReadPlacement();
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
        CheckEventWatch();
        Check(SDL.GetWindowPosition(window.Handle, out int x, out int y));
        Check(SDL.GetWindowSize(window.Handle, out int width, out int height));
        Check(SDL.GetWindowSizeInPixels(window.Handle, out int clientWidth, out int clientHeight));
        WindowTransferRectangle bounds = new(x, y, width, height);
        WindowTransferPlacement current = ReadPlacement();
        var presentation = graphics.GraphicsDevice.PresentationParameters;
        return new WindowTransferState(window.IsBorderless, bounds, current, window.IsBorderless ? windowedPlacement : null, clientWidth, clientHeight, presentation.BackBufferWidth, presentation.BackBufferHeight);
    }

    public void RestoreTransfer(WindowTransferState state)
    {
        CheckEventWatch();
        if (state.ClientWidth <= 0 || state.ClientHeight <= 0 || state.BackBufferWidth <= 0 || state.BackBufferHeight <= 0 || state.Bounds.Width <= 0 || state.Bounds.Height <= 0)
        {
            throw new InvalidDataException("Transferred window dimensions must be positive.");
        }
        if (state.Borderless && state.WindowedPlacement is null)
        {
            throw new InvalidDataException("A borderless transfer requires its saved windowed placement.");
        }
        windowedPlacement = state.WindowedPlacement ?? state.Placement;
        WindowTransferRectangle normal = windowedPlacement.NormalBounds;
        normalBounds = normal;
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
        if (transferHidden)
        {
            Check(SDL.HideWindow(window.Handle));
        }
    }

    public void PrepareTransferChild()
    {
        CheckEventWatch();
        transferHidden = true;
        Check(SDL.SetWindowFocusable(window.Handle, false));
        Check(SDL.HideWindow(window.Handle));
    }

    public void RevealTransferChild()
    {
        CheckEventWatch();
        if (!transferHidden)
        {
            return;
        }
        transferHidden = false;
        Check(SDL.SetWindowFocusable(window.Handle, true));
        Check(SDL.ShowWindow(window.Handle));
        if (!window.IsBorderless && transferPlacement is not null)
        {
            RestorePlacement(transferPlacement);
        }
        Check(SDL.RaiseWindow(window.Handle));
    }

    private WindowTransferPlacement ReadPlacement()
    {
        bool maximized = (SDL.GetWindowFlags(window.Handle) & SDL.WindowFlags.Maximized) != 0;
        if (maximized)
        {
            return new WindowTransferPlacement(normalBounds, true);
        }
        Check(SDL.GetWindowPosition(window.Handle, out int x, out int y));
        Check(SDL.GetWindowSize(window.Handle, out int width, out int height));
        if (!window.IsBorderless)
        {
            normalBounds = new WindowTransferRectangle(x, y, width, height);
        }
        return new WindowTransferPlacement(new WindowTransferRectangle(x, y, width, height), maximized);
    }

    private bool TrackWindow(nint userData, ref SDL.Event value)
    {
        try
        {
            return TrackWindowCore(ref value);
        }
        catch (Exception exception)
        {
            eventWatchFailure = exception;
            return true;
        }
    }

    private bool TrackWindowCore(ref SDL.Event value)
    {
        SDL.EventType type = (SDL.EventType)value.Type;
        if (value.Window.WindowID != windowId)
        {
            return true;
        }
        if (type == SDL.EventType.WindowDestroyed)
        {
            SDL.RemoveEventWatch(eventWatch, 0);
            return true;
        }
        if (type == SDL.EventType.WindowShown && transferHidden)
        {
            Check(SDL.HideWindow(window.Handle));
            return true;
        }
        if (type is not (SDL.EventType.WindowMoved or SDL.EventType.WindowResized))
        {
            return true;
        }
        SDL.WindowFlags flags = SDL.GetWindowFlags(window.Handle);
        if ((flags & (SDL.WindowFlags.Fullscreen | SDL.WindowFlags.Maximized | SDL.WindowFlags.Minimized)) != 0 || window.IsBorderless)
        {
            return true;
        }
        if (type == SDL.EventType.WindowMoved)
        {
            normalBounds = normalBounds with { X = value.Window.Data1, Y = value.Window.Data2 };
        }
        else
        {
            normalBounds = normalBounds with { Width = value.Window.Data1, Height = value.Window.Data2 };
        }
        return true;
    }

    private void CheckEventWatch()
    {
        if (eventWatchFailure is not null)
        {
            throw new InvalidOperationException("Could not preserve the desktop game window during transfer.", eventWatchFailure);
        }
    }

    private void RestorePlacement(WindowTransferPlacement value)
    {
        Check(SDL.RestoreWindow(window.Handle));
        WindowTransferRectangle bounds = value.NormalBounds;
        Check(SDL.SetWindowPosition(window.Handle, bounds.X, bounds.Y));
        Check(SDL.SetWindowSize(window.Handle, bounds.Width, bounds.Height));
        if (value.Maximized)
        {
            Check(SDL.MaximizeWindow(window.Handle));
        }
    }

    private static void Check(bool result)
    {
        if (!result)
        {
            throw new InvalidOperationException($"Could not transfer the desktop game window: {SDL.GetError()}");
        }
    }
}
