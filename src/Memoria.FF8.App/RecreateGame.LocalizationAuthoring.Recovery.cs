using Memoria.FF8.GameUi;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private Task<(LocalizationRowSnapshot Current, LocalizationRecoveredDraft Draft)>? authoringRecoveryTask;
    private Task<LocalizationCommitResult>? authoringRoleTask;
    private long authoringRecoveryOperation;

    private IEnumerable<MenuEntry> AuthoringProjectActions()
    {
        if (authoringSelection is not { } selected)
        {
            yield break;
        }
        yield return new("authoring-catalog", AuthoringHostText("catalog.label", "Inspect translation catalog"), AuthoringHostText("catalog.entry.help", "Edit verified canonical rows, including the workbench's own labels."), "localization-catalog");
        LocalizationSourceRole primary = LocalizationSourceRoles.Resolve(selected.Project.Manifest);
        foreach (string dataset in selected.Project.Manifest.ReferenceLanguages.Values.Append(selected.Project.Manifest.FieldScriptBaseLanguage).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return new("authoring-source-" + dataset, FormatAuthoringHostText("source.reference", "Source Reference: {0}{1}", dataset, dataset == primary.Language ? " ✓" : ""), AuthoringHostText("source.reference.help", "Changes the designated translation source; reference display order and game scripts stay independent."), Action: new("localization-workbench", "source-reference:" + dataset));
        }
        foreach (LocalizationRecoveredDraft draft in (authoringRecoveredWorkspace?.State.Drafts ?? []).Where(draft => draft.Base.ProjectId == selected.Project.Manifest.ProjectId))
        {
            yield return new("authoring-recover-" + draft.Base.Identity.Key, FormatAuthoringHostText("recover.label", "Recover draft: {0}", draft.Base.Identity.Key), FormatAuthoringHostText("recover.help", "Associate this saved draft with the selected project identity and current path: {0}", selected.Package.RootDirectory), Action: new("localization-workbench", "recover:" + draft.Base.Identity.Key));
        }
    }

    private IReadOnlyList<MenuEntry> AuthoringCatalogEntries()
    {
        if (authoringSelection is not { } selected)
        {
            return [new("select-project", AuthoringHostText("choose.project", "Choose an editing mod"), "", "localization-workbench")];
        }
        return selected.Project.Entries.Select(row => new MenuEntry(row.Key, row.Key, row.Target ?? row.Source ?? LocalizeUiText(SystemTextCatalog.Key("localization.workbench.no.text"), "No text available"), Action: new("localization-workbench", "row:" + row.Key))).ToArray();
    }

    private bool HandleAuthoringProjectAction(string action)
    {
        if (authoringSelection is not { } selected || authoringStore is null)
        {
            return false;
        }
        if (authoringRecoveryTask is not null || authoringRoleTask is not null)
        {
            return action.StartsWith("recover:", StringComparison.Ordinal) || action.StartsWith("source-reference:", StringComparison.Ordinal);
        }
        if (action.StartsWith("row:", StringComparison.Ordinal) && selected.Rows.TryGetValue(action[4..], out LocalizationRecord? row))
        {
            OpenAuthoringBinding(new(new(row.Key, row.Source ?? "", row.Target ?? row.Source ?? "", row.Target ?? row.Source ?? "") { Category = row.Category, File = row.File, PackageId = selected.Package.Id, ProjectId = selected.Project.Manifest.ProjectId }, default, Detached: true));
            gameMenus?.Close();
            return true;
        }
        if (action.StartsWith("recover:", StringComparison.Ordinal))
        {
            if (localizationWorkbench.IsEditing && (localizationWorkbench.Session!.IsDirty || localizationWorkbench.Session.Conflict is not null))
            {
                authoringNotice = "Finish the current draft or conflict review before recovering another draft.";
                return true;
            }
            LocalizationRecoveredDraft? draft = authoringRecoveredWorkspace?.State.Drafts.SingleOrDefault(draft => draft.Base.ProjectId == selected.Project.Manifest.ProjectId && draft.Base.Identity.Key == action[8..]);
            if (draft is null)
            {
                authoringNotice = "That recovery record is no longer available for this project identity.";
                return true;
            }
            authoringRecoveryOperation = authoringOperation;
            var store = authoringStore;
            authoringRecoveryTask = Task.Run(async () => (await store.ReadRowAsync(draft.Base.Identity, authoringCancellation.Token).ConfigureAwait(false), draft));
            gameMenus?.Close();
            return true;
        }
        if (action.StartsWith("source-reference:", StringComparison.Ordinal))
        {
            if (localizationWorkbench.IsEditing && (localizationWorkbench.Session!.IsDirty || localizationWorkbench.Session.Conflict is not null))
            {
                authoringNotice = "Finish the current Target/conflict before changing the project's Source Reference.";
                return true;
            }
            LocalizationRecord? first = selected.Project.Entries.FirstOrDefault();
            if (first is null)
            {
                authoringNotice = "The project has no canonical row from which to validate its current revision.";
                return true;
            }
            var store = authoringStore;
            string language = action[17..];
            authoringRecoveryOperation = authoringOperation;
            authoringRoleTask = Task.Run(async () =>
            {
                LocalizationRowSnapshot expected = await store.ReadRowAsync(new(first.Category, first.File, first.Key), authoringCancellation.Token).ConfigureAwait(false);
                return await store.SetSourceReferenceAsync(expected, language, authoringCancellation.Token).ConfigureAwait(false);
            });
            return true;
        }
        return false;
    }

    private void PollAuthoringRecovery()
    {
        if (authoringRecoveryTask is { IsCompleted: true } recovered)
        {
            authoringRecoveryTask = null;
            try
            {
                var recovery = recovered.GetAwaiter().GetResult();
                if (authoringRecoveryOperation != authoringOperation || recovery.Current.ProjectId != authoringSelection?.Project.Manifest.ProjectId)
                {
                    return;
                }
                LocalizationEditingSession session = LocalizationEditingSession.RestoreDraft(authoringStore!, authoringSyntax, recovery.Draft);
                editingAuthoringBinding = new(recovery.Current.Identity.Key, recovery.Current.Row.Source ?? "", recovery.Draft.Draft.Text ?? "", recovery.Draft.Draft.Text ?? "");
                editingAuthoringDialogue = null;
                editingAuthoringHit = new(editingAuthoringBinding, default, Detached: true);
                localizationWorkbench.SystemTemplate = recovery.Current.Identity.Category == "System";
                localizationWorkbench.PageBreakSupported = recovery.Current.Identity.Category != "System";
                localizationWorkbench.PositionSupported = false;
                localizationWorkbench.Open(session, AuthoringLanguages(recovery.Current.Row), authoringSelection!.Package.Name, ValidateAuthoringTarget);
                authoringNotice = recovery.Current.Revision == recovery.Draft.Base.Revision ? "Recovered unsaved draft. Captured gameplay context is unavailable after restart." : "Recovered draft: the row changed externally. Saving requires a fresh three-way review; captured gameplay context is unavailable.";
                SynchronizeUiMoviePause();
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or ArgumentException or OperationCanceledException)
            {
                authoringNotice = exception.Message;
            }
        }
        if (authoringRoleTask is { IsCompleted: true } role)
        {
            authoringRoleTask = null;
            try
            {
                LocalizationCommitResult result = role.GetAwaiter().GetResult();
                if (authoringRecoveryOperation == authoringOperation)
                {
                    authoringNotice = result.Message ?? result.Status.ToString();
                    if (result.Status is LocalizationCommitStatus.Saved or LocalizationCommitStatus.Unchanged)
                    {
                        ReloadAuthoringSelection();
                    }
                    gameMenus?.InvalidateLiveData();
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or OperationCanceledException)
            {
                authoringNotice = exception.Message;
            }
        }
    }
}
