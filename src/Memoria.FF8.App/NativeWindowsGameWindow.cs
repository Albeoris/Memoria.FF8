using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Memoria.FF8;

/// <summary>Resolves the operating-system window without treating MonoGame Native's SDL_Window pointer as an HWND.</summary>
[SupportedOSPlatform("windows")]
internal static class NativeWindowsGameWindow
{
    internal static Microsoft.Xna.Framework.Point GetClientSize(nint handle)
    {
        if (!GetClientRect(handle, out ClientRectangle rectangle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not inspect the native game window's client size.");
        }
        return new(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
    }

    internal static nint ResolveHandle(string title)
    {
        List<nint> matches = [];
        EnumThreadWindow callback = (handle, _) =>
        {
            if (GetWindowTextLength(handle) != title.Length)
            {
                return true;
            }
            StringBuilder caption = new(title.Length + 1);
            GetWindowText(handle, caption, caption.Capacity);
            GetWindowThreadProcessId(handle, out uint processId);
            if (processId == Environment.ProcessId && caption.ToString() == title)
            {
                matches.Add(handle);
            }
            return true;
        };
        if (!EnumThreadWindows(GetCurrentThreadId(), callback, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate the native game thread's windows.");
        }
        if (matches.Count != 1)
        {
            throw new InvalidOperationException($"The native graphics host requires exactly one primary window; found {matches.Count} matching windows.");
        }
        return matches[0];
    }

    private delegate bool EnumThreadWindow(nint window, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct ClientRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumThreadWindows(uint threadId, EnumThreadWindow callback, nint parameter);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out ClientRectangle rectangle);
}
