using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    public ImmutableArray<EnemyDefeatedEvent> PendingAchievementFacts { get; init; } = [];
    public ImmutableArray<BattleKillTotalObservedEvent> PendingAchievementObservations { get; init; } = [];
    public ImmutableArray<OriginalAchievementObservedEvent> PendingOriginalAchievementObservations { get; init; } = [];
    public ImmutableArray<OriginalAchievementActionEvent> PendingOriginalAchievementActions { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OriginalAchievementDrawCursor? OriginalAchievementDrawCursor { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalAchievementMutationContext { get; init; }
    public long OriginalAchievementMutationSequence { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OriginalAchievementObservedEvent? LastOriginalAchievementObservation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleAchievementCursor? AchievementCursor { get; init; }
    /// <summary>Process DWORD1D9CDC0, initially zero and set to1 by the PC DEBUG handler.</summary>
    public bool NativeFieldDebugRequested { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativePcSaveData? OriginalSave { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAbandonment[]? AbandonedNativeBattles { get; init; }
    public GameplayPreferences GameplayPreferences { get; init; } = new();
    public ImmutableArray<int> RefinedCardIds { get; init; } = [];
    public GameplayPreferences? PendingGameplayPreferences { get; init; }
    public bool PendingMagicModeReview { get; init; }
    [JsonIgnore]
    public MagicSystemState? MagicSystem => CharacterVitals?.MagicSystem;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingEntityName[]? PendingEntityNames { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTripleTriadPlayState? TripleTriadPlay { get; init; }
    [JsonRequired]
    public NativeFieldBarsState? FieldBars { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRetainedMemory? NativeBattleMemory { get; init; }
    public NativeKnownMagic? KnownMagic { get; init; }
    public NativeScannedModels? ScannedModels { get; init; }
    public NativeCrtRandomState? CrtRandom { get; init; }
    [JsonRequired]
    public BattleEncounterOccurrence? PendingBattleEncounter { get; init; }
    [JsonRequired]
    public string[] AppliedBattleResultIds { get; init; } = [];
    public NativeBattleVariableState? BattleVariables { get; init; }
    public int Version { get; init; } = 1;
    public SceneKind Scene { get; init; } = SceneKind.Title;
    public int FieldId { get; init; } = 74;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int? PreviousFieldId { get; init; }
    public string FieldName { get; init; } = "start0";
    public int EntryPoint { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public int MovieIndex { get; init; } = 30;
    public double MovieSeconds { get; init; }
    public MoviePlaybackState? MoviePlayback { get; init; }
    [JsonRequired]
    public NativeMovieSource? MovieSource { get; init; }
    public FieldTransition? FieldArrival { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldRequestPacket? FieldRequestPacket { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeGameOverReceipt? NativeGameOver { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldLoadContext? FieldLoadContext { get; init; }
    public WorldTransition? WorldArrival { get; init; }
    public WorldState? World { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WorldState? DepartingWorldState { get; init; }
    public NativeSavedWorldState? SavedWorld { get; init; }
    [JsonRequired]
    public NativeDiscProcessState? DiscProcess { get; init; }
    public NativeWorldControlState? WorldControls { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldConfiguration? WorldConfiguration { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeExamTimerState? ExamTimer { get; init; }
    public NativePlaytimeState? Playtime { get; init; }
    public NativeMessageVariables? MessageVariables { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeEventRandomState? EventRandom { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeVibrationProcessState? VibrationProcess { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeFieldFadeState? FieldFade { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeShopHistory? ShopHistory { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeWeaponRecipes? WeaponRecipes { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeMenuRandomState? MenuRandom { get; init; }
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
    public NativeLimitProgress? LimitProgress { get; init; }
    public NativePocketStationState? PocketStation { get; init; }
    public NativeGlobalRandomState? GlobalRandom { get; init; }
    public NativeSavedBattleFlags? SavedBattleFlags { get; init; }
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
    [System.Text.Json.Serialization.JsonRequired]
    public NativeWorldEntryMemory? WorldEntryMemory { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeSeeDState? SeeD { get; init; }
    [JsonRequired]
    public NativeEventMemory EventMemory { get; init; } = NativeEventMemory.Empty;
    [JsonIgnore]
    public IReadOnlyDictionary<int, int> Variables => WorldEntryMemory is null ? EventMemory.KnownValues : WorldEntryMemory.ProjectVariables(EventMemory.KnownValues);
    [JsonIgnore]
    public IReadOnlyDictionary<int, NativeMemoryValue> NativeVariableValues => EventMemory.UncertainValues;
    public JsonElement? EventState { get; init; }
    public JsonElement? DepartingFieldState { get; init; }
    public DialogueDraft? DialogueDraft { get; init; }
    public DebugSceneView? DebugView { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? FullBattlePlaythrough { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomEncounterSessionState? RandomEncounters { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleSnapshot? Battle { get; init; }
}

public sealed record DialogueDraft(int Channel, int MessageId, int SelectedChoice, string EnteredName);
public sealed record MoviePlaybackState(int FrameIndex, int TerminalFrame, bool IsPlaying, bool Completed, bool Skipped);
public sealed record FieldTransition(int FieldId, float X, float Y, int Triangle, int Direction, short? NativeExtraWord = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeDiscJumpTransition? DiscJump = null)
{
    public static FieldTransition FromMapJumpOrigin(int fieldId, int triangle)
    {
        // Native521C30 stores both operands as WORDs; X/Y7FFF requests the triangle centroid.
        return new FieldTransition(unchecked((ushort)fieldId), short.MaxValue, short.MaxValue, unchecked((ushort)triangle), 0);
    }
}

public readonly record struct InputFrame(float Horizontal, float Vertical, bool Confirm, bool Cancel, bool Skip, bool Save, int MenuVertical = 0, bool QuickLoad = false, bool SaveMenu = false, bool LoadMenu = false, int MenuHorizontal = 0, int MenuPage = 0, float CameraHorizontal = 0, float SecondaryHorizontal = 0, float SecondaryVertical = 0, bool CardChallenge = false);

public interface IEventServices
{
    void RecordOriginalSalaryPayment()
    {
    }
    bool NativeFieldDebugRequested => false;
    bool CanStoreNativeFieldDebugRequested => false;
    void SetNativeFieldDebugRequested(bool requested) => throw new NotSupportedException("This service cannot retain the native field DEBUG process owner.");
    NativeMovieSource? MovieSource => null;
    bool CanStoreMovieSource => false;
    void SetMovieSource(NativeMovieSource source)
    {
        throw new NotSupportedException("Native prepared movie resource ownership is not implemented by this service.");
    }

    NativeDiscProcessState? DiscProcess => null;
    bool CanStoreDiscProcess => false;
    void SetDiscProcess(NativeDiscProcessState disc)
    {
        throw new NotSupportedException("Native disc process ownership is not implemented by this service.");
    }

    NativeMenuRandomState? MenuRandom => null;
    bool CanStoreMenuRandom => false;
    void SetMenuRandom(NativeMenuRandomState random)
    {
        throw new NotSupportedException("Native menu random process ownership is not implemented by this service.");
    }

    NativeWeaponRecipes? WeaponRecipes => null;
    bool CanStoreWeaponRecipes => false;
    void SetWeaponRecipes(NativeWeaponRecipes recipes)
    {
        throw new NotSupportedException("Native saved weapon recipe ownership is not implemented by this service.");
    }

    NativeShopHistory? ShopHistory => null;
    bool CanStoreShopHistory => false;
    void SetShopHistory(NativeShopHistory history)
    {
        throw new NotSupportedException("Native saved shop history ownership is not implemented by this service.");
    }

    bool CanStoreNativeBattleFieldReturn => false;
    void SetNativeBattleFieldReturn(NativeBattleFieldReturnOwners owners)
    {
        throw new NotSupportedException("This service cannot atomically publish native battle field-return owners.");
    }

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

    NativeEventMemory EventMemory => NativeEventMemory.Empty;
    IReadOnlyDictionary<int, NativeMemoryValue> NativeVariableValues => EventMemory.UncertainValues;
    bool CanStoreEventMemory => false;
    void SetEventMemory(NativeEventMemory memory)
    {
        throw new NotSupportedException("Typed native event memory is not implemented by this event service.");
    }
    NativeExamTimerState? ExamTimer => null;
    NativeCharacterVitals? CharacterVitals => null;
    NativeLimitProgress? LimitProgress => null;
    NativeCharacterAppearance? CharacterAppearance => null;
    NativeCardInventory? CardInventory => null;
    GameplayPreferences GameplayPreferences => new();
    IReadOnlyList<int> RefinedCardIds => Array.Empty<int>();
    void RecordCardRefinement(int cardId)
    {
        throw new NotSupportedException("Card refinement requires a persistent history owner.");
    }
    NativeEconomy? Economy => null;
    NativeShotAvailabilityState? ShotAvailability => null;
    NativeFieldLocation? NativeFieldLocation => null;
    bool CanStoreNativePartyMenuOwners => false;
    void SetNativePartyMenuOwners(NativeCharacterVitals characterVitals, NativeEconomy economy, NativeShotAvailabilityState shots, NativeSalaryPresentation salaryPresentation, NativeExamTimerState? timer = null, NativeFieldLocation? fieldLocation = null, NativeVibrationProcessState? vibrationProcess = null, NativeWorldProcess? worldProcess = null, NativeFieldFadeState? fieldFade = null, NativeShopHistory? shopHistory = null, NativeMenuRandomState? menuRandom = null, NativeWeaponRecipes? weaponRecipes = null, NativeCardInventory? cardInventory = null, NativeLimitProgress? limitProgress = null)
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
    NativeFieldBarsState? FieldBars => null;
    bool CanStoreFieldBars => false;
    bool CanStoreNativeFieldBarAliasOwners => false;
    void SetNativeFieldBarAliasOwners(NativeFieldBarsState bars, NativeVibrationProcessState vibration)
    {
        throw new NotSupportedException("Atomic native bar alias ownership is not implemented by this event service.");
    }
    void SetFieldBars(NativeFieldBarsState state) => throw new NotSupportedException("This service cannot retain native global field bars.");

    NativeWorldEntryMemory? WorldEntryMemory => null;
    bool CanStoreWorldEntryMemory => false;
    void SetWorldEntryMemory(NativeWorldEntryMemory memory)
    {
        throw new NotSupportedException("Native world-entry byte ownership is not implemented by this event service.");
    }
    bool CanStoreNativeEventOwners => false;
    void SetNativeEventOwners(NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null, NativeVibrationProcessState? vibrationProcess = null, NativeFieldFadeState? fieldFade = null, NativeEventMemory? eventMemory = null, NativeInputConfiguration? inputConfiguration = null)
    {
        throw new NotSupportedException("Atomic native event-owner publication is not implemented by this service.");
    }
    NativeWorldConfiguration? WorldConfiguration => null;
    void SetWorldControls(NativeWorldControlState controls)
    {
        throw new NotSupportedException("Native world control flags are not implemented by this event service.");
    }
    SceneKind CurrentScene => SceneKind.Field;
    MoviePlaybackState? CurrentMoviePlayback => null;
    bool IsMoviePlaying { get; }
    bool? NativeMovieClockFlag => null;
    void RequestMovie(int index);
    void RequestField(int fieldId, int entryPoint);
    void RequestField(FieldTransition transition)
    {
        throw new NotSupportedException("Coordinate field transitions are not implemented by this event service.");
    }
    void RequestField(FieldTransition transition, ushort rawDirection)
    {
        RequestField(transition);
    }
    NativeFieldRequestPacket? FieldRequestPacket => null;
    bool CanStoreFieldRequestPacket => false;
    NativeGameOverReceipt? NativeGameOver => null;
    bool CanStoreNativeGameOver => false;
    void SetNativeGameOver(NativeGameOverReceipt? receipt)
    {
        throw new NotSupportedException("Native game-over terminal-loss ownership is not implemented by this event service.");
    }
    void SetFieldRequestPacket(NativeFieldRequestPacket packet)
    {
        throw new NotSupportedException("Native retained field request packet ownership is not implemented by this event service.");
    }
    void RequestTimerExpiredField(NativeFieldRequestPacket expected)
    {
        throw new NotSupportedException("Atomic native timer-expiry field requests are not implemented by this event service.");
    }
    void SetNativeBattleEntryOwners(NativeExamTimerState? timer, NativeFieldRequestPacket packet)
    {
        throw new NotSupportedException("Atomic native battle entry request ownership is not implemented by this event service.");
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
