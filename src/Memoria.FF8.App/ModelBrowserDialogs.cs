using Memoria.FF8.Platform;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal static class ModelBrowserDialogs
{
    internal static IDesktopDialogs Desktop { get; set; } = new SdlDesktopDialogs();
    internal static Func<GameConfirmationRequest, Task<bool>>? Confirmation { get; set; }

    private static FileDialogFilter[] ModelFilters(Func<string, string, string>? localizeText)
    {
        return [new(SystemTextCatalog.Translate(localizeText, "model.dialog.filter.glb", "glTF binary model (*.glb)"), ["glb"])];
    }

    public static async Task<string?> ExportAsync(nint owner, string name, Func<string, string, string>? localizeText = null)
    {
        string title = SystemTextCatalog.Translate(localizeText, "model.dialog.export.title", "Export to Blender");
        string filename = string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)) + ".glb";
        string? result = await PathAsync(Desktop.SaveFileAsync(new(title, filename, ModelFilters(localizeText))));
        return result is null || Path.HasExtension(result) ? result : result + ".glb";
    }

    public static Task<string?> ImportAsync(nint owner, Func<string, string, string>? localizeText = null)
    {
        string title = SystemTextCatalog.Translate(localizeText, "model.dialog.import.title", "Import from Blender (as mod)");
        return PathAsync(Desktop.OpenFileAsync(new(title, Filters: ModelFilters(localizeText))));
    }

    public static Task<string?> DestinationAsync(nint owner, string root, Func<string, string, string>? localizeText = null)
    {
        Directory.CreateDirectory(root);
        string title = SystemTextCatalog.Translate(localizeText, "model.dialog.destination.title", "Choose a mod package folder (or create one)");
        return PathAsync(Desktop.SelectFolderAsync(new(title, root)));
    }

    internal static async Task<string?> PathAsync(Task<DialogResult> operation)
    {
        DialogResult result = await operation;
        return result.Status switch
        {
            DialogStatus.Selected => Path.GetFullPath(result.Path ?? throw new InvalidDataException("The desktop dialog returned an empty path.")),
            DialogStatus.Cancelled => null,
            _ => throw new IOException(result.Error ?? "The desktop dialog failed.")
        };
    }

    internal static Task<bool> ShowConfirmationAsync(GameConfirmationRequest request)
    {
        return (Confirmation ?? throw new InvalidOperationException("The game confirmation host is unavailable."))(request);
    }

    public static Task<bool> ConfirmAsync(nint owner, string warning, string? title = null, string? question = null, Func<string, string, string>? localizeText = null)
    {
        string SystemText(string id, string original) => SystemTextCatalog.Translate(localizeText, id, original);
        return ShowConfirmationAsync(new GameConfirmationRequest(warning + "\n\n" + (question ?? SystemText("model.dialog.import.question", "Import this model anyway?")), title ?? SystemText("model.dialog.import.warning-title", "Model import warning"), SystemText("common.cancel", "Cancel"), SystemText("model.dialog.continue", "Continue"), SystemText("model.dialog.scroll-review", "Up / Down: scroll review")));
    }
}
