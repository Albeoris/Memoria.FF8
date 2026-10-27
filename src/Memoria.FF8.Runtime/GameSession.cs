using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession : IEventServices
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private static readonly JsonSerializerOptions SnapshotJsonOptions = CreateSnapshotJsonOptions();
    private static readonly JsonSerializerOptions CompactJsonOptions = new(SnapshotJsonOptions) { WriteIndented = false, Converters = { new ImmutableBytesJsonConverter() } };
    private SceneKind movieReturnScene;
    private bool acceleratingMovie;
    private NativeBattleRetainedMemory? coldBattleMemory;

    private static JsonSerializerOptions CreateSnapshotJsonOptions()
    {
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(type =>
        {
            foreach (JsonPropertyInfo property in type.Properties)
            {
                if (property.AttributeProvider?.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).OfType<JsonIgnoreAttribute>().Any(attribute => attribute.Condition == JsonIgnoreCondition.WhenWritingNull) == true)
                {
                    // Missing means use an initial value on load; an intentional null must therefore be written.
                    property.ShouldSerialize = (_, _) => true;
                }
            }
        });
        return new() { WriteIndented = true, TypeInfoResolver = resolver, Converters = { new JsonStringEnumConverter() } };
    }

    public GameSession(GameState state)
    {
        state = ChocoboRewardTransactions.Reconcile(NormalizeGuardianForceCompatibilityState(state));
        Validate(state);
        State = state;
        movieReturnScene = SceneKind.Field;
    }

    public static GameSession CreateColdProcess(NativeBattleRetainedMemory? battleMemory = null)
    {
        NativeBattleRetainedMemory coldMemory = battleMemory ?? NativeBattleRetainedMemory.FromColdProcess();
        return new GameSession(CreateColdProcessState(coldMemory)) { coldBattleMemory = coldMemory };
    }

    private static GameState CreateColdProcessState(NativeBattleRetainedMemory? battleMemory)
    {
        return new GameState { NativeBattleMemory = battleMemory ?? NativeBattleRetainedMemory.FromColdProcess(), Playtime = NativePlaytimeState.FromColdProcess(), CrtRandom = NativeCrtRandomState.FromColdProcess(), MenuRandom = NativeMenuRandomState.FromColdProcess(), FieldFade = NativeFieldFadeState.FromColdProcess(), VibrationProcess = NativeVibrationProcessState.FromColdProcess(), EventRandom = NativeEventRandomState.FromColdProcess(), ExamTimer = NativeExamTimerState.FromColdProcess(), MessageVariables = NativeMessageVariables.FromColdProcess(), GlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), SalaryPresentation = NativeSalaryPresentation.FromColdProcess(), FieldShake = NativeFieldShake.FromColdProcess(), WorldProcess = NativeWorldProcess.FromColdProcess(), FieldButtonInput = NativeFieldButtonInput.FromColdProcess() };
    }

    private GameState state = null!;
    public GameState State
    {
        get => state;
        private set => state = ChocoboRewardTransactions.Reconcile(value);
    }

    public void BeginMovieAcceleration()
    {
        if (!IsMoviePlaying || acceleratingMovie)
        {
            throw new InvalidOperationException("Movie acceleration requires one active movie owner.");
        }
        acceleratingMovie = true;
    }

    public void EndMovieAcceleration(MovieSkipFrames? frames)
    {
        acceleratingMovie = false;
        frames?.Validate();
    }

    public NativeWorldControlState? WorldControls => State.WorldControls;
    public bool CanStoreNativeEventOwners => true;
    public NativeWorldConfiguration? WorldConfiguration => State.WorldConfiguration;
    public NativeExamTimerState? ExamTimer => State.ExamTimer;
    public NativeCharacterVitals? CharacterVitals => State.CharacterVitals;
    public NativeCharacterAppearance? CharacterAppearance => State.CharacterAppearance;
    public NativeEventMemory EventMemory => State.EventMemory;
    public IReadOnlyDictionary<int, NativeMemoryValue> NativeVariableValues => State.NativeVariableValues;
    public bool CanStoreEventMemory => true;
    public string? Dialog { get; private set; }
    public SceneKind CurrentScene => State.Scene;
    public MoviePlaybackState? CurrentMoviePlayback => State.MoviePlayback;
    public bool IsMoviePlaying => State.Scene == SceneKind.Movie;
    // Accepted RequestMovie models51F390->55A530; CompleteMovie models55A5C0 cleanup.
    public bool? NativeMovieClockFlag => IsMoviePlaying;
    public event Action<GameState>? StateChanged;

    private SnapshotPreparationBinding? snapshotPreparation;
    private bool preparingSnapshot;

    /// <summary>Binds the live allocation's snapshot preparation; disposing an older binding cannot remove its replacement.</summary>
    public IDisposable BindSnapshotPreparation(Func<GameState, GameState> prepare)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        SnapshotPreparationBinding binding = new(this, prepare);
        snapshotPreparation = binding;
        return binding;
    }

    /// <summary>Retains the owning allocation's final snapshot before its live resources are released.</summary>
    public void CompleteSnapshotPreparation(IDisposable binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!ReferenceEquals(snapshotPreparation, binding))
        {
            return;
        }
        try
        {
            State = PrepareSnapshot();
            liveBattle = null;
            acceptedBattleObservation = null;
            liveFieldMemory = null;
        }
        finally
        {
            binding.Dispose();
        }
    }

    /// <summary>Materializes live allocation state for persistence without publishing a gameplay change.</summary>
    public GameState PrepareSnapshot()
    {
        if (preparingSnapshot)
        {
            throw new InvalidOperationException("Snapshot preparation cannot recursively prepare another snapshot.");
        }
        preparingSnapshot = true;
        preparedFieldMemory = null;
        try
        {
            GameState snapshot = snapshotPreparation?.Prepare(State) ?? State;
            snapshot = CaptureLiveFieldMemory(snapshot);
            snapshot = CaptureLiveBattle(snapshot);
            ValidateLiveEventMemoryOwners(snapshot, snapshot.EventMemory);
            return snapshot;
        }
        finally
        {
            preparedFieldMemory = null;
            preparingSnapshot = false;
        }
    }

    private sealed class SnapshotPreparationBinding(GameSession session, Func<GameState, GameState> prepare) : IDisposable
    {
        public GameState Prepare(GameState state) => prepare(state);

        public void Dispose()
        {
            if (ReferenceEquals(session.snapshotPreparation, this))
            {
                session.snapshotPreparation = null;
            }
        }
    }

    public void StartNewGame()
    {
        NativeBattleRetainedMemory? memory = coldBattleMemory;
        if (memory is null && State.NativeBattleMemory?.EffectMemory is { ExecutionStack: not null } previous)
        {
            memory = NativeBattleRetainedMemory.FromColdProcess();
            memory = memory with { EffectMemory = memory.EffectMemory! with { ExecutionStack = previous.ExecutionStack, HeroCrtStack = previous.HeroCrtStack } };
        }
        StartNewGame(memory);
    }

    public void StartNewGame(NativeBattleRetainedMemory? battleMemory)
    {
        GameState cold = CreateColdProcessState(battleMemory);
        ResetTransientGameplayState();
        Change(cold with { BattleVariables = NativeBattleVariableState.CreateNewGame(), DiscProcess = NativeDiscProcessState.CreateNewGame(), FieldFade = cold.FieldFade!.ClearSavedForNewGame(), EventRandom = cold.EventRandom!.ClearSavedStateForNewGame(), WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(), FieldBars = NativeFieldBarsState.CreateHidden(), Scene = SceneKind.Field, FieldId = 74, FieldName = "start0", FieldLoadContext = NativeFieldLoadContext.NewGame(), SavedWorld = NativeSavedWorldState.CreateNewGame(), WorldControls = NativeWorldControlState.CreateNewGame(), CardInventory = NativeCardInventory.CreateNewGame(), SeeD = NativeSeeDState.CreateNewGame(), ExamTimer = cold.ExamTimer!.ClearSavedStateForNewGame() }, advancePlaytime: false);
    }

    public void Restore(GameState state)
    {
        ResetTransientGameplayState();
        Change(NormalizeGuardianForceCompatibilityState(state), advancePlaytime: false);
    }

    private void ResetTransientGameplayState()
    {
        liveFieldMemory = null;
        preparedFieldMemory = null;
        liveBattle = null;
        acceptedBattleObservation = null;
        Dialog = null;
        movieReturnScene = SceneKind.Field;
        acceleratingMovie = false;
        originalAchievementActionBranch = Guid.NewGuid().ToString("N");
    }

    public void RequestMovie(int index)
    {
        if (index < 0 || index > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        movieReturnScene = State.Scene == SceneKind.Movie ? movieReturnScene : State.Scene;
        Change(StageOriginalMovieAchievement(State with { Scene = SceneKind.Movie, MovieIndex = index, MovieSeconds = 0, MoviePlayback = null }, index));
    }

    public void CompleteMovie()
    {
        CompleteMovie(false);
    }

    public void CompleteMovie(bool skipped)
    {
        skipped |= acceleratingMovie;
        if (State.Scene == SceneKind.Movie)
        {
            MovieCompleting?.Invoke();
            MoviePlaybackState? playback = State.MoviePlayback;
            if (playback is not null)
            {
                // Native5304B0 closes playback without seeking past its actual last decoded frame.
                // Story-safe SkipMovie first executes the remaining decoded callbacks in MovieClock;
                // assigning TerminalFrame here would silently discard intervening script events.
                playback = playback with { IsPlaying = false, Completed = true, Skipped = skipped };
            }
            Change(State with { Scene = movieReturnScene, MovieSeconds = 0, MoviePlayback = playback });
        }
    }

    public void RequestField(int fieldId, int entryPoint)
    {
        GameState candidate = PrepareFieldRequest(fieldId, entryPoint);
        Dialog = null;
        Change(candidate);
    }

    private GameState PrepareFieldRequest(int fieldId, int entryPoint)
    {
        if (fieldId is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        }
        if (entryPoint != 0)
        {
            throw new NotSupportedException("Nonzero original field entry points are not implemented. Resume a full recreation checkpoint for an exact state.");
        }
        bool hasFieldSource = State.Scene == SceneKind.Field || State.Scene == SceneKind.Movie && movieReturnScene == SceneKind.Field;
        int? previousFieldId = hasFieldSource ? State.FieldId : null;
        if (previousFieldId is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException("The departing field cannot be represented by the native previous-field WORD.");
        }
        string name = fieldId switch { 74 => "start0", 179 => "bghoke_1", 180 => "bghoke_2", 181 => "bghoke_3", 229 => "bgroad_6", 232 => "bgroom_1", _ => $"field_{fieldId}" };
        return State with { Scene = SceneKind.Field, FieldId = fieldId, PreviousFieldId = previousFieldId, FieldLoadContext = NativeFieldLoadContext.FromFieldRequest(fieldId), FieldName = name, EntryPoint = entryPoint, X = 0, Y = 0, EventState = null, DepartingFieldState = null, DialogueDraft = null, FieldArrival = null, WorldArrival = null, World = null, DebugView = null, WorldControls = State.WorldControls?.WithNormalFieldLoad(), VibrationProcess = State.VibrationProcess?.EndFieldCallback() };
    }

    public void RequestField(FieldTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        RequestField(transition, transition.DiscJump?.NativeFacingWord ?? checked((ushort)transition.Direction));
    }

    public void RequestField(FieldTransition transition, ushort rawDirection)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.DiscJump is NativeDiscJumpTransition discJump)
        {
            discJump.Validate(State.DiscProcess?.SavedDisc204 ?? throw new NotSupportedException("DISCJUMP requires its actual disc process owner."));
        }
        NativeFieldRequestPacket packet = (State.FieldRequestPacket ?? new()).WithFieldRequest(transition, rawDirection);
        GameState candidate = PrepareFieldRequest(transition.FieldId, 0) with { X = transition.X, Y = transition.Y, FieldArrival = transition, FieldRequestPacket = packet };
        Dialog = null;
        Change(candidate);
    }

    public void CommitPreparedWorldFieldEntry(GameState expectedDeparture, WorldState finalWorldState, FieldTransition transition, NativeSavedWorldFootReturn receipt, NativeWorldSalaryState salary, Action<GameState> validatePrepared)
    {
        ArgumentNullException.ThrowIfNull(expectedDeparture);
        ArgumentNullException.ThrowIfNull(finalWorldState);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(validatePrepared);
        receipt.Validate();
        GameState departure = State;
        if (!ReferenceEquals(departure, expectedDeparture) || !ReferenceEquals(departure, salary.Departure))
        {
            throw new InvalidOperationException("The prepared field entry no longer matches the current world session.");
        }
        if (departure.Scene != SceneKind.WorldMap || departure.World is null || departure.WorldArrival is null || finalWorldState.NativeFootRuntime is not NativeFootWorldRuntimeState activeFoot || activeFoot.EntityType is not (0 or 1 or 2) || departure.SavedWorld is null || departure.WorldControls is null)
        {
            throw new NotSupportedException("Native world-to-field entry requires a known normal foot-world departure and saved/control provenance.");
        }
        Validate(departure with { World = finalWorldState, WorldProcess = PrepareWorldProcess(departure, finalWorldState) });
        if (transition.FieldId is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(transition), "Native world-to-field destination must fit its unsigned WORD.");
        }
        if (receipt.LastFieldId != departure.FieldId || receipt.SourceWorldFrame != finalWorldState.Frame || receipt.Pose0 != new NativeSavedWorldPose(finalWorldState.X, finalWorldState.Z, checked((short)finalWorldState.NativeHeight), checked((ushort)finalWorldState.BodyYaw)) || receipt.CameraYaw96 != finalWorldState.CameraYaw || receipt.StoryWord != departure.Variables.GetValueOrDefault(256) || receipt.EntityType105 != activeFoot.EntityType)
        {
            throw new InvalidOperationException("The prepared foot save no longer matches the current native world departure.");
        }
        string name = transition.FieldId switch { 106 => "bcgate_1", 136 => "bdview1", 159 => "bggate_1", _ => $"field_{transition.FieldId}" };
        GameState candidate = departure with
        {
            Scene = SceneKind.Field,
            FieldId = transition.FieldId,
            FieldLoadContext = NativeFieldLoadContext.FromWorldReturn(transition.FieldId),
            PreviousFieldId = receipt.LastFieldId,
            FieldName = name,
            EntryPoint = 0,
            X = transition.X,
            Y = transition.Y,
            EventState = null,
            DepartingFieldState = departure.DepartingFieldState ?? departure.EventState,
            DialogueDraft = null,
            FieldArrival = transition,
            FieldRequestPacket = (departure.FieldRequestPacket ?? new()).WithWorldReturn(transition),
            WorldArrival = null,
            World = null,
            DepartingWorldState = finalWorldState,
            DebugView = null,
            SavedWorld = PrepareGardenParkingForFootReturn(departure.SavedWorld, finalWorldState).WithFootReturn(receipt),
            WorldControls = departure.WorldControls.WithNormalFieldLoad(),
            WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(finalWorldState, departure.FieldId),
            ExamTimer = AdvanceWorldTimer(departure, finalWorldState),
            WorldProcess = PrepareWorldProcess(departure, finalWorldState),
            SeeD = salary.SeeD
        };
        candidate = salary.ApplyTo(candidate, finalWorldState);
        candidate = candidate with { SavedWorld = PrepareOrdinaryWorldFieldEntry(candidate.SavedWorld!) };
        candidate = StageOriginalAchievementNativeMutations(candidate, salary.Payments > 0);
        candidate = AdvanceNativePlaytime(candidate);
        Validate(candidate);
        validatePrepared(candidate);
        if (!ReferenceEquals(State, departure))
        {
            throw new InvalidOperationException("The world session changed during prepared field-entry validation.");
        }
        Dialog = null;
        Change(candidate);
    }

    public void SetMoviePlayback(MoviePlaybackState playback)
    {
        State = State with { MoviePlayback = playback };
    }

    public void RequestWorld(WorldTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        Dialog = null;
        Change(State with { Scene = SceneKind.WorldMap, WorldArrival = transition, World = null, FieldArrival = null, DialogueDraft = null, DebugView = null, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(transition, State.FieldId), FieldRequestPacket = (State.FieldRequestPacket ?? new()).WithWorldRequest(transition).WithConsumedRequest() });
    }

    /// <summary>Commits live world owners. The field action must finish and capture its VM mirror before a standalone save, as with other native owner writes.</summary>
    public void CommitPreparedWorldEntry(WorldTransition transition, WorldState initialState, NativeWorldControlState expectedControls, Action<GameState> validatePrepared, NativeSavedWorldState? preparedSavedWorld = null, NativeEventMemory? preparedEventMemory = null)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(expectedControls);
        ArgumentNullException.ThrowIfNull(validatePrepared);
        expectedControls.Validate();
        GameState departure = State;
        if (departure.Scene != SceneKind.Field || departure.WorldControls != expectedControls)
        {
            throw new InvalidOperationException("The prepared world entry no longer matches its normal field departure controls.");
        }
        NativeSavedWorldState? saved = preparedSavedWorld ?? departure.SavedWorld;
        NativeSavedWorldState? expectedSaved = initialState.NativeCarRuntime is null ? departure.SavedWorld : departure.SavedWorld?.WithOrdinaryCarEntry(initialState.NativeCarRuntime.EntityType);
        if (saved != expectedSaved)
        {
            throw new InvalidOperationException("Prepared world saved-vehicle state differs from its native entry writes.");
        }
        NativeEventMemory memory = preparedEventMemory ?? departure.EventMemory;
        NativeWorldAuxiliaryRelocationReceipt? relocation = initialState.NativeGardenRuntime?.AuxiliaryEntry ?? initialState.NativeRagnarokRuntime?.Allocation.AuxiliaryEntry;
        NativeEventMemory expectedMemory = relocation?.ApplyTo(departure.EventMemory) ?? departure.EventMemory;
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(memory), JsonSerializer.SerializeToElement(expectedMemory)))
        {
            throw new InvalidOperationException("Prepared world event memory differs from its authenticated native selector consumption.");
        }
        GameState candidate = departure with { Scene = SceneKind.WorldMap, WorldArrival = transition, World = initialState, FieldArrival = null, FieldRequestPacket = (departure.FieldRequestPacket ?? new()).WithWorldRequest(transition).WithConsumedRequest(), DialogueDraft = null, DebugView = null, WorldControls = expectedControls.WithWorldEntryMenuFlag(), SavedWorld = saved, EventMemory = memory, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(transition, departure.FieldId), VibrationProcess = departure.VibrationProcess is NativeVibrationProcessState queue ? queue.EndFieldCallback() with { GraphicsSpeed20409F0 = 0 } : null };
        candidate = AdvanceNativePlaytime(candidate);
        Validate(candidate);
        validatePrepared(candidate);
        if (!ReferenceEquals(State, departure))
        {
            throw new InvalidOperationException("The field session changed during prepared world-entry validation.");
        }
        Dialog = null;
        Change(candidate);
    }

    public void SetWorldState(WorldState state)
    {
        State = State with { World = state };
    }

    public void CommitWorldAction(WorldState state, NativeWorldSalaryState salary)
    {
        ArgumentNullException.ThrowIfNull(state);
        GameState departure = State;
        if (departure.Scene != SceneKind.WorldMap || departure.World is null || !ReferenceEquals(departure, salary.Departure))
        {
            throw new InvalidOperationException("A world action requires its current native world session.");
        }
        GameState candidate = salary.ApplyTo(departure with { World = state, SavedWorld = PrepareScriptedWorldSave(departure, state), WorldConfiguration = PrepareScriptedWorldConfiguration(departure, state), ExamTimer = AdvanceWorldTimer(departure, state), WorldProcess = PrepareWorldProcess(departure, state) }, state);
        candidate = StageOriginalAchievementNativeMutations(candidate, salary.Payments > 0);
        candidate = AdvanceNativePlaytime(candidate);
        Validate(candidate);
        State = candidate;
    }

    private static NativeExamTimerState? AdvanceWorldTimer(GameState departure, WorldState finalWorld)
    {
        int callbacks = checked((int)(finalWorld.Frame - departure.World!.Frame));
        // 53F0F0 calls 4701B0 twice per admitted world callback, even after KILLTIMER hides the display.
        return departure.ExamTimer?.AdvanceCallbacks(NativeTimerCallbackKind.World, callbacks);
    }

    public void SetDepartingWorldState(WorldState? state)
    {
        State = State with { DepartingWorldState = state };
    }

    public void SetWorldControls(NativeWorldControlState controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        controls.Validate();
        ValidateLiveEventMemoryOwners(State with { WorldControls = controls }, State.EventMemory);
        State = State with { WorldControls = controls };
    }

    public void SetNativeEventOwners(NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null, NativeVibrationProcessState? vibrationProcess = null, NativeFieldFadeState? fieldFade = null, NativeEventMemory? eventMemory = null, NativeInputConfiguration? inputConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(controls);
        controls.Validate();
        seeD?.Validate();
        timer?.Validate();
        characterVitals?.Validate();
        economy?.Validate();
        globalSoundProcess?.Validate();
        salaryPresentation?.Validate();
        eventRandom?.Validate();
        vibrationProcess?.Validate();
        fieldFade?.Validate();
        inputConfiguration?.Validate();
        NativeEventMemory nextMemory = eventMemory ?? State.EventMemory;
        if (eventRandom is { DrawPoints: null } && State.EventRandom?.DrawPoints is not null)
        {
            throw new InvalidDataException("Atomic native event publication cannot erase the already initialized draw-point bank.");
        }
        if ((eventRandom ?? State.EventRandom)?.DrawPoints is not null)
        {
            ValidateEventRandomAliases(nextMemory);
        }
        if (seeD is not null || State.SeeD is not null)
        {
            ValidateSeeDAliases(nextMemory);
        }
        if (characterVitals is not null || State.CharacterVitals is not null)
        {
            ValidateJunctionPartyBackupAliases(nextMemory);
        }
        NativeCharacterVitals? nextVitals = characterVitals is null ? State.CharacterVitals : characterVitals.DeepCopy();
        NativeEconomy? nextEconomy = economy is null ? State.Economy : economy.DeepCopy();
        if (nextEconomy is not null && nextVitals is not null && nextEconomy.InitOutSha256 != nextVitals.InitOutSha256)
        {
            throw new InvalidDataException("Native economy and character owners have different initialization provenance.");
        }
        GameState candidate = State with { WorldControls = controls, SeeD = seeD ?? State.SeeD, ExamTimer = timer ?? State.ExamTimer, CharacterVitals = nextVitals, SavedBattleFlags = SynchronizeSavedBattleFlags(nextVitals), Economy = nextEconomy, GlobalSoundProcess = globalSoundProcess ?? State.GlobalSoundProcess, SalaryPresentation = salaryPresentation ?? State.SalaryPresentation, EventRandom = eventRandom ?? State.EventRandom, VibrationProcess = vibrationProcess ?? State.VibrationProcess, FieldFade = fieldFade ?? State.FieldFade, EventMemory = nextMemory, InputConfiguration = inputConfiguration ?? State.InputConfiguration };
        candidate = StageOriginalAchievementNativeMutations(candidate);
        candidate = AdvanceNativePlaytime(candidate);
        ValidateLiveEventMemoryOwners(candidate, nextMemory);
        State = candidate;
    }

    public void SetExamTimer(NativeExamTimerState timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        timer.Validate();
        ValidateLiveEventMemoryOwners(State with { ExamTimer = timer }, State.EventMemory);
        State = AdvanceNativePlaytime(State with { ExamTimer = timer });
    }

    public void InitializeNewGameCharacterVitals(NativeCharacterVitals vitals)
    {
        if (State.CharacterVitals is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Character initialization requires the untouched authentic New Game before its first engine transaction.");
        }
        SetCharacterVitals(MagicModeCoordinator.Preview(vitals, State.GameplayPreferences).Vitals);
    }

    public void SetCharacterVitals(NativeCharacterVitals vitals)
    {
        vitals = vitals.NormalizeGuardianForceCompatibility();
        vitals.Validate();
        ValidateLiveEventMemoryOwners(State with { CharacterVitals = vitals }, State.EventMemory);
        State = State with { CharacterVitals = vitals.DeepCopy(), SavedBattleFlags = SynchronizeSavedBattleFlags(vitals) };
    }

    public void InitializeNewGameCharacterAppearance(NativeCharacterAppearance appearance)
    {
        if (State.CharacterAppearance is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Appearance initialization requires the untouched authentic New Game before its first engine transaction.");
        }
        SetCharacterAppearance(appearance);
    }

    public void SetCharacterAppearance(NativeCharacterAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        appearance.Validate();
        State = State with { CharacterAppearance = appearance with { Records = appearance.Records.ToArray() } };
    }

    public void SetEventMemory(NativeEventMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ValidateLiveEventMemoryOwners(State, memory);
        State = State with { EventMemory = memory };
    }

    public void ShowDialog(string text)
    {
        Dialog = text;
    }

    public void DismissDialog()
    {
        Dialog = null;
    }

    public void SetPosition(float x, float y)
    {
        State = State with { X = x, Y = y };
    }

    public void SetMoviePosition(double seconds)
    {
        State = State with { MovieSeconds = Math.Max(0, seconds) };
    }

    public void Save(string path)
    {
        Save(path, compact: false);
    }

    public void Save(string path, bool compact, Action<GameState>? validateContinuation = null)
    {
        GameState snapshot = PrepareSnapshot();
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (compact)
                {
                    SnapshotStorage.Write(stream, snapshot, CompactJsonOptions);
                }
                else
                {
                    JsonSerializer.Serialize(stream, snapshot, SnapshotJsonOptions);
                }
                stream.Flush(true);
            }
            GameState candidate = Load(temporary);
            validateContinuation?.Invoke(candidate);
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public void SetEventState(JsonElement? state)
    {
        GameState candidate = State with { EventState = state?.Clone() };
        ValidateLiveEventMemoryOwners(candidate, candidate.EventMemory);
        State = candidate;
    }

    public void SetDepartingFieldState(JsonElement? state)
    {
        GameState candidate = State with { DepartingFieldState = state?.Clone() };
        ValidateLiveEventMemoryOwners(candidate, candidate.EventMemory);
        State = candidate;
    }

    public void SetDialogueDraft(DialogueDraft? draft)
    {
        State = State with { DialogueDraft = draft };
    }

    public void SetDebugView(DebugSceneView? view)
    {
        view?.Validate();
        if (view is not null && view.FieldId != State.FieldId)
        {
            throw new InvalidDataException("Debug view belongs to another field.");
        }
        State = State with { DebugView = view };
    }

    public static GameState Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        GameState state = NormalizeGuardianForceCompatibilityState(SnapshotStorage.Read(stream, CompactJsonOptions));
        Validate(state);
        JsonElement? currentVm = state.Scene == SceneKind.WorldMap ? state.DepartingFieldState ?? state.EventState : state.EventState;
        if (currentVm is null && state.Scene == SceneKind.Field && state.DepartingFieldState is JsonElement pendingField && pendingField.TryGetProperty("TransitionRequested", out JsonElement requested) && requested.ValueKind == JsonValueKind.True && pendingField.TryGetProperty("Scene", out JsonElement pendingScene) && pendingScene.TryGetProperty("WorldTransitionRequest", out JsonElement worldRequest) && worldRequest.ValueKind == JsonValueKind.Null)
        {
            // A pending ordinary field reload owns the current global state even when source and destination IDs agree.
            currentVm = pendingField;
        }
        if (state.WorldControls is not null && currentVm is JsonElement vm && vm.TryGetProperty("WorldControls", out JsonElement savedControls) && savedControls.ValueKind != JsonValueKind.Null)
        {
            NativeWorldControlState controls = savedControls.Deserialize<NativeWorldControlState>(JsonOptions) ?? throw new InvalidDataException("Empty event world-control state.");
            controls.Validate();
            if (controls != state.WorldControls)
            {
                throw new InvalidDataException("Root and event world-control checkpoints disagree.");
            }
        }
        ValidateAllocatedWorldEntityCheckpoint(state, currentVm);
        ValidateCardInventoryCheckpoint(state, currentVm);
        ValidateEconomyCheckpoint(state, currentVm);
        ValidatePresentationProcessCheckpoint(state, currentVm);
        ValidateFieldShakeCheckpoint(state, currentVm);
        ValidateFieldBarsCheckpoint(state, currentVm);
        ValidateEventRandomCheckpoint(state, currentVm);
        ValidateVibrationCheckpoint(state, currentVm);
        ValidateFieldFadeCheckpoint(state, currentVm);
        ValidateShopHistoryCheckpoint(state, currentVm);
        ValidateMenuRandomCheckpoint(state, currentVm);
        ValidateDiscCheckpoint(state, currentVm);
        ValidateMovieSourceCheckpoint(state, currentVm);
        ValidateWeaponRecipesCheckpoint(state, currentVm);
        ValidateWorldEntryMemoryCheckpoint(state, currentVm);
        if (currentVm is JsonElement vitalsVm && vitalsVm.TryGetProperty("CharacterVitals", out JsonElement vitalsJson) && vitalsJson.ValueKind != JsonValueKind.Null)
        {
            NativeCharacterVitals vitals = vitalsJson.Deserialize<NativeCharacterVitals>(JsonOptions) ?? throw new InvalidDataException("Empty event character vitals.");
            vitals.Validate();
            if (state.CharacterVitals is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.CharacterVitals), JsonSerializer.SerializeToElement(vitals)))
            {
                throw new InvalidDataException("Root and event character vitals checkpoints disagree.");
            }
        }
        else if (currentVm is not null && state.CharacterVitals is not null)
        {
            throw new InvalidDataException("Root character vitals require matching history in the active event checkpoint.");
        }
        if (currentVm is JsonElement appearanceVm && appearanceVm.TryGetProperty("CharacterAppearance", out JsonElement appearanceJson) && appearanceJson.ValueKind != JsonValueKind.Null)
        {
            NativeCharacterAppearance appearance = appearanceJson.Deserialize<NativeCharacterAppearance>(JsonOptions) ?? throw new InvalidDataException("Empty event character appearance.");
            appearance.Validate();
            if (state.CharacterAppearance is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.CharacterAppearance), JsonSerializer.SerializeToElement(appearance)))
            {
                throw new InvalidDataException("Root and event character appearance checkpoints disagree.");
            }
        }
        else if (currentVm is not null && state.CharacterAppearance is not null)
        {
            throw new InvalidDataException("Root character appearance requires matching history in the active event checkpoint.");
        }
        if (currentVm is JsonElement timerVm && timerVm.TryGetProperty("ExamTimer", out JsonElement timerJson) && timerJson.ValueKind != JsonValueKind.Null)
        {
            NativeExamTimerState timer = timerJson.Deserialize<NativeExamTimerState>(JsonOptions) ?? throw new InvalidDataException("Empty event exam timer state.");
            timer.Validate();
            if (state.Scene == SceneKind.WorldMap && state.ExamTimer is NativeExamTimerState sceneTimer && sceneTimer.ClockCalls >= timer.ClockCalls)
            {
                long calls = sceneTimer.ClockCalls - timer.ClockCalls;
                long callbacks = calls / 2;
                if (calls % 2 != 0 || callbacks > (state.World?.Frame ?? 0))
                {
                    throw new InvalidDataException("Frozen field timer has no matching elapsed world callback count.");
                }
                while (callbacks != 0)
                {
                    int batch = (int)Math.Min(callbacks, 30000);
                    timer = timer.AdvanceCallbacks(NativeTimerCallbackKind.World, batch);
                    callbacks -= batch;
                }
            }
            if (state.ExamTimer is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.ExamTimer), JsonSerializer.SerializeToElement(timer)))
            {
                throw new InvalidDataException("Root and event exam timer checkpoints disagree.");
            }
        }
        if (currentVm is JsonElement memoryVm)
        {
            if (!memoryVm.TryGetProperty("EventMemory", out JsonElement memoryJson) || memoryJson.ValueKind == JsonValueKind.Null)
            {
                throw new InvalidDataException("Saved VM lacks its required typed event memory; migrate this save explicitly.");
            }
            NativeEventMemory memory = memoryJson.Deserialize<NativeEventMemory>(JsonOptions) ?? throw new InvalidDataException("Empty event memory state.");
            ValidateEventMemoryOwners(state, memory);
            if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.EventMemory), JsonSerializer.SerializeToElement(memory)))
            {
                throw new InvalidDataException("Root and event native memory checkpoints disagree.");
            }
        }
        ValidateSeeDCheckpoint(state, currentVm);
        ValidateMessageVariableCheckpoint(state, currentVm);
        ValidateInputConfigurationCheckpoint(state, currentVm);
        ValidatePartyMenuOwnersCheckpoint(state, currentVm);
        ValidateFieldRequestPacketCheckpoint(state, state.Scene == SceneKind.WorldMap ? null : state.EventState);
        ValidateBattleFactsCheckpoint(state);
        return ChocoboRewardTransactions.Reconcile(state);
    }

    /// <summary>Reads only the display header of a compact save; legacy JSON has no header.</summary>
    public static SaveSummary? ReadSummary(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SnapshotStorage.ReadSummary(stream, CompactJsonOptions);
    }

    private void Change(GameState state, bool advancePlaytime = true)
    {
        if (advancePlaytime)
        {
            state = AdvanceNativePlaytime(state);
        }
        Validate(state);
        State = state;
        StateChanged?.Invoke(state);
    }

    private static void Validate(GameState state)
    {
        state.OriginalSave?.Validate();
        state.FieldLoadContext?.Validate();
        state.FieldRequestPacket?.Validate();
        if (state.Scene == SceneKind.Field && state.FieldLoadContext is { } loadContext && loadContext.FieldId != state.FieldId)
        {
            throw new InvalidDataException("The saved native field allocation request has a different destination.");
        }
        if (state.GameplayPreferences is null)
        {
            throw new InvalidDataException("Saved gameplay preferences cannot be null.");
        }
        state.GameplayPreferences.Validate();
        if (state.Battle is { Definition: { } drawDefinition } && drawDefinition.DrawOncePerSpell != state.GameplayPreferences.DrawOncePerSpell)
        {
            throw new InvalidDataException("Saved battle Draw once per spell option differs from its save preference.");
        }
        if ((state.Battle is not null || state.PendingBattleEncounter is not null) && state.PendingGameplayPreferences is { } pendingDrawPreferences && pendingDrawPreferences.DrawOncePerSpell != state.GameplayPreferences.DrawOncePerSpell)
        {
            throw new InvalidDataException("Saved pending Draw once per spell preference cannot change during a battle.");
        }
        state.PendingGameplayPreferences?.Validate();
        if (state.Battle is { Definition: { } limitDefinition } && limitDefinition.LimitOncePerTurn != state.GameplayPreferences.LimitOncePerTurn)
        {
            throw new InvalidDataException("Battle Limit policy differs from the saved gameplay preference.");
        }
        if ((state.Battle is not null || state.PendingBattleEncounter is not null) && state.PendingGameplayPreferences is { } pendingLimitPreferences && pendingLimitPreferences.LimitOncePerTurn != state.GameplayPreferences.LimitOncePerTurn)
        {
            throw new InvalidDataException("Pending Limit policy cannot change during battle.");
        }
        if (state.Battle is { Definition: { } slotsDefinition } && slotsDefinition.SelphieCardSlots != state.GameplayPreferences.SelphieCardSlots)
        {
            throw new InvalidDataException("Battle Selphie card Slots policy differs from the saved gameplay preference.");
        }
        if ((state.Battle is not null || state.PendingBattleEncounter is not null) && state.PendingGameplayPreferences is { } pendingSlotsPreferences && pendingSlotsPreferences.SelphieCardSlots != state.GameplayPreferences.SelphieCardSlots)
        {
            throw new InvalidDataException("Pending Selphie card Slots policy cannot change during battle.");
        }
        if (state.Battle is { Definition: { } battleDefinition } && battleDefinition.GuardianFormsEnabled != state.GameplayPreferences.GuardianFormsEnabled)
        {
            throw new InvalidDataException("Saved battle GF Transformations option differs from its save preference.");
        }
        if ((state.Battle is not null || state.PendingBattleEncounter is not null) && state.PendingGameplayPreferences is { } pendingPreferences && pendingPreferences.GuardianFormsEnabled != state.GameplayPreferences.GuardianFormsEnabled)
        {
            throw new InvalidDataException("Saved pending GF Transformations preference cannot change during a battle.");
        }
        state.CharacterVitals?.ValidateMagicSystemConsistency();
        if (state.CharacterVitals is { } magicVitals && (magicVitals.MagicSystem.SharedMagicStock != state.GameplayPreferences.SharedMagicStock || magicVitals.MagicSystem.InfiniteMagic != state.GameplayPreferences.InfiniteMagic || magicVitals.MagicSystem.SmartAttack != state.GameplayPreferences.SmartAttack))
        {
            throw new InvalidDataException("Saved gameplay rules disagree with their magic-system owner.");
        }
        ValidateEntityNames(state);
        ValidateTripleTriadCheckpoint(state);
        ValidateNativePlaytime(state);
        ValidateBattleFactsCheckpoint(state);
        if (state.Battle is not null)
        {
            _ = BattleSession.Restore(state.Battle);
            if (state.Scene is not (SceneKind.Field or SceneKind.WorldMap))
            {
                throw new InvalidDataException("Saved interactive combat requires its field or world-map scene.");
            }
        }
        if (state.PreviousFieldId is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Invalid native previous-field provenance.");
        }
        state.SavedWorld?.Validate();
        if (state.SavedWorld is not null && (state.Variables.Keys.Any(address => address < 1396 && (long)address + 4 > 1392) || state.NativeVariableValues?.Any(pair => pair.Key < 1396 && (long)pair.Key + pair.Value.Width > 1392) == true))
        {
            throw new InvalidDataException("Saved-world fuel DWORD112 overlaps a separately owned event-memory alias.");
        }
        state.WorldControls?.Validate();
        state.WorldConfiguration?.Validate();
        state.ExamTimer?.Validate();
        state.SeeD?.Validate();
        if (state.SeeD is not null)
        {
            ValidateSeeDAliases(state.EventMemory);
        }
        state.CharacterVitals?.Validate();
        if (state.CharacterVitals is not null)
        {
            ValidateJunctionPartyBackupAliases(state.EventMemory);
        }
        state.CharacterAppearance?.Validate();
        state.CardInventory?.Validate();
        state.Economy?.Validate();
        state.GlobalSoundProcess?.Validate();
        state.WorldProcess?.Validate();
        ValidateDiscCheckpoint(state, null);
        ValidateMovieSourceCheckpoint(state, null);
        state.SalaryPresentation?.Validate();
        state.FieldShake?.Validate();
        state.FieldBars?.Validate();
        state.EventRandom?.Validate();
        state.VibrationProcess?.Validate();
        state.FieldFade?.Validate();
        state.ShopHistory?.Validate();
        state.MenuRandom?.Validate();
        state.WeaponRecipes?.Validate();
        if (state.EventRandom?.DrawPoints is not null)
        {
            ValidateEventRandomAliases(state.EventMemory);
        }
        state.InputConfiguration?.Validate();
        state.PocketStation?.Validate();
        state.FieldButtonInput?.Validate();
        if (state.Economy is not null)
        {
            ValidateEconomyAliases(state.EventMemory);
            if (state.CharacterVitals is not null && state.Economy.InitOutSha256 != state.CharacterVitals.InitOutSha256)
            {
                throw new InvalidDataException("Native economy and character state have different New Game template identities.");
            }
        }
        if (state.CharacterAppearance is not null && state.CharacterVitals is not null && state.CharacterAppearance.InitOutSha256 != state.CharacterVitals.InitOutSha256)
        {
            throw new InvalidDataException("Native appearance and character vitals have different New Game template identities.");
        }
        ValidateEventMemoryOwners(state, state.EventMemory);
        state.DebugView?.Validate();
        if (state.DebugView is not null && state.DebugView.FieldId != state.FieldId)
        {
            throw new InvalidDataException("Debug view belongs to another field.");
        }
        if (state.EntryPoint != 0)
        {
            throw new NotSupportedException("Nonzero original field entry points are not implemented; use a full recreation checkpoint.");
        }
        if (state.SnapshotSchemaVersion != GameState.CurrentSnapshotSchemaVersion || state.Version != 1 || !Enum.IsDefined(state.Scene) || state.FieldId < 0 || !float.IsFinite(state.X) || !float.IsFinite(state.Y) || !double.IsFinite(state.MovieSeconds) || state.MovieSeconds < 0)
        {
            throw new InvalidDataException("Unsupported or invalid recreation checkpoint.");
        }
        if (state.FieldArrival is FieldTransition arrival && (arrival.FieldId != state.FieldId || !float.IsFinite(arrival.X) || !float.IsFinite(arrival.Y) || arrival.Triangle < 0 || arrival.Direction is < 0 or > 255))
        {
            throw new InvalidDataException("Invalid coordinate field arrival.");
        }
        state.FieldArrival?.DiscJump?.Validate(state.DiscProcess?.SavedDisc204 ?? throw new InvalidDataException("DISCJUMP arrival has no native disc owner."));
        if (state.MoviePlayback is MoviePlaybackState playback && (playback.TerminalFrame < 0 || playback.FrameIndex < 0 || playback.FrameIndex > playback.TerminalFrame || playback.Completed && playback.IsPlaying))
        {
            throw new InvalidDataException("Invalid movie playback clock.");
        }
        if (state.Scene == SceneKind.WorldMap && state.WorldArrival is null)
        {
            throw new InvalidDataException("A world scene requires its original transition packet.");
        }
        if (state.World is WorldState world)
        {
            world.EventUi?.Validate();
            ValidateWorldState(world);
            if (world.NativeFootRuntime?.Forest is NativeWorldForestState forest && (state.WorldProcess is null || forest.CameraCooldown != state.WorldProcess.CameraCooldown))
            {
                throw new InvalidDataException("Active native foot forest cooldown disagrees with the world process authority.");
            }
            bool battleFootReload = world.NativeFootRuntime is { Rule: NativeFootWorldRuntimeState.BattleReloadRule } battleFoot && ValidateWorldBattleFootReload(state, world, battleFoot);
            if (world.NativeFootRuntime is NativeFootWorldRuntimeState foot && (state.Scene != SceneKind.WorldMap || state.WorldArrival is null || (foot.StationTrains is { } stationSource ? state.WorldArrival != stationSource.Packet || state.SavedWorld is null || state.DepartingWorldState is null || stationSource.Load != NativeTrainWorldLoadReceipt.Create(foot.StationTrainEntrySavedWorld ?? throw new NotSupportedException("Station foot requires its retained train-entry saved owner."), state.DepartingWorldState, stationSource.Load.Allocation) : !battleFootReload && unchecked((byte)state.WorldArrival.Mode) != foot.EntityType && !(unchecked((byte)state.WorldArrival.Mode) == 48 && foot.Rule == NativeFootWorldRuntimeState.GardenDismountRule && foot.GardenDismount is not null && world.NativeGardenRuntime?.Allocation.Packet == state.WorldArrival) && !(unchecked((byte)state.WorldArrival.Mode) == 50 && foot.Rule == NativeFootWorldRuntimeState.RagnarokDismountRule && foot.RagnarokDismount is not null && world.NativeRagnarokRuntime?.Allocation.Packet == state.WorldArrival))))
            {
                throw new InvalidDataException("Native foot world state disagrees with its original transition entity type.");
            }
            if (world.NativeGardenRuntime is NativeGardenWorldRuntimeState garden)
            {
                if (state.Scene != SceneKind.WorldMap || state.WorldArrival != garden.Allocation.Packet || state.WorldProcess is null)
                {
                    throw new InvalidDataException("Native Garden world state lost its original allocation packet or retained process owner.");
                }
                garden.Validate(state.WorldProcess);
                world.NativeFootRuntime?.GardenDismount?.Validate(garden, state.WorldProcess);
            }
            if (world.NativeRagnarokRuntime is { } ragnarok && (state.Scene != SceneKind.WorldMap || state.WorldArrival != ragnarok.Allocation.Packet || state.WorldProcess is null || state.WorldProcess.CarCameraFollowSpeed != ragnarok.Motor.CameraFollowSpeed))
            {
                throw new InvalidDataException("Ragnarok scene lost its original allocation packet or shared process camera-follow owner.");
            }
            if (world.NativeFootRuntime?.RagnarokDismount is { } ragnarokDismount)
            {
                ragnarokDismount.Validate(world.NativeRagnarokRuntime ?? throw new InvalidDataException("Ragnarok foot lost its actual parked allocation."), state.WorldProcess);
            }
            if (world.NativeCarRuntime is { } car && (state.Scene != SceneKind.WorldMap || state.WorldArrival is null || unchecked((byte)state.WorldArrival.Mode) != car.EntityType || state.SavedWorld?.Slot4.ShapeId != car.ShapeId))
            {
                throw new InvalidDataException("Native car world state disagrees with its original transition or active saved vehicle.");
            }
            if (world.NativeCarRuntime is { } carCamera && state.WorldProcess?.CarCameraFollowSpeed != carCamera.CameraFollowSpeed)
            {
                throw new InvalidDataException("Native car camera follow speed differs from its retained process WORD204DAE4.");
            }
            if (world.NativeTrainRuntime is NativeTrainWorldRuntimeState train && (state.Scene != SceneKind.WorldMap || state.WorldArrival != train.Packet || state.SavedWorld is null || state.DepartingWorldState is null || train.Load != NativeTrainWorldLoadReceipt.Create(state.SavedWorld, state.DepartingWorldState, train.Load.Allocation)))
            {
                throw new InvalidDataException("Native train ride lost its actual packet or preceding physical body allocation.");
            }
            if (world.NativeScriptedRuntime is NativeScriptedWorldRuntimeState scripted && (state.Scene != SceneKind.WorldMap || state.WorldArrival != scripted.Path.Packet || scripted.Return is null && (state.SavedWorld is null || state.DepartingWorldState is null || scripted.Load != NativeScriptedWorldLoadReceipt.Create(state.SavedWorld, state.DepartingWorldState, scripted.Load.FootRecord0)) || scripted.Return is not null && (state.SavedWorld?.ScriptedReturn != scripted.Return || scripted.Return.LastFieldId != state.FieldId || scripted.Return.StoryWord != state.Variables.GetValueOrDefault(256))))
            {
                throw new InvalidDataException("Native scripted world state disagrees with its original packet or retained world/object record.");
            }
        }
        if (state.DepartingWorldState is WorldState departingWorld)
        {
            ValidateWorldState(departingWorld);
        }
    }

    private static void ValidateWorldState(WorldState world)
    {
        world.NativeFootRuntime?.Validate();
        world.NativeCarRuntime?.Validate();
        world.NativeScriptedRuntime?.Validate();
        world.NativeTrainRuntime?.Validate();
        world.NativeGardenRuntime?.Validate();
        world.NativeRagnarokRuntime?.RequireSceneState();
        if (world.NativeRagnarokRuntime is { } ragnarokWorld)
        {
            NativeFootWorldRuntimeState? foot = world.NativeFootRuntime;
            NativeRagnarokWorldAllocation allocation = ragnarokWorld.Allocation;
            if (foot is not null && foot.MapDisplayMode != ragnarokWorld.MapDisplayMode)
            {
                throw new InvalidDataException("Parked Ragnarok and its controlled foot must share the current world map display mode.");
            }
            bool ordinaryParkedReload = foot?.Rule is NativeFootWorldRuntimeState.FirstLoadRule or NativeFootWorldRuntimeState.SavedReloadRule && allocation.BattleReload is null && unchecked((byte)allocation.Packet.Mode) == 0;
            bool battleParkedReload = foot is { Rule: NativeFootWorldRuntimeState.BattleReloadRule } && allocation.BattleReload is { WorldMode: 3, EntityType105: 0 } battleReload && foot.ReloadSourceWorldFrame == battleReload.SourceWorldFrame && foot.EntityType == battleReload.EntityType105 && foot.AnimationVariant == battleReload.AnimationVariant108;
            bool parkedReload = (ordinaryParkedReload || battleParkedReload) && allocation.ControlledEntityType == 0 && allocation.ParkedRagnarokPose2 is not null;
            if (world.NativeFootRuntime is not null && world.NativeFootRuntime.RagnarokDismount is null && !parkedReload || world.NativeCarRuntime is not null || world.NativeGardenRuntime is not null || world.NativeTrainRuntime is not null || world.NativeScriptedRuntime is not null || world.GroundType is null)
            {
                throw new InvalidDataException("Ragnarok requires its sole current physical allocation and accepted ground owner.");
            }
            NativeRagnarokWorldObject controlled = ragnarokWorld.Objects[world.NativeFootRuntime is null ? ragnarokWorld.Allocation.RagnarokIndex : 0];
            if (controlled.Position != new NativeWorldPathPosition(world.X, world.NativeHeight, -world.Z) || ((controlled.Yaw % 4096 + 4096) % 4096) != world.BodyYaw || world.NativeFootRuntime is null && ragnarokWorld.Motor.CameraYaw != world.CameraYaw)
            {
                throw new InvalidDataException("Ragnarok world globals disagree with its current native object or motor yaw owners.");
            }
        }
        if ((world.NativeCarRuntime is not null ? 1 : 0) + (world.NativeFootRuntime is not null ? 1 : 0) + (world.NativeScriptedRuntime is not null ? 1 : 0) + (world.NativeTrainRuntime is not null ? 1 : 0) > 1)
        {
            throw new InvalidDataException("A world scene cannot simultaneously own foot, car and scripted runtime authority.");
        }
        if (world.NativeGardenRuntime is NativeGardenWorldRuntimeState garden)
        {
            if (world.NativeCarRuntime is not null || world.NativeScriptedRuntime is not null || world.NativeTrainRuntime is not null || world.GroundType is null || garden.ProjectionCamera is null)
            {
                throw new InvalidDataException("Native Garden state requires its sole allocation, selected ground and actual projection camera.");
            }
            NativeWorldPathPosition body = new(world.X, world.NativeHeight, -world.Z);
            NativeGardenWorldObject controlled = garden.Objects[world.NativeFootRuntime is null ? garden.Allocation.Garden64Index : garden.Allocation.FootObjectIndex];
            if (controlled.Position != body || ((controlled.RotationY % 4096 + 4096) % 4096) != world.BodyYaw)
            {
                throw new InvalidDataException("World globals disagree with their actual controlled Garden-allocation body.");
            }
            if (world.NativeFootRuntime is NativeFootWorldRuntimeState gardenFoot)
            {
                gardenFoot.RequireNativeGroundState();
                gardenFoot.GardenDismount?.Validate(garden);
                if (gardenFoot.StationTrains is not null || gardenFoot.NormalObjects is not null || gardenFoot.GardenDismount?.Transfer.LastLoweringFrame > world.Frame || gardenFoot.GroundYaw != garden.GroundYaw || gardenFoot.AnimationVariant != garden.AnimationVariant || gardenFoot.MapDisplayMode != garden.MapDisplayMode || gardenFoot.CameraOffsetY != garden.ProjectionCamera.OffsetY || gardenFoot.CameraOffsetZ != garden.ProjectionCamera.OffsetZ || !NativeWorldGroundCacheState.ContentEquals(gardenFoot.GroundCache, garden.GroundCache) || !NativeWorldForestState.ContentEquals(gardenFoot.Forest, garden.Forest))
                {
                    throw new InvalidDataException("Parked Garden and controlled foot state disagree on their shared physical ground, forest or camera owners.");
                }
            }
            else if (garden.Transfer is null)
            {
                NativeGardenWorldObject footObject = garden.Objects[garden.Allocation.FootObjectIndex];
                NativeGardenWorldObject pairedObject = garden.Objects[garden.Allocation.Garden65Index];
                if (footObject.Position != body || pairedObject.Position != body || ((footObject.RotationY % 4096 + 4096) % 4096) != world.BodyYaw || ((pairedObject.RotationY % 4096 + 4096) % 4096) != world.BodyYaw)
                {
                    throw new InvalidDataException("Ordinary controlled Garden lost548300's shared foot/shape64/shape65 pose publication.");
                }
            }
            else if (garden.Transfer.Counter == 31)
            {
                throw new InvalidDataException("Completed Garden lowering must publish its destination foot runtime in the same callback.");
            }
        }
        else if (world.NativeFootRuntime?.GardenDismount is not null)
        {
            throw new InvalidDataException("Native Garden dismount foot state lost its actual parked Garden allocation.");
        }
        if (world.NativeTrainRuntime is NativeTrainWorldRuntimeState train && world != train.ToWorldState(world))
        {
            throw new InvalidDataException("Native train globals disagree with their sole physical body and rail callback authority.");
        }
        if (world.NativeScriptedRuntime is NativeScriptedWorldRuntimeState scripted && (world.Frame != scripted.Path.Callback || world.X != scripted.Path.EntityPosition.X || world.Z != unchecked(-scripted.Path.EntityPosition.Z) || world.NativeHeight != scripted.Path.EntityPosition.Y || world.BodyYaw != scripted.Load.RetainedBodyYaw || world.CameraYaw != NativeSavedWorldScriptedReturn.CameraYaw(scripted.Path.Camera)))
        {
            throw new InvalidDataException("World scripted projection globals disagree with their executed native callback.");
        }
        if (world.Frame < 0 || world.X is < -131072 or >= 131072 || world.Z is < -98304 or >= 98304 || world.NativeHeight is < short.MinValue or > short.MaxValue || world.BodyYaw is < 0 or >= 4096 || world.NativeScriptedRuntime is null && world.NativeTrainRuntime is null && world.CameraYaw is < 0 or >= 4096 || world.AnimationId < 0 || world.AnimationCounter < 0)
        {
            throw new InvalidDataException("Invalid world checkpoint.");
        }
    }
}
