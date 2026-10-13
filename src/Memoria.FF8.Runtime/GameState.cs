using System.Text.Json;

namespace Memoria.FF8.Runtime;

public enum SceneKind
{
    Title,
    Field,
    Movie,
    WorldMap
}

/// <summary>A recreation checkpoint, not an original FF8 save-file format.</summary>
public sealed record GameState
{
    public int Version { get; init; } = 1;
    public SceneKind Scene { get; init; } = SceneKind.Title;
    public int FieldId { get; init; } = 74;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? PreviousFieldId { get; init; }
    public string FieldName { get; init; } = "start0";
    public int EntryPoint { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public int MovieIndex { get; init; } = 30;
    public double MovieSeconds { get; init; }
    public MoviePlaybackState? MoviePlayback { get; init; }
    public FieldTransition? FieldArrival { get; init; }
    public WorldTransition? WorldArrival { get; init; }
    public WorldState? World { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WorldState? DepartingWorldState { get; init; }
    public NativeSavedWorldState? SavedWorld { get; init; }
    public NativeWorldControlState? WorldControls { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeExamTimerState? ExamTimer { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeCharacterVitals? CharacterVitals { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeCharacterAppearance? CharacterAppearance { get; init; }
    public Dictionary<int, int> Variables { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, NativeMemoryValue>? NativeVariableValues { get; init; }
    public JsonElement? EventState { get; init; }
    public JsonElement? DepartingFieldState { get; init; }
    public DialogueDraft? DialogueDraft { get; init; }
    public DebugSceneView? DebugView { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ReplayJournal? ReplayJournal { get; init; }
}

public sealed record DialogueDraft(int Channel, int MessageId, int SelectedChoice, string EnteredName);
public sealed record MoviePlaybackState(int FrameIndex, int TerminalFrame, bool IsPlaying, bool Completed, bool Skipped);
public sealed record FieldTransition(int FieldId, float X, float Y, int Triangle, int Direction, short? NativeExtraWord = null);

public readonly record struct InputFrame(float Horizontal, float Vertical, bool Confirm, bool Cancel, bool Skip, bool Save, int MenuVertical = 0, bool QuickLoad = false, bool SaveMenu = false, bool LoadMenu = false, int MenuHorizontal = 0, int MenuPage = 0, bool RecreateSave = false);

public interface IEventServices
{
    string? ReplayMovieSourceHash => null;
    ReplayJournal? ReplayJournal => null;
    SceneKind ReplayScene => SceneKind.Field;
    int? ReplayFieldId => null;
    bool IsReplayCaptureActive => false;
    MoviePlaybackState? ReplayMoviePlayback => null;
    void RecordReplayEntry(ReplayJournalEntry entry)
    {
    }
    void MarkReplayUnavailable(string reason)
    {
    }
    IDisposable? BeginReplayCapture() => null;
    IReadOnlyDictionary<int, NativeMemoryValue>? NativeVariableValues => null;
    bool CanStoreNativeVariableValues => false;
    void SetNativeVariableValues(Dictionary<int, NativeMemoryValue>? values)
    {
        throw new NotSupportedException("Typed uncertain native memory is not implemented by this event service.");
    }
    NativeExamTimerState? ExamTimer => null;
    NativeCharacterVitals? CharacterVitals => null;
    NativeCharacterAppearance? CharacterAppearance => null;
    void SetCharacterAppearance(NativeCharacterAppearance appearance)
    {
        throw new NotSupportedException("Native character appearance is not implemented by this event service.");
    }
    void SetCharacterVitals(NativeCharacterVitals vitals)
    {
        throw new NotSupportedException("Native character vitals are not implemented by this event service.");
    }
    void SetExamTimer(NativeExamTimerState timer)
    {
        throw new NotSupportedException("Native exam timer state is not implemented by this event service.");
    }
    NativeWorldControlState? WorldControls => null;
    void SetWorldControls(NativeWorldControlState controls)
    {
        throw new NotSupportedException("Native world control flags are not implemented by this event service.");
    }
    bool IsMoviePlaying { get; }
    bool? NativeMovieClockFlag => null;
    void RequestMovie(int index);
    void RequestField(int fieldId, int entryPoint);
    void RequestField(FieldTransition transition)
    {
        throw new NotSupportedException("Coordinate field transitions are not implemented by this event service.");
    }
    void ShowDialog(string text);
    void DismissDialog()
    {
    }
    void RequestWorld(WorldTransition transition)
    {
        throw new NotSupportedException("World transitions are not implemented by this event service.");
    }
}
