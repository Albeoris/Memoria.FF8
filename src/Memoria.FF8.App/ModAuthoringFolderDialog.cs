using System.Runtime.InteropServices;

namespace Memoria.FF8;

/// <summary>A shell folder picker that refuses navigation outside the configured authoring root.</summary>
internal static class ModAuthoringFolderDialog
{
    public static string? Choose(nint owner, string root)
    {
        if (!ModelBrowserDialogs.IsPackageDirectory(root, Path.Combine(root, "authoring-destination")))
        {
            throw new IOException("The configured authoring directory cannot contain directory links.");
        }
        Directory.CreateDirectory(root);
        if (!ModelBrowserDialogs.IsPackageDirectory(root, Path.Combine(root, "authoring-destination")))
        {
            throw new IOException("The configured authoring directory changed before opening the picker.");
        }
        nint previous = SetThreadDpiAwarenessContext(new nint(-4));
        IFileDialog? dialog = null;
        FolderEvents events = new(root);
        uint cookie = 0;
        try
        {
            dialog = (IFileDialog)new FileOpenDialog();
            Check(SHCreateItemFromParsingName(Path.GetFullPath(root), 0, typeof(IShellItem).GUID, out IShellItem folder));
            try
            {
                dialog.SetOptions(0x20u | 0x40u | 0x800u | 0x1000u);
                dialog.SetTitle("Choose or create a mod package inside Memoria");
                dialog.SetFolder(folder);
                dialog.SetDefaultFolder(folder);
                dialog.Advise(events, out cookie);
                int result = dialog.Show(owner);
                if (result == unchecked((int)0x800704C7))
                {
                    return null;
                }
                Check(result);
                dialog.GetResult(out IShellItem selection);
                try
                {
                    string path = PathOf(selection);
                    return IsDirectPackage(root, path) ? path : throw new IOException("Choose a direct child of the configured Memoria mod directory without directory links.");
                }
                finally
                {
                    Marshal.ReleaseComObject(selection);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(folder);
            }
        }
        finally
        {
            try
            {
                if (cookie != 0)
                {
                    dialog!.Unadvise(cookie);
                }
            }
            finally
            {
                try
                {
                    if (dialog is not null)
                    {
                        Marshal.ReleaseComObject(dialog);
                    }
                }
                finally
                {
                    if (previous != 0)
                    {
                        SetThreadDpiAwarenessContext(previous);
                    }
                }
            }
        }
    }

    internal static bool IsDirectPackage(string root, string path) => ModelBrowserDialogs.IsPackageDirectory(root, path) && string.Equals(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))), Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase);

    private static string PathOf(IShellItem item)
    {
        item.GetDisplayName(0x80058000, out nint value);
        try
        {
            return Marshal.PtrToStringUni(value) ?? throw new IOException("A filesystem folder is required.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(value);
        }
    }

    private static void Check(int result) => Marshal.ThrowExceptionForHR(result);

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class FolderEvents(string root) : IFileDialogEvents
    {
        public int OnFileOk(IFileDialog dialog)
        {
            try
            {
                dialog.GetResult(out IShellItem selected);
                try
                {
                    return IsDirectPackage(root, PathOf(selected)) ? 0 : 1;
                }
                finally
                {
                    Marshal.ReleaseComObject(selected);
                }
            }
            catch (Exception exception) when (exception is COMException or IOException or ArgumentException or UnauthorizedAccessException)
            {
                return 1;
            }
        }

        public int OnFolderChanging(IFileDialog dialog, IShellItem folder)
        {
            try
            {
                string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(PathOf(folder)));
                bool atRoot = string.Equals(path, Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase);
                bool safe = ModelBrowserDialogs.IsPackageDirectory(root, atRoot ? Path.Combine(root, "authoring-destination") : path);
                return safe ? 0 : unchecked((int)0x80070005);
            }
            catch (Exception exception) when (exception is COMException or IOException or ArgumentException or UnauthorizedAccessException)
            {
                return unchecked((int)0x80070005);
            }
        }

        public void OnFolderChange(IFileDialog dialog)
        {
        }
        public void OnSelectionChange(IFileDialog dialog)
        {
        }
        public void OnShareViolation(IFileDialog dialog, IShellItem item, out uint response) => response = 0;
        public void OnTypeChange(IFileDialog dialog)
        {
        }
        public void OnOverwrite(IFileDialog dialog, IShellItem item, out uint response) => response = 0;
    }

    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialog
    {
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint context, in Guid handler, in Guid iid, out nint result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint kind, out nint name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count, nint types);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IFileDialogEvents events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName(out nint name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem result);
        void AddPlace(IShellItem item, uint position);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(in Guid guid);
        void ClearClientData();
        void SetFilter(nint filter);
    }

    [ComImport, Guid("973510DB-7D7F-452B-8975-74A85828D354"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialogEvents
    {
        [PreserveSig] int OnFileOk(IFileDialog dialog);
        [PreserveSig] int OnFolderChanging(IFileDialog dialog, IShellItem folder);
        void OnFolderChange(IFileDialog dialog);
        void OnSelectionChange(IFileDialog dialog);
        void OnShareViolation(IFileDialog dialog, IShellItem item, out uint response);
        void OnTypeChange(IFileDialog dialog);
        void OnOverwrite(IFileDialog dialog, IShellItem item, out uint response);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, nint context, in Guid iid, out IShellItem item);

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);
}
