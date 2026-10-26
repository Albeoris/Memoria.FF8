using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Memoria.FF8;

internal static class ModelBrowserDialogs
{
    public static string? Export(nint owner, string name, Func<string, string, string>? localizeText = null)
    {
        string SystemText(string id, string original) => localizeText?.Invoke(Memoria.FF8.Resources.Localization.SystemTextCatalog.Key(id), original) ?? original;
        using DpiScope dpi = new();
        SaveFileDialog dialog = new()
        {
            Title = SystemText("model.dialog.export.title", "Export to Blender"),
            Filter = SystemText("model.dialog.filter.glb", "glTF binary model (*.glb)") + "|*.glb",
            DefaultExt = ".glb",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)) + ".glb"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public static string? Import(nint owner, Func<string, string, string>? localizeText = null)
    {
        string SystemText(string id, string original) => localizeText?.Invoke(Memoria.FF8.Resources.Localization.SystemTextCatalog.Key(id), original) ?? original;
        using DpiScope dpi = new();
        OpenFileDialog dialog = new()
        {
            Title = SystemText("model.dialog.import.title", "Import from Blender (as mod)"),
            Filter = SystemText("model.dialog.filter.glb", "glTF binary model (*.glb)") + "|*.glb",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public static string? Destination(nint owner, string root, Func<string, string, string>? localizeText = null)
    {
        Directory.CreateDirectory(root);
        string SystemText(string id, string original) => localizeText?.Invoke(Memoria.FF8.Resources.Localization.SystemTextCatalog.Key(id), original) ?? original;
        using DpiScope dpi = new();
        OpenFolderDialog dialog = new()
        {
            Title = SystemText("model.dialog.destination.title", "Choose a mod package folder (or create one)"),
            InitialDirectory = root,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? Path.GetFullPath(dialog.FolderName) : null;
    }

    public static bool Confirm(nint owner, string warning, string? title = null, string? question = null, Func<string, string, string>? localizeText = null)
    {
        string SystemText(string id, string original) => localizeText?.Invoke(Memoria.FF8.Resources.Localization.SystemTextCatalog.Key(id), original) ?? original;
        using DpiScope dpi = new();
        return MessageBoxW(owner, warning + "\n\n" + (question ?? SystemText("model.dialog.import.question", "Import this model anyway?")), title ?? SystemText("model.dialog.import.warning-title", "Model import warning"), 0x134) == 6;
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
