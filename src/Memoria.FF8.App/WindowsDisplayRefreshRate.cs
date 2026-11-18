using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Memoria.FF8;

[SupportedOSPlatform("windows")]
internal sealed class WindowsDisplayRefreshRate : IDisposable
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint DisplayChange = 0x007E;
    private const uint NonClientDestroy = 0x0082;
    private const int CurrentDisplaySettings = -1;
    private readonly nint window;
    private readonly WindowSubclass subclass;
    private readonly DisplayRefreshRateCache cache;
    private bool installed;
    private bool disposed;

    internal WindowsDisplayRefreshRate(nint window)
    {
        this.window = window;
        subclass = ObserveWindowMessage;
        cache = new(() => MonitorFromWindow(window, MonitorDefaultToNearest), ReadRefreshRate);
        installed = window != 0 && SetWindowSubclass(window, subclass, 1, 0);
    }

    internal int? RefreshRate => !disposed && installed ? cache.RefreshRate : null;

    internal void Invalidate()
    {
        cache.Invalidate();
    }

    private nint ObserveWindowMessage(nint handle, uint message, nuint wParam, nint lParam, nuint subclassId, nuint referenceData)
    {
        if (message == DisplayChange)
        {
            Invalidate();
        }
        else if (message == NonClientDestroy)
        {
            RemoveWindowSubclass(handle, subclass, subclassId);
            installed = false;
            Invalidate();
        }
        return DefSubclassProc(handle, message, wParam, lParam);
    }

    private static int? ReadRefreshRate(nint monitor)
    {
        MonitorInformation information = new() { Size = Marshal.SizeOf<MonitorInformation>() };
        if (!GetMonitorInfo(monitor, ref information) || string.IsNullOrWhiteSpace(information.DeviceName))
        {
            return null;
        }
        DisplayMode mode = new() { Size = checked((ushort)Marshal.SizeOf<DisplayMode>()) };
        return EnumDisplaySettings(information.DeviceName, CurrentDisplaySettings, ref mode) && mode.Frequency is > 1 and <= int.MaxValue ? (int)mode.Frequency : null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        if (installed)
        {
            RemoveWindowSubclass(window, subclass, 1);
            installed = false;
        }
        Invalidate();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInformation
    {
        internal int Size;
        internal NativeRectangle Monitor;
        internal NativeRectangle WorkArea;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;
    }

    // DEVMODEW: only the structure size and nominal display frequency are consumed.
    [StructLayout(LayoutKind.Explicit, Size = 220)]
    private struct DisplayMode
    {
        [FieldOffset(68)]
        internal ushort Size;
        [FieldOffset(184)]
        internal uint Frequency;
    }

    private delegate nint WindowSubclass(nint window, uint message, nuint wParam, nint lParam, nuint subclassId, nuint referenceData);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInformation information);
    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string deviceName, int mode, ref DisplayMode settings);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint window, WindowSubclass callback, nuint subclassId, nuint referenceData);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint window, WindowSubclass callback, nuint subclassId);
    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);
}

internal sealed class DisplayRefreshRateCache(Func<nint> readMonitor, Func<nint, int?> readRefreshRate)
{
    private nint cachedMonitor;
    private bool cached;
    private int? cachedRate;

    internal int? RefreshRate
    {
        get
        {
            nint monitor = readMonitor();
            if (monitor == 0)
            {
                Invalidate();
                return null;
            }
            if (!cached || monitor != cachedMonitor)
            {
                int? observed = readRefreshRate(monitor);
                cachedRate = observed > 1 ? observed : null;
                cachedMonitor = monitor;
                cached = true;
            }
            return cachedRate;
        }
    }

    internal void Invalidate()
    {
        cached = false;
        cachedMonitor = 0;
        cachedRate = null;
    }
}
