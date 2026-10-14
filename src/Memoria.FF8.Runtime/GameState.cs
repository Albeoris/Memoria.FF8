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
    public NativeWorldConfiguration? WorldConfiguration { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeExamTimerState? ExamTimer { get; init; }
    public NativeMessageVariables? MessageVariables { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeEventRandomState? EventRandom { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeVibrationProcessState? VibrationProcess { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeFieldFadeState? FieldFade { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeInputConfiguration? InputConfiguration { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeFieldButtonInput? FieldButtonInput { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeCharacterVitals? CharacterVitals { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeCharacterAppearance? CharacterAppearance { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeCardInventory? CardInventory { get; init; }
    public NativeEconomy? Economy { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeShotAvailabilityState? ShotAvailability { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeFieldLocation? NativeFieldLocation { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeGlobalSoundProcess? GlobalSoundProcess { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeSalaryPresentation? SalaryPresentation { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeFieldShake? FieldShake { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeWorldProcess? WorldProcess { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeSeeDState? SeeD { get; init; }
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
public sealed record FieldTransition(int FieldId, float X, float Y, int Triangle, int Direction, short? NativeExtraWord = null)
{
    public static FieldTransition FromMapJumpOrigin(int fieldId, int triangle)
    {
        // Native521C30 stores both operands as WORDs; X/Y7FFF requests the triangle centroid.
        return new FieldTransition(unchecked((ushort)fieldId), short.MaxValue, short.MaxValue, unchecked((ushort)triangle), 0);
    }
}

public readonly record struct InputFrame(float Horizontal, float Vertical, bool Confirm, bool Cancel, bool Skip, bool Save, int MenuVertical = 0, bool QuickLoad = false, bool SaveMenu = false, bool LoadMenu = false, int MenuHorizontal = 0, int MenuPage = 0, bool RecreateSave = false);

public interface IEventServices
{
    NativeFieldFadeState? FieldFade => null;
    bool CanStoreFieldFade => false;
    void SetFieldFade(NativeFieldFadeState fade)
    {
        throw new NotSupportedException("Native field fade ownership is not implemented by this service.");
    }

    NativeVibrationProcessState? VibrationProcess => null;
    bool CanStoreVibrationProcess => false;
    void SetVibrationProcess(NativeVibrationProcessState vibration)
    {
        throw new NotSupportedException("Native vibration process ownership is not implemented by this service.");
    }

    NativeEventRandomState? EventRandom => null;
    bool CanStoreEventRandom => false;
    void SetEventRandom(NativeEventRandomState random)
    {
        throw new NotSupportedException("Native event random process ownership is not implemented by this service.");
    }

    NativeFieldButtonInput? FieldButtonInput => null;
    bool CanStoreFieldButtonInput => false;
    void SetFieldButtonInput(NativeFieldButtonInput input)
    {
        throw new NotSupportedException("Native sampled input process is not implemented by this event service.");
    }
    NativeInputConfiguration? InputConfiguration => null;
    bool CanStoreInputConfiguration => false;
    void SetInputConfiguration(NativeInputConfiguration configuration)
    {
        throw new NotSupportedException("Native saved input configuration is not implemented by this event service.");
    }

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
    NativeCardInventory? CardInventory => null;
    NativeEconomy? Economy => null;
    NativeShotAvailabilityState? ShotAvailability => null;
    NativeFieldLocation? NativeFieldLocation => null;
    FieldTransition? NativeIncomingFieldEntryWords => null;
    bool CanStoreNativePartyMenuOwners => false;
    void SetNativePartyMenuOwners(NativeCharacterVitals characterVitals, NativeEconomy economy, NativeShotAvailabilityState shots, NativeSalaryPresentation salaryPresentation, NativeExamTimerState? timer = null, NativeFieldLocation? fieldLocation = null, NativeVibrationProcessState? vibrationProcess = null, NativeWorldProcess? worldProcess = null, NativeFieldFadeState? fieldFade = null)
    {
        throw new NotSupportedException("Atomic party menu entry ownership is not implemented by this event service.");
    }

    NativeGlobalSoundProcess? GlobalSoundProcess => null;
    int QueryNativeMusicStatus()
    {
        NativeGlobalSoundProcess backend = GlobalSoundProcess ?? throw new NotSupportedException("Native MUSICSTATUS requires the explicit current audio backend owner.");
        return backend.QueryMusicStatus();
    }

    bool CanStoreGlobalSoundProcess => false;
    void SetGlobalSoundProcess(NativeGlobalSoundProcess state)
    {
        throw new NotSupportedException("Native global sound process ownership is not implemented by this event service.");
    }
    NativeFieldShake? FieldShake => null;
    NativeWorldProcess? WorldProcess => null;
    bool CanStoreWorldProcess => false;
    void SetWorldProcess(NativeWorldProcess process)
    {
        throw new NotSupportedException("Native world process ownership is not implemented by this event service.");
    }
    bool CanStoreFieldShake => false;
    void SetFieldShake(NativeFieldShake state)
    {
        throw new NotSupportedException("Native field shake process ownership is not implemented by this event service.");
    }
    NativeSalaryPresentation? SalaryPresentation => null;
    bool CanStoreSalaryPresentation => false;
    void SetSalaryPresentation(NativeSalaryPresentation state)
    {
        throw new NotSupportedException("Native salary presentation process ownership is not implemented by this event service.");
    }
    bool CanStoreEconomy => false;
    void SetEconomy(NativeEconomy economy)
    {
        throw new NotSupportedException("Native economy is not implemented by this event service.");
    }
    bool CanStoreCardInventory => false;
    NativeMessageVariables? MessageVariables => null;
    bool CanStoreMessageVariables => false;
    void SetMessageVariables(NativeMessageVariables bank)
    {
        throw new NotSupportedException("Native process message variables are not implemented by this service.");
    }
    NativeSeeDState? SeeD => null;
    bool CanStoreSeeD => false;
    void SetSeeD(NativeSeeDState state)
    {
        throw new NotSupportedException("Native SeeD rank and salary state is not implemented by this event service.");
    }
    void SetCardInventory(NativeCardInventory inventory)
    {
        throw new NotSupportedException("Native card inventory is not implemented by this event service.");
    }
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
    bool CanStoreNativeEventOwners => false;
    void SetNativeEventOwners(NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null, NativeVibrationProcessState? vibrationProcess = null, NativeFieldFadeState? fieldFade = null)
    {
        throw new NotSupportedException("Atomic native event-owner publication is not implemented by this service.");
    }
    NativeWorldConfiguration? WorldConfiguration => null;
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
