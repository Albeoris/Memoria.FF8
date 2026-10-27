using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

[SupportedOSPlatform("windows")]
internal sealed class WindowsGameWindow(GameWindow window, nint nativeHandle) : IGameWindowPlatform
{
    private WindowPlacement placement;

    public void Maximize()
    {
        ShowWindow(nativeHandle, 3);
    }

    public void EnterBorderless()
    {
        placement = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>() };
        Check(GetWindowPlacement(nativeHandle, ref placement));
        MonitorInfo monitor = new() { Size = Marshal.SizeOf<MonitorInfo>() };
        Check(GetMonitorInfo(MonitorFromWindow(nativeHandle, 2), ref monitor));
        ShowWindow(nativeHandle, 9);
        window.IsBorderless = true;
        NativeRect bounds = monitor.Monitor;
        Check(SetWindowPos(nativeHandle, 0, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0024));
    }

    public void RestoreWindowed()
    {
        window.IsBorderless = false;
        Check(SetWindowPlacement(nativeHandle, in placement));
    }

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(nint window, ref WindowPlacement placement);
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
