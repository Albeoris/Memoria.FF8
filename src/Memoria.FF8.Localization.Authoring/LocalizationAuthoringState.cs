using System.Text.Json;

namespace Memoria.FF8.Localization.Authoring;

public sealed record LocalizationRecoveredDraft(string DestinationId, LocalizationRowSnapshot Base, LocalizationTargetValue Draft, int Caret, int SelectionLength, DateTimeOffset UpdatedAt)
{
    public LocalizationRecoveredConflict? Conflict { get; init; }
}
public sealed record LocalizationRecoveredConflict(LocalizationRowSnapshot Base, LocalizationTargetValue Ours, LocalizationRowSnapshot Theirs, IReadOnlyDictionary<LocalizationConflictPane, LocalizationTargetValue> Candidates);
public sealed record LocalizationReplayReview(LocalizationChange Change, LocalizationRowSnapshot Current, LocalizationTargetValue Proposed, bool Redo);

/// <summary>User authoring state is independent of distributed localization payloads.</summary>
public sealed record LocalizationAuthoringState
{
    public IReadOnlyList<LocalizationRecoveredDraft> Drafts { get; init; } = [];
    public IReadOnlyList<LocalizationChange> Changes { get; init; } = [];
    public IReadOnlyList<LocalizationObservation> Dialogues { get; init; } = [];

    public LocalizationAuthoringState ClearDialogues() => this with { Dialogues = [] };

    public static async Task<LocalizationAuthoringState> LoadAsync(string userStatePath, CancellationToken cancellation = default)
    {
        if (!File.Exists(userStatePath))
        {
            return new();
        }
        using FileStream stream = new(userStatePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<LocalizationAuthoringState>(stream, cancellationToken: cancellation).ConfigureAwait(false) ?? throw new InvalidDataException("The authoring state is empty.");
    }

    public async Task SaveAsync(string userStatePath, CancellationToken cancellation = default)
    {
        string fullPath = Path.GetFullPath(userStatePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, this, cancellationToken: cancellation).ConfigureAwait(false);
                await stream.FlushAsync(cancellation).ConfigureAwait(false);
                stream.Flush(true);
            }
            cancellation.ThrowIfCancellationRequested();
            if (File.Exists(fullPath))
            {
                File.Replace(temporary, fullPath, null);
            }
            else
            {
                File.Move(temporary, fullPath);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public static async Task<LocalizationReplayReview> ReviewReplayAsync(ILocalizationProjectStore store, ILocalizationTargetSyntax syntax, LocalizationChange change, bool redo, Func<string> source, CancellationToken cancellation = default)
    {
        LocalizationRowSnapshot current = await store.ReadRowAsync(change.Before.Identity, cancellation).ConfigureAwait(false);
        if (current.ProjectId != change.Before.ProjectId)
        {
            throw new InvalidOperationException("The selected record belongs to a different project.");
        }
        LocalizationTargetValue proposed = LocalizationReplay.Propose(syntax, change, current.Target, redo, source);
        return new(change, current, proposed, redo);
    }

    public static Task<LocalizationCommitResult> ApplyReplayAsync(ILocalizationProjectStore store, LocalizationReplayReview reviewed, CancellationToken cancellation = default) => store.CommitAsync(reviewed.Current, reviewed.Proposed, cancellation);
}
