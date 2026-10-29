namespace Memoria.FF8.Resources.Localization;

public static partial class SystemTextCatalog
{
    private static void AddWorkbenchHostDefaults(Dictionary<string, string> defaults)
    {
        const string prefix = "localization.workbench.host.";
        foreach ((string id, string text) in new (string, string)[]
        {
            ("catalog.title", "Localization · Canonical catalog"),
            ("catalog.help", "Inspect exact rows without picking workbench chrome. Reference data remains read-only."),
            ("shortcuts.title", "Localization · Editor shortcuts"),
            ("shortcuts.help", "Choose a command, release the current key, then press its new shortcut. Escape cancels capture."),
            ("toggle.state", "{0}: {1}"),
            ("toggle.help", "Authoring starts Off after every application launch."),
            ("state.on", "On"),
            ("state.off", "Off"),
            ("preview.state", "Preview selected mod: {0}"),
            ("preview.help", "Temporary preview does not enable a package or reorder the saved stack."),
            ("shortcuts.label", "Editor shortcuts"),
            ("shortcuts.entry.help", "Rebind editor commands, including native page break and Pick text / Navigate."),
            ("fonts.label", "Text presentation"),
            ("fonts.help", "Choose system/native fonts and layout under Config → Interface → Text and fonts."),
            ("project.label", "{0}{1}"),
            ("project.details", "{0} · {1}"),
            ("create.label", "Create a localization project"),
            ("create.help", "Create a project first; disabled projects can be edited."),
            ("shortcut.binding", "{0} · {1}"),
            ("shortcut.help", "Editor-only shortcut. Completion/IME owns its Enter and Escape before preview/save."),
            ("shortcut.capture", "Release the current key, then press the shortcut for {0}. Escape cancels."),
            ("catalog.label", "Inspect translation catalog"),
            ("catalog.entry.help", "Edit verified canonical rows, including the workbench's own labels."),
            ("source.reference", "Source Reference: {0}{1}"),
            ("source.reference.help", "Changes the designated translation source; reference display order and game scripts stay independent."),
            ("recover.label", "Recover draft: {0}"),
            ("recover.help", "Associate this saved draft with the selected project identity and current path: {0}"),
            ("choose.project", "Choose an editing mod"),
            ("picking.choose", "Choose a text fragment — Escape cancels"),
            ("package.status.Active", "Active"),
            ("package.status.Disabled", "Disabled"),
            ("package.status.IncompatibleEdition", "Incompatible edition"),
            ("package.status.NoUsableEntries", "No usable entries"),
            ("package.status.ReloadFailure", "Reload failed"),
            ("shortcut.Left", "Move left"), ("shortcut.Right", "Move right"),
            ("shortcut.Up", "Move up"), ("shortcut.Down", "Move down"),
            ("shortcut.Home", "Line start"), ("shortcut.End", "Line end"),
            ("shortcut.Backspace", "Backspace"), ("shortcut.Delete", "Delete"),
            ("shortcut.SelectAll", "Select all"), ("shortcut.Copy", "Copy"),
            ("shortcut.Cut", "Cut"), ("shortcut.Paste", "Paste"),
            ("shortcut.Undo", "Undo draft"), ("shortcut.Redo", "Redo draft"),
            ("shortcut.Confirm", "Preview / save"), ("shortcut.Cancel", "Cancel"),
            ("shortcut.LineBreak", "Insert line break"), ("shortcut.PageBreak", "Insert page break"),
            ("shortcut.Complete", "Complete control tag"), ("shortcut.AcceptCompletion", "Accept completion"),
            ("shortcut.PreviewPreviousPage", "Previous preview page"), ("shortcut.PreviewNextPage", "Next preview page"),
            ("shortcut.PreviewPreviousChoice", "Previous preview choice"), ("shortcut.PreviewNextChoice", "Next preview choice"),
            ("shortcut.TogglePicking", "Pick text / navigate")
        })
        {
            defaults.Add(prefix + id, text);
        }
    }
}
