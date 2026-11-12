using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

[SupportedOSPlatform("windows")]
internal sealed class WindowsGameWindow(GameWindow window, nint nativeHandle, GraphicsDeviceManager? graphics = null) : IGameWindowPlatform
{
    private WindowPlacement placement;
    private WindowPlacement? transferPlacement;
    private bool transferHidden;
    private nint originalExtendedStyle;
    private WindowSubclass? transferSubclass;

    public void Maximize()
    {
        if (transferHidden)
        {
            throw new InvalidOperationException("Restore the transferred placement before revealing the replacement window.");
        }
        ShowWindow(nativeHandle, 3);
    }

    public void EnterBorderless()
    {
        placement = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>() };
        Check(GetWindowPlacement(nativeHandle, ref placement));
        MonitorInfo monitor = new() { Size = Marshal.SizeOf<MonitorInfo>() };
        Check(GetMonitorInfo(MonitorFromWindow(nativeHandle, 2), ref monitor));
        if (!transferHidden)
        {
            ShowWindow(nativeHandle, 9);
        }
        window.IsBorderless = true;
        NativeRect bounds = monitor.Monitor;
        Check(SetWindowPos(nativeHandle, 0, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0024));
    }

    public void RestoreWindowed()
    {
        window.IsBorderless = false;
        ApplyPlacement(placement);
    }

    public WindowTransferState CaptureTransfer()
    {
        WindowPlacement current = ReadPlacement();
        Check(GetWindowRect(nativeHandle, out NativeRect bounds));
        Point client = NativeWindowsGameWindow.GetClientSize(nativeHandle);
        var presentation = graphics?.GraphicsDevice.PresentationParameters;
        return new WindowTransferState(window.IsBorderless, ToRectangle(bounds), ToTransfer(current), window.IsBorderless ? ToTransfer(placement) : null, client.X, client.Y, presentation?.BackBufferWidth ?? client.X, presentation?.BackBufferHeight ?? client.Y);
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
        window.IsBorderless = state.Borderless;
        if (graphics is not null)
        {
            graphics.PreferredBackBufferWidth = state.BackBufferWidth;
            graphics.PreferredBackBufferHeight = state.BackBufferHeight;
            graphics.ApplyChanges();
        }
        placement = FromTransfer(state.WindowedPlacement ?? state.Placement);
        WindowPlacement current = FromTransfer(state.Placement);
        transferPlacement = current;
        ApplyPlacement(current);
        WindowTransferRectangle bounds = state.Bounds;
        Check(SetWindowPos(nativeHandle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0014));
        if (transferHidden)
        {
            ShowWindow(nativeHandle, 0);
        }
    }

    public void PrepareTransferChild()
    {
        if (transferHidden)
        {
            return;
        }
        originalExtendedStyle = GetWindowLongPtr(nativeHandle, -20);
        SetWindowLongPtr(nativeHandle, -20, originalExtendedStyle | 0x08000000);
        transferSubclass = FilterHiddenWindow;
        Check(SetWindowSubclass(nativeHandle, transferSubclass, 1, 0));
        transferHidden = true;
        ShowWindow(nativeHandle, 0);
    }

    public void RevealTransferChild()
    {
        if (!transferHidden)
        {
            return;
        }
        transferHidden = false;
        if (transferSubclass is not null)
        {
            Check(RemoveWindowSubclass(nativeHandle, transferSubclass, 1));
            transferSubclass = null;
        }
        SetWindowLongPtr(nativeHandle, -20, originalExtendedStyle);
        if (transferPlacement is WindowPlacement current)
        {
            Check(SetWindowPlacement(nativeHandle, in current));
        }
        ShowWindow(nativeHandle, transferPlacement?.ShowCommand == 3 ? 3 : 5);
        SetForegroundWindow(nativeHandle);
    }

    private void ApplyPlacement(WindowPlacement value)
    {
        if (transferHidden)
        {
            value.ShowCommand = 0;
        }
        Check(SetWindowPlacement(nativeHandle, in value));
    }

    private nint FilterHiddenWindow(nint handle, uint message, nuint wParam, nint lParam, nuint subclassId, nuint referenceData)
    {
        if (transferHidden && message == 0x0046)
        {
            WindowPosition position = Marshal.PtrToStructure<WindowPosition>(lParam);
            position.Flags = (position.Flags & ~0x0040u) | 0x0010u;
            Marshal.StructureToPtr(position, lParam, false);
        }
        return DefSubclassProc(handle, message, wParam, lParam);
    }

    private WindowPlacement ReadPlacement()
    {
        WindowPlacement value = new() { Length = Marshal.SizeOf<WindowPlacement>() };
        Check(GetWindowPlacement(nativeHandle, ref value));
        return value;
    }

    private static WindowTransferRectangle ToRectangle(NativeRect value) => new(value.Left, value.Top, value.Right - value.Left, value.Bottom - value.Top);

    private static WindowTransferPlacement ToTransfer(WindowPlacement value) => new(ToRectangle(value.NormalPosition), value.ShowCommand == 3, value.Flags, value.MinimumPosition.X, value.MinimumPosition.Y, value.MaximumPosition.X, value.MaximumPosition.Y);

    private static WindowPlacement FromTransfer(WindowTransferPlacement value) => new()
    {
        Length = Marshal.SizeOf<WindowPlacement>(),
        Flags = value.Flags,
        ShowCommand = value.Maximized ? 3 : 1,
        MinimumPosition = new NativePoint { X = value.MinimumX, Y = value.MinimumY },
        MaximumPosition = new NativePoint { X = value.MaximumX, Y = value.MaximumY },
        NormalPosition = new NativeRect { Left = value.NormalBounds.X, Top = value.NormalBounds.Y, Right = checked(value.NormalBounds.X + value.NormalBounds.Width), Bottom = checked(value.NormalBounds.Y + value.NormalBounds.Height) }
    };

    private static void Check(bool result)
    {
        if (!result)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        internal int Length;
        internal int Flags;
        internal int ShowCommand;
        internal NativePoint MinimumPosition;
        internal NativePoint MaximumPosition;
        internal NativeRect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        internal int Size;
        internal NativeRect Monitor;
        internal NativeRect WorkArea;
        internal int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPosition
    {
        internal nint Window;
        internal nint InsertAfter;
        internal int X;
        internal int Y;
        internal int Width;
        internal int Height;
        internal uint Flags;
    }

    private delegate nint WindowSubclass(nint window, uint message, nuint wParam, nint lParam, nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint window, WindowSubclass callback, nuint subclassId, nuint referenceData);
    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint window, WindowSubclass callback, nuint subclassId);
    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(nint window, ref WindowPlacement placement);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rectangle);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(nint window, in WindowPlacement placement);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo information);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);
}
