using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed class ApplicationWindowIcon : IDisposable
{
    private nint window;
    private nint largeIcon;
    private nint smallIcon;
    private nint previousLargeIcon;
    private nint previousSmallIcon;

    internal static ApplicationWindowIcon Apply(GameWindow gameWindow)
    {
        ApplicationWindowIcon icon = new();
        if (OperatingSystem.IsWindows())
        {
            icon.ApplyWindows(gameWindow);
        }
        // MonoGame Native loads the embedded Icon.bmp through its own SDL runtime when creating the window.
        return icon;
    }

    [SupportedOSPlatform("windows")]
    private void ApplyWindows(GameWindow gameWindow)
    {
        using Stream stream = typeof(ApplicationWindowIcon).Assembly.GetManifestResourceStream("Memoria.Application.ico") ?? throw new InvalidOperationException("The application icon resource is missing.");
        using MemoryStream contents = new();
        stream.CopyTo(contents);
        byte[] bytes = contents.ToArray();
        window = NativeWindowsGameWindow.ResolveHandle(gameWindow.Title);
        try
        {
            largeIcon = CreateIcon(bytes, GetSystemMetrics(11), GetSystemMetrics(12));
            smallIcon = CreateIcon(bytes, GetSystemMetrics(49), GetSystemMetrics(50));
        }
        catch
        {
            if (largeIcon != 0)
            {
                DestroyIcon(largeIcon);
                largeIcon = 0;
            }
            throw;
        }
        previousLargeIcon = SendMessage(window, 0x0080, 1, largeIcon);
        previousSmallIcon = SendMessage(window, 0x0080, 0, smallIcon);
    }

    [SupportedOSPlatform("windows")]
    private static nint CreateIcon(byte[] bytes, int width, int height)
    {
        ReadOnlySpan<byte> directory = bytes;
        if (directory.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(directory) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(directory[2..]) != 1)
        {
            throw new InvalidDataException("The application icon directory is invalid.");
        }
        int count = BinaryPrimitives.ReadUInt16LittleEndian(directory[4..]);
        int selectedOffset = 0;
        int selectedLength = 0;
        int bestDistance = int.MaxValue;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = directory.Slice(6 + index * 16, 16);
            int entryWidth = entry[0] == 0 ? 256 : entry[0];
            int entryHeight = entry[1] == 0 ? 256 : entry[1];
            int distance = Math.Abs(entryWidth - width) + Math.Abs(entryHeight - height);
            if (distance < bestDistance)
            {
                selectedLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]));
                selectedOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]));
                bestDistance = distance;
            }
        }
        if (selectedLength <= 0)
        {
            throw new InvalidDataException("The application icon has no images.");
        }
        byte[] image = directory.Slice(selectedOffset, selectedLength).ToArray();
        nint result = CreateIconFromResourceEx(image, checked((uint)image.Length), true, 0x00030000, width, height, 0);
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the application window icon.");
        }
        return result;
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            if (largeIcon != 0)
            {
                SendMessage(window, 0x0080, 1, previousLargeIcon);
                DestroyIcon(largeIcon);
                largeIcon = 0;
            }
            if (smallIcon != 0)
            {
                SendMessage(window, 0x0080, 0, previousSmallIcon);
                DestroyIcon(smallIcon);
                smallIcon = 0;
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreateIconFromResourceEx(byte[] bits, uint size, [MarshalAs(UnmanagedType.Bool)] bool icon, uint version, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nuint parameter, nint value);
}
