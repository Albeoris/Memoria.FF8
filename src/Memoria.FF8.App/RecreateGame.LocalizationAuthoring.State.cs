using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Localization.Authoring;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private sealed record AuthoringWorkspace(string? LastProject, LocalizationWorkbenchWorkspace Layout, LocalizationAuthoringState State, AuthoringShortcutSettings? Shortcuts = null);
    private Task<AuthoringWorkspace?>? authoringWorkspaceLoading;
    private Task? authoringWorkspaceSaving;
    private AuthoringWorkspace? authoringRecoveredWorkspace;
    private DateTime authoringWorkspaceSavedAt;
    private bool authoringWorkspaceInitialized;
    private bool authoringExitRequested;
    private bool authoringExitAllowed;
    private bool authoringExitNeedsFinalSave;
    private string AuthoringStatePath => Path.Combine(Path.GetDirectoryName(UiPreferencesPath)!, "localization-authoring.json");

    private void InitializeAuthoringWorkspace()
    {
        authoringWorkspaceInitialized = true;
        string path = AuthoringStatePath;
        authoringWorkspaceLoading = Task.Run(async () =>
        {
            if (!File.Exists(path))
            {
                return null;
            }
            await using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<AuthoringWorkspace>(input).ConfigureAwait(false);
        });
        localizationWorkbench.DisableCancelled = () => authoringExitRequested = false;
    }

    private void PollAuthoringWorkspace()
    {
        if (authoringWorkspaceLoading is { IsCompleted: true } loaded)
        {
            authoringWorkspaceLoading = null;
            try
            {
                authoringRecoveredWorkspace = loaded.GetAwaiter().GetResult();
                if (authoringRecoveredWorkspace is { } recovered)
                {
                    authoringRememberedProject ??= recovered.LastProject;
                    if (authoringRememberedProject is { } remembered && authoringSelection is null && authoringSelectionTask is null && resourceHost?.Mods.Catalog.Packages.FirstOrDefault(package => package.Id == remembered && package.HasLocalization) is { } package)
                    {
                        StartAuthoringSelection(package, enableWhenReady: false);
                    }
                    authoringKeyboard.Configure(recovered.Shortcuts ?? new AuthoringShortcutSettings());
                    localizationWorkbench.LoadHistory(recovered.State.Changes.TakeLast(10000).Concat(localizationWorkbench.ReadHistory()).DistinctBy(change => change.Id));
                    if (!localizationWorkbench.Enabled)
                    {
                        localizationWorkbench.LoadWorkspace(recovered.Layout);
                    }
                    if (recovered.State.Drafts.Count != 0)
                    {
                        authoringNotice = "Recoverable translation drafts are available in Debug Room → Localization.";
                    }
                    if (recovered.State.Changes.Count > 10000)
                    {
                        authoringNotice = "Older saved-edit history was truncated to the most recent 10,000 records.";
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
            {
                authoringNotice = "Authoring recovery state could not be read: " + exception.Message;
            }
        }
        if (authoringWorkspaceSaving is { IsCompleted: true } saved)
        {
            authoringWorkspaceSaving = null;
            try
            {
                saved.GetAwaiter().GetResult();
                if (authoringExitRequested && !localizationWorkbench.Enabled)
                {
                    if (authoringExitNeedsFinalSave || authoringWorkspaceLoading is not null)
                    {
                        if (authoringWorkspaceLoading is null)
                        {
                            authoringExitNeedsFinalSave = false;
                            SaveAuthoringWorkspace();
                        }
                    }
                    else
                    {
                        authoringExitAllowed = true;
                        Exit();
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                authoringExitRequested = false;
                authoringNotice = "Authoring state could not be saved; recovery remains available in this session: " + exception.Message;
            }
        }
        if (authoringExitRequested && localizationWorkbench.Enabled && !localizationWorkbench.DisableRequested && localizationWorkbench.Session?.Mode != LocalizationSessionMode.Saving)
        {
            localizationWorkbench.RequestDisable();
        }
        if (authoringExitRequested && !localizationWorkbench.Enabled && authoringWorkspaceLoading is null && authoringWorkspaceSaving is null && authoringExitNeedsFinalSave)
        {
            authoringExitNeedsFinalSave = false;
            SaveAuthoringWorkspace();
        }
        if (authoringWorkspaceLoading is null && authoringWorkspaceSaving is null && DateTime.UtcNow - authoringWorkspaceSavedAt >= TimeSpan.FromSeconds(2))
        {
            SaveAuthoringWorkspace();
        }
    }

    private void ForgetRecoveredAuthoringDraft()
    {
        if (authoringRecoveredWorkspace is { } recovered && localizationWorkbench.Session is { } session)
        {
            authoringRecoveredWorkspace = recovered with { State = recovered.State with { Drafts = recovered.State.Drafts.Where(draft => draft.Base.ProjectId != session.Base.ProjectId || draft.Base.Identity != session.Base.Identity).ToArray() } };
        }
    }

    private void SaveAuthoringWorkspace()
    {
        LocalizationRecoveredDraft[] drafts = localizationWorkbench.IsEditing && (localizationWorkbench.Session!.IsDirty || localizationWorkbench.Session.Conflict is not null)
            ? [localizationWorkbench.Session.CaptureDraft(authoringSelection?.Package.Id ?? "")]
            : [];
        var previousDrafts = authoringRecoveredWorkspace?.State.Drafts ?? [];
        var state = new LocalizationAuthoringState
        {
            Changes = localizationWorkbench.ReadHistory(),
            Drafts = previousDrafts.Where(draft => !drafts.Any(current => current.Base.ProjectId == draft.Base.ProjectId && current.Base.Identity == draft.Base.Identity)).Concat(drafts).ToArray()
        };
        AuthoringWorkspace snapshot = new(authoringRememberedProject, localizationWorkbench.ReadWorkspace(), state, authoringKeyboard.Settings);
        string path = AuthoringStatePath;
        authoringWorkspaceSavedAt = DateTime.UtcNow;
        authoringWorkspaceSaving = Task.Run(async () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (FileStream output = new(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(output, snapshot).ConfigureAwait(false);
                    await output.FlushAsync().ConfigureAwait(false);
                    output.Flush(true);
                }
                File.Move(staged, path, true);
            }
            finally
            {
                File.Delete(staged);
            }
        });
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (authoringWorkspaceInitialized && !authoringExitAllowed)
        {
            args.Cancel = true;
            if (!authoringExitRequested)
            {
                authoringExitRequested = true;
                authoringExitNeedsFinalSave = true;
            }
            if (localizationWorkbench.RequestDisable() && authoringWorkspaceSaving is null && authoringWorkspaceLoading is null)
            {
                authoringExitNeedsFinalSave = false;
                SaveAuthoringWorkspace();
            }
            return;
        }
        base.OnExiting(sender, args);
    }
}
