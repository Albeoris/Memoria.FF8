using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Memoria.FF8;

internal static class ModelBrowserDialogs
{
    public static string? Export(nint owner, string name)
    {
        using DpiScope dpi = new();
        SaveFileDialog dialog = new()
        {
            Title = "Export to Blender",
            Filter = "glTF binary model (*.glb)|*.glb",
            DefaultExt = ".glb",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)) + ".glb"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public static string? Import(nint owner)
    {
        using DpiScope dpi = new();
        OpenFileDialog dialog = new()
        {
            Title = "Import from Blender (as mod)",
            Filter = "glTF binary model (*.glb)|*.glb",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public static string? Destination(nint owner, string root)
    {
        Directory.CreateDirectory(root);
        using DpiScope dpi = new();
        while (true)
        {
            OpenFolderDialog dialog = new()
            {
                Title = "Choose a mod package folder inside Mods/Memoria (or create one)",
                InitialDirectory = root,
                Multiselect = false
            };
            if (dialog.ShowDialog() != true)
            {
                return null;
            }
            if (IsPackageDirectory(root, dialog.FolderName) && string.Equals(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(dialog.FolderName))), Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(dialog.FolderName);
            }
            MessageBoxW(owner, "Choose a mod package directly inside Mods/Memoria. Nested folders and directory links cannot be used as package roots.", "Model import", 0x30);
        }
    }

    internal static bool IsPackageDirectory(string root, string path)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        // Check every existing ancestor, including the root, to prevent junction escapes.
        for (DirectoryInfo? directory = new(fullPath); directory is not null; directory = directory.Parent)
        {
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }
        return true;
    }

    public static bool Confirm(nint owner, string warning, string title = "Model import warning", string question = "Import this model anyway?")
    {
        using DpiScope dpi = new();
        return MessageBoxW(owner, warning + "\n\n" + question, title, 0x134) == 6;
    }

    private sealed class DpiScope : IDisposable
    {
        private readonly nint previous = SetThreadDpiAwarenessContext(new nint(-4));

        public void Dispose()
        {
            if (previous != 0)
            {
                SetThreadDpiAwarenessContext(previous);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint owner, string text, string caption, uint type);
}
