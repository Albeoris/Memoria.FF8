using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Memoria.FF8.Mods.Localization;

internal readonly record struct LocalizationSnapshotIdentity(uint Volume, ulong FileIndex, ulong Length, ulong LastWrite)
{
    public static LocalizationSnapshotIdentity? Capture(FileStream stream)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out FileInformation information))
        {
            throw new IOException("Cannot inspect localization snapshot file identity.", new Win32Exception(Marshal.GetLastWin32Error()));
        }
        return new(information.VolumeSerialNumber, ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow, ((ulong)information.FileSizeHigh << 32) | information.FileSizeLow, ((ulong)information.LastWriteTimeHigh << 32) | information.LastWriteTimeLow);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
}
