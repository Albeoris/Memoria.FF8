namespace Memoria.FF8.Localization.Authoring;

public enum LocalizationSessionMode
{
    Edit,
    Preview,
    Saving,
    Conflict,
    Closed
}
public enum LocalizationConflictPane
{
    Base,
    Ours,
    Theirs
}
public sealed class LocalizationConflict
{
    public LocalizationConflict(LocalizationRowSnapshot baseline, LocalizationTargetValue ours, LocalizationRowSnapshot theirs)
    {
        Base = baseline;
        Ours = ours;
        Theirs = theirs;
        Candidates = new Dictionary<LocalizationConflictPane, LocalizationTargetValue> { [LocalizationConflictPane.Base] = baseline.Target, [LocalizationConflictPane.Ours] = ours, [LocalizationConflictPane.Theirs] = theirs.Target };
    }
    public LocalizationRowSnapshot Base { get; }
    public LocalizationTargetValue Ours { get; }
    public LocalizationRowSnapshot Theirs { get; }
    public Dictionary<LocalizationConflictPane, LocalizationTargetValue> Candidates { get; }
    public void Reset(LocalizationConflictPane pane) => Candidates[pane] = pane switch { LocalizationConflictPane.Base => Base.Target, LocalizationConflictPane.Ours => Ours, _ => Theirs.Target };
}

public sealed class LocalizationEditingSession
{
    private readonly ILocalizationProjectStore store;
    private readonly ILocalizationTargetSyntax syntax;
    private readonly Stack<LocalizationTargetValue> undo = new();
    private readonly Stack<LocalizationTargetValue> redo = new();
    private readonly List<LocalizationChange> changes = [];
    public LocalizationEditingSession(ILocalizationProjectStore store, ILocalizationTargetSyntax syntax, LocalizationRowSnapshot snapshot)
    {
        this.store = store;
        this.syntax = syntax;
        Base = snapshot;
        Draft = snapshot.Target;
    }
    public LocalizationRowSnapshot Base { get; private set; }
    public LocalizationTargetValue Draft { get; private set; }
    public bool IsDirty => Draft != Base.Target;
    public LocalizationSessionMode Mode { get; private set; } = LocalizationSessionMode.Edit;
    public LocalizationConflict? Conflict { get; private set; }
    public IReadOnlyList<LocalizationChange> Changes => changes;
    public bool RecordChanges { get; set; } = true;
    public int Caret { get; set; }
    public int SelectionLength { get; set; }
    public LocalizationRecoveredDraft CaptureDraft(string destinationId)
    {
        return new(destinationId, Base, Draft, Caret, SelectionLength, DateTimeOffset.UtcNow)
        {
            Conflict = Conflict is null ? null : new(Conflict.Base, Conflict.Ours, Conflict.Theirs, new Dictionary<LocalizationConflictPane, LocalizationTargetValue>(Conflict.Candidates))
        };
    }
    public static LocalizationEditingSession RestoreDraft(ILocalizationProjectStore store, ILocalizationTargetSyntax syntax, LocalizationRecoveredDraft recovered)
    {
        LocalizationEditingSession session = new(store, syntax, recovered.Base)
        {
            Draft = recovered.Draft,
            Caret = recovered.Caret,
            SelectionLength = recovered.SelectionLength
        };
        if (recovered.Conflict is LocalizationRecoveredConflict saved)
        {
            session.Conflict = new(saved.Base, saved.Ours, saved.Theirs);
            foreach (var candidate in saved.Candidates)
            {
                session.Conflict.Candidates[candidate.Key] = candidate.Value;
            }
            session.Mode = LocalizationSessionMode.Conflict;
        }
        return session;
    }
    public void SetDraft(LocalizationTargetValue value)
    {
        if (Mode is LocalizationSessionMode.Saving or LocalizationSessionMode.Closed)
        {
            throw new InvalidOperationException("The submitted draft is frozen.");
        }
        undo.Push(Draft);
        redo.Clear();
        Draft = value;
        Mode = LocalizationSessionMode.Edit;
    }
    public void UndoDraft()
    {
        EnsureMutable();
        if (undo.TryPop(out LocalizationTargetValue? previous))
        {
            redo.Push(Draft);
            Draft = previous;
        }
    }
    public void RedoDraft()
    {
        EnsureMutable();
        if (redo.TryPop(out LocalizationTargetValue? next))
        {
            undo.Push(Draft);
            Draft = next;
        }
    }
    public void Preview()
    {
        EnsureMutable();
        Mode = LocalizationSessionMode.Preview;
    }
    public void ReturnToEdit()
    {
        EnsureMutable();
        Mode = LocalizationSessionMode.Edit;
    }
    public void Cancel()
    {
        EnsureMutable();
        Draft = Base.Target;
        Mode = LocalizationSessionMode.Closed;
    }
    public void SelectConflict(LocalizationConflictPane pane)
    {
        EnsureMutable();
        LocalizationConflict conflict = Conflict ?? throw new InvalidOperationException("No conflict is open.");
        Base = conflict.Theirs;
        Draft = conflict.Candidates[pane];
        Mode = LocalizationSessionMode.Preview;
    }
    public async Task<LocalizationCommitResult> SaveAsync(string summary = "Target changed", CancellationToken cancellation = default)
    {
        if (Mode != LocalizationSessionMode.Preview)
        {
            throw new InvalidOperationException("Review the candidate in Preview before saving.");
        }
        Mode = LocalizationSessionMode.Saving;
        LocalizationRowSnapshot before = Base;
        LocalizationCommitResult result;
        try
        {
            result = await store.CommitAsync(before, Draft, cancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Mode = LocalizationSessionMode.Preview;
            return new(LocalizationCommitStatus.Cancelled, null, "Save cancelled; draft retained.");
        }
        catch
        {
            Mode = LocalizationSessionMode.Preview;
            throw;
        }
        if (result.Status is LocalizationCommitStatus.Saved or LocalizationCommitStatus.Unchanged && result.Current is not null)
        {
            if (result.Status == LocalizationCommitStatus.Saved && RecordChanges)
            {
                changes.Add(new LocalizationChange(Guid.NewGuid(), before, Draft, LocalizationReplay.Changes(syntax, before.Target, Draft), summary));
            }
            Base = result.Current;
            Draft = Base.Target;
            Conflict = null;
            undo.Clear();
            redo.Clear();
            Mode = LocalizationSessionMode.Edit;
        }
        else if (result.Status == LocalizationCommitStatus.Conflict && result.Current is not null)
        {
            LocalizationConflict fresh = new(Conflict?.Base ?? before, Conflict?.Ours ?? Draft, result.Current);
            if (Conflict is not null)
            {
                foreach (var candidate in Conflict.Candidates)
                {
                    fresh.Candidates[candidate.Key] = candidate.Value;
                }
            }
            Conflict = fresh;
            Mode = LocalizationSessionMode.Conflict;
        }
        else
        {
            Mode = LocalizationSessionMode.Preview;
        }
        return result;
    }

    private void EnsureMutable()
    {
        if (Mode is LocalizationSessionMode.Saving or LocalizationSessionMode.Closed)
        {
            throw new InvalidOperationException("The submitted draft is frozen.");
        }
    }
}
