using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession : IEventServices
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private SceneKind movieReturnScene;
    private int replayCaptureDepth;

    public GameSession(GameState state)
    {
        Validate(state);
        State = state;
        movieReturnScene = SceneKind.Field;
    }

    public static GameSession CreateColdProcess()
    {
        return new GameSession(new GameState { EventRandom = NativeEventRandomState.FromColdProcess(), ExamTimer = NativeExamTimerState.FromColdProcess(), MessageVariables = NativeMessageVariables.FromColdProcess(), GlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), SalaryPresentation = NativeSalaryPresentation.FromColdProcess(), FieldShake = NativeFieldShake.FromColdProcess(), WorldProcess = NativeWorldProcess.FromColdProcess(), FieldButtonInput = NativeFieldButtonInput.FromColdProcess() });
    }

    public GameState State { get; private set; }
    public ReplayJournal? ReplayJournal => State.ReplayJournal;
    public string? ReplayMovieSourceHash { get; private set; }

    public void SetReplayMovieSourceHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("Invalid movie source identity.");
        }
        ReplayMovieSourceHash = hash;
    }
    public SceneKind ReplayScene => State.Scene;
    public int? ReplayFieldId => State.FieldId;
    public bool IsReplayCaptureActive => replayCaptureDepth != 0;
    public MoviePlaybackState? ReplayMoviePlayback => State.MoviePlayback;
    public NativeWorldControlState? WorldControls => State.WorldControls;
    public bool CanStoreNativeEventOwners => true;
    public NativeWorldConfiguration? WorldConfiguration => State.WorldConfiguration;
    public NativeExamTimerState? ExamTimer => State.ExamTimer;
    public NativeCharacterVitals? CharacterVitals => State.CharacterVitals;
    public NativeCharacterAppearance? CharacterAppearance => State.CharacterAppearance;
    public IReadOnlyDictionary<int, NativeMemoryValue>? NativeVariableValues => State.NativeVariableValues;
    public bool CanStoreNativeVariableValues => true;
    public string? Dialog { get; private set; }
    public bool IsMoviePlaying => State.Scene == SceneKind.Movie;
    // Accepted RequestMovie models51F390->55A530; CompleteMovie models55A5C0 cleanup.
    public bool? NativeMovieClockFlag => IsMoviePlaying;
    public event Action<GameState>? StateChanged;
    public event Action<ReplayJournal>? ReplayJournalChanged;

    public void StartNewGame()
    {
        NativeExamTimerState clock = State.ExamTimer ?? throw new NotSupportedException("New Game requires the existing native process clock; create a cold process explicitly or restore its current clock.");
        NativeExamTimerState reset = clock.ClearSavedStateForNewGame();
        NativeEventRandomState random = State.EventRandom ?? throw new NotSupportedException("New Game requires the actual current native event random process.");
        ReplayJournal journal = ReplayJournal.StartNewGame();
        if (reset.ClockCalls != 0 || reset.Alternatives.Length != 1 || reset.Alternatives[0].FirstPhase != 0 || reset.Alternatives[0].LastPhase != 0 || reset.DisplayEnabled || reset.DisplayX != 0 || reset.DisplayY != 0)
        {
            journal = journal.MarkUnavailable("Warm New Game preserves the native process clock and UI; the journal does not contain its earlier process history.");
        }
        if (State.MessageVariables?.IsColdProcessBank != true)
        {
            journal = journal.MarkUnavailable("Warm New Game preserves the process message-variable bank; the journal lacks its preceding history.");
        }
        if (State.GlobalSoundProcess?.IsColdProcess != true || State.SalaryPresentation?.IsColdProcess != true)
        {
            journal = journal.MarkUnavailable("Warm New Game preserves sound allocation and salary UI process history; the journal lacks its preceding producers.");
        }
        if (State.FieldShake?.IsColdProcess != true)
        {
            journal = journal.MarkUnavailable("Warm NewGame retains prior shake process history that this new journal does not contain.");
        }
        if (State.WorldProcess?.IsColdProcess != true)
        {
            journal = journal.MarkUnavailable("Warm New Game preserves the main-loop counter and world camera cooldown; the journal lacks earlier process history.");
        }
        if (State.FieldButtonInput?.IsColdProcess != true)
        {
            journal = journal.MarkUnavailable("Warm NewGame preserves the input sampler process; the journal lacks its prior physical input history.");
        }
        if (!random.IsColdProcess)
        {
            journal = journal.MarkUnavailable("Warm New Game preserves the native event random process; earlier draws are absent from this new journal.");
        }
        Change(new GameState { EventRandom = random.ClearSavedStateForNewGame(), FieldButtonInput = State.FieldButtonInput, WorldProcess = State.WorldProcess, FieldShake = State.FieldShake, MessageVariables = State.MessageVariables, GlobalSoundProcess = State.GlobalSoundProcess, SalaryPresentation = State.SalaryPresentation, Scene = SceneKind.Field, FieldId = 74, FieldName = "start0", SavedWorld = NativeSavedWorldState.CreateNewGame(), WorldControls = NativeWorldControlState.CreateNewGame(), CardInventory = NativeCardInventory.CreateNewGame(), SeeD = NativeSeeDState.CreateNewGame(State.SeeD), ExamTimer = reset, ReplayJournal = journal });
        ReplayJournalChanged?.Invoke(State.ReplayJournal!);
    }

    public void AppendReplayEntry(ReplayJournalEntry entry)
    {
        if (State.ReplayJournal is ReplayJournal journal)
        {
            State = State with { ReplayJournal = journal.Append(entry) };
            ReplayJournalChanged?.Invoke(State.ReplayJournal!);
        }
    }

    public void RecordReplayEntry(ReplayJournalEntry entry)
    {
        AppendReplayEntry(entry);
    }

    public void MarkReplayUnavailable(string reason)
    {
        if (State.ReplayJournal is ReplayJournal journal)
        {
            State = State with { ReplayJournal = journal.MarkUnavailable(reason) };
            ReplayJournalChanged?.Invoke(State.ReplayJournal!);
        }
    }

    public IDisposable BeginReplayCapture()
    {
        replayCaptureDepth++;
        return new ReplayCapture(this);
    }

    private sealed class ReplayCapture(GameSession session) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (!disposed)
            {
                session.replayCaptureDepth--;
                disposed = true;
            }
        }
    }

    public void Restore(GameState state)
    {
        Dialog = null;
        movieReturnScene = SceneKind.Field;
        Change(state);
    }

    public void RequestMovie(int index)
    {
        ReplayMovieSourceHash = null;
        if (index < 0 || index > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        movieReturnScene = State.Scene == SceneKind.Movie ? movieReturnScene : State.Scene;
        Change(State with { Scene = SceneKind.Movie, MovieIndex = index, MovieSeconds = 0, MoviePlayback = null });
    }

    public void CompleteMovie()
    {
        CompleteMovie(false);
    }

    public void CompleteMovie(bool skipped)
    {
        if (State.Scene == SceneKind.Movie)
        {
            GameState source = State;
            MovieCompleting?.Invoke();
            MoviePlaybackState? playback = State.MoviePlayback;
            if (playback is not null)
            {
                playback = playback with { FrameIndex = playback.TerminalFrame, IsPlaying = false, Completed = true, Skipped = skipped };
            }
            Change(State with { Scene = movieReturnScene, MovieSeconds = 0, MoviePlayback = playback });
            RecordMovieChange(source, ReplayEntryKind.MovieComplete, skipped);
        }
    }

    public void RequestField(int fieldId, int entryPoint)
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
        Dialog = null;
        Change(State with { Scene = SceneKind.Field, FieldId = fieldId, PreviousFieldId = previousFieldId, FieldName = name, EntryPoint = entryPoint, X = 0, Y = 0, EventState = null, DepartingFieldState = null, DialogueDraft = null, FieldArrival = null, WorldArrival = null, World = null, DebugView = null, WorldControls = State.WorldControls?.WithNormalFieldLoad() });
    }

    public void RequestField(FieldTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        RequestField(transition.FieldId, 0);
        Change(State with { X = transition.X, Y = transition.Y, FieldArrival = transition });
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
        if (departure.Scene != SceneKind.WorldMap || departure.World is not WorldState liveWorld || departure.WorldArrival is not WorldTransition arrival || unchecked((sbyte)arrival.Mode) is not (0 or 1) || departure.SavedWorld is null || departure.WorldControls is null)
        {
            throw new NotSupportedException("Native world-to-field entry requires a known normal foot-world departure and saved/control provenance.");
        }
        Validate(departure with { World = finalWorldState, WorldProcess = PrepareWorldProcess(departure, finalWorldState) });
        ValidatePreparedWorldActions(liveWorld, finalWorldState);
        if (transition.FieldId is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(transition), "Native world-to-field destination must fit its unsigned WORD.");
        }
        if (receipt.LastFieldId != departure.FieldId || receipt.SourceWorldFrame != finalWorldState.Frame || receipt.Pose0 != new NativeSavedWorldPose(finalWorldState.X, finalWorldState.Z, checked((short)finalWorldState.NativeHeight), checked((ushort)finalWorldState.BodyYaw)) || receipt.CameraYaw96 != finalWorldState.CameraYaw || receipt.StoryWord != departure.Variables.GetValueOrDefault(256) || receipt.EntityType105 != unchecked((byte)arrival.Mode) || finalWorldState.NativeFootRuntime is NativeFootWorldRuntimeState foot && receipt.EntityType105 != foot.EntityType)
        {
            throw new InvalidOperationException("The prepared foot save no longer matches the current native world departure.");
        }
        string name = transition.FieldId switch { 106 => "bcgate_1", 136 => "bdview1", 159 => "bggate_1", _ => $"field_{transition.FieldId}" };
        GameState candidate = departure with
        {
            Scene = SceneKind.Field,
            FieldId = transition.FieldId,
            PreviousFieldId = receipt.LastFieldId,
            FieldName = name,
            EntryPoint = 0,
            X = transition.X,
            Y = transition.Y,
            EventState = null,
            DepartingFieldState = departure.DepartingFieldState ?? departure.EventState,
            DialogueDraft = null,
            FieldArrival = transition,
            WorldArrival = null,
            World = null,
            DepartingWorldState = finalWorldState,
            DebugView = null,
            SavedWorld = departure.SavedWorld.WithFootReturn(receipt),
            WorldControls = departure.WorldControls.WithNormalFieldLoad(),
            WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(finalWorldState, departure.FieldId),
            ExamTimer = AdvanceWorldTimer(departure, finalWorldState),
            WorldProcess = PrepareWorldProcess(departure, finalWorldState),
            SeeD = salary.SeeD
        };
        candidate = salary.ApplyTo(candidate, finalWorldState);
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
        GameState source = State;
        State = State with { MoviePlayback = playback };
        RecordMovieChange(source, ReplayEntryKind.MovieSample);
    }

    public void RequestWorld(WorldTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        Dialog = null;
        Change(State with { Scene = SceneKind.WorldMap, WorldArrival = transition, World = null, FieldArrival = null, DialogueDraft = null, DebugView = null, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(transition, State.FieldId) });
    }

    public void CommitPreparedWorldEntry(WorldTransition transition, WorldState initialState, NativeWorldControlState expectedControls, Action<GameState> validatePrepared, NativeSavedWorldState? preparedSavedWorld = null)
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
        NativeSavedWorldState? expectedSaved = initialState.NativeCarRuntime is null ? departure.SavedWorld : departure.SavedWorld?.WithOrdinaryCarEntry();
        if (saved != expectedSaved)
        {
            throw new InvalidOperationException("Prepared world saved-vehicle state differs from its native entry writes.");
        }
        GameState candidate = departure with { Scene = SceneKind.WorldMap, WorldArrival = transition, World = initialState, FieldArrival = null, DialogueDraft = null, DebugView = null, WorldControls = expectedControls.WithWorldEntryMenuFlag(), SavedWorld = saved, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(transition, departure.FieldId) };
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
        if (departure.Scene != SceneKind.WorldMap || departure.World is not WorldState liveWorld || !ReferenceEquals(departure, salary.Departure))
        {
            throw new InvalidOperationException("A world action requires its current native world session.");
        }
        ValidatePreparedWorldActions(liveWorld, state);
        GameState candidate = salary.ApplyTo(departure with { World = state, SavedWorld = PrepareScriptedWorldSave(departure, state), WorldConfiguration = PrepareScriptedWorldConfiguration(departure, state), ExamTimer = AdvanceWorldTimer(departure, state), WorldProcess = PrepareWorldProcess(departure, state) }, state);
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
        State = State with { WorldControls = controls };
    }

    public void SetNativeEventOwners(NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null)
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
        if (eventRandom is { DrawPoints: null } && State.EventRandom?.DrawPoints is not null)
        {
            throw new InvalidDataException("Atomic native event publication cannot erase the already initialized draw-point bank.");
        }
        if (eventRandom is not null || State.EventRandom is not null)
        {
            ValidateEventRandomAliases(State.Variables, State.NativeVariableValues);
        }
        if (seeD is not null || State.SeeD is not null)
        {
            ValidateSeeDAliases(State.Variables, State.NativeVariableValues);
        }
        if (characterVitals is not null || State.CharacterVitals is not null)
        {
            ValidateJunctionPartyBackupAliases(State.Variables, State.NativeVariableValues);
        }
        NativeCharacterVitals? nextVitals = characterVitals is null ? State.CharacterVitals : characterVitals.DeepCopy();
        NativeEconomy? nextEconomy = economy is null ? State.Economy : economy.DeepCopy();
        if (nextEconomy is not null && nextVitals is not null && nextEconomy.InitOutSha256 != nextVitals.InitOutSha256)
        {
            throw new InvalidDataException("Native economy and character owners have different initialization provenance.");
        }
        State = State with { WorldControls = controls, SeeD = seeD ?? State.SeeD, ExamTimer = timer ?? State.ExamTimer, CharacterVitals = nextVitals, Economy = nextEconomy, GlobalSoundProcess = globalSoundProcess ?? State.GlobalSoundProcess, SalaryPresentation = salaryPresentation ?? State.SalaryPresentation, EventRandom = eventRandom ?? State.EventRandom };
    }

    public void SetExamTimer(NativeExamTimerState timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        timer.Validate();
        State = State with { ExamTimer = timer };
    }

    public void InitializeNewGameCharacterVitals(NativeCharacterVitals vitals)
    {
        if (State.CharacterVitals is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.ReplayJournal is not { EntryCount: 0, IsComplete: true } || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Character initialization requires the untouched authentic New Game before its first engine transaction.");
        }
        SetCharacterVitals(vitals);
    }

    public void SetCharacterVitals(NativeCharacterVitals vitals)
    {
        vitals.Validate();
        ValidateJunctionPartyBackupAliases(State.Variables, State.NativeVariableValues);
        State = State with { CharacterVitals = vitals.DeepCopy() };
    }

    public void InitializeNewGameCharacterAppearance(NativeCharacterAppearance appearance)
    {
        if (State.CharacterAppearance is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.ReplayJournal is not { EntryCount: 0, IsComplete: true } || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
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

    public void SetNativeVariableValues(Dictionary<int, NativeMemoryValue>? values)
    {
        if (values is not null)
        {
            NativeMemoryValue.Validate(values, State.Variables);
        }
        State = State with { NativeVariableValues = values is { Count: > 0 } ? new Dictionary<int, NativeMemoryValue>(values) : null };
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
        GameState source = State;
        State = State with { MovieSeconds = Math.Max(0, seconds) };
        RecordMovieChange(source, ReplayEntryKind.MovieSample);
    }

    private void RecordMovieChange(GameState source, ReplayEntryKind kind, bool skipped = false)
    {
        if (replayCaptureDepth == 0 && (source.MoviePlayback != State.MoviePlayback || source.MovieSeconds != State.MovieSeconds || source.Scene != State.Scene))
        {
            AppendReplayEntry(new ReplayJournalEntry(kind, source.Scene, source.FieldId, source.ReplayJournal?.Endpoint?.ResultFrame ?? source.ReplayJournal?.Endpoint?.Frame ?? 0) { MoviePlayback = State.MoviePlayback, MovieSeconds = State.MovieSeconds, MovieSourceHash = ReplayMovieSourceHash, Skipped = skipped, ResultScene = State.Scene, ResultFieldId = State.FieldId });
        }
    }

    public void Save(string path, bool registerJournalReference = true)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(State, JsonOptions));
        if (registerJournalReference && State.ReplayJournal is ReplayJournal journal)
        {
            // Pin before publishing the save: interrupted publication may retain extra history, never lose it.
            new ReplayJournalArchive(ReplayJournalArchive.DefaultDirectory).Pin(fullPath, journal);
        }
        File.Move(temporary, fullPath, true);
    }

    public void SetEventState(JsonElement? state)
    {
        State = State with { EventState = state?.Clone() };
    }

    public void SetDepartingFieldState(JsonElement? state)
    {
        State = State with { DepartingFieldState = state?.Clone() };
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
        GameState state = JsonSerializer.Deserialize<GameState>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Empty checkpoint.");
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
        ValidateEventRandomCheckpoint(state, currentVm);
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
        if (currentVm is JsonElement memoryVm && memoryVm.TryGetProperty("NativeVariableValues", out JsonElement memoryJson) && memoryJson.ValueKind != JsonValueKind.Null)
        {
            Dictionary<int, NativeMemoryValue> memory = memoryJson.Deserialize<Dictionary<int, NativeMemoryValue>>(JsonOptions) ?? throw new InvalidDataException("Empty event uncertain memory state.");
            NativeMemoryValue.Validate(memory, state.Variables);
            if (state.NativeVariableValues is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.NativeVariableValues), JsonSerializer.SerializeToElement(memory)))
            {
                throw new InvalidDataException("Root and event uncertain native memory checkpoints disagree.");
            }
        }
        else if (currentVm is not null && state.NativeVariableValues is { Count: > 0 })
        {
            throw new InvalidDataException("Root uncertain native memory is missing from the saved complete VM.");
        }
        ValidateSeeDCheckpoint(state, currentVm);
        ValidateMessageVariableCheckpoint(state, currentVm);
        ValidateInputConfigurationCheckpoint(state, currentVm);
        ValidatePartyMenuOwnersCheckpoint(state, currentVm);
        return state;
    }

    public static GameState LoadReplaySource(string path)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            int version = root.GetProperty("Version").GetInt32();
            SceneKind scene = root.GetProperty("Scene").Deserialize<SceneKind>(JsonOptions);
            int fieldId = root.GetProperty("FieldId").GetInt32();
            if (version != 1 || !Enum.IsDefined(scene) || scene == SceneKind.Title || fieldId is < 0 or > ushort.MaxValue)
            {
                throw new InvalidDataException("Invalid replay checkpoint boundary.");
            }
            ReplayJournal? journal = root.TryGetProperty("ReplayJournal", out JsonElement capsule) && capsule.ValueKind != JsonValueKind.Null ? capsule.Deserialize<ReplayJournal>(JsonOptions) : null;
            journal?.Validate();
            string name = root.TryGetProperty("FieldName", out JsonElement fieldName) && fieldName.ValueKind == JsonValueKind.String ? fieldName.GetString()! : $"field_{fieldId}";
            DialogueDraft? draft = root.TryGetProperty("DialogueDraft", out JsonElement draftJson) && draftJson.ValueKind != JsonValueKind.Null ? draftJson.Deserialize<DialogueDraft>(JsonOptions) : null;
            return new GameState { Version = version, Scene = scene, FieldId = fieldId, FieldName = name, ReplayJournal = journal, DialogueDraft = draft };
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Malformed replay checkpoint boundary: " + exception.Message, exception);
        }
    }

    private void Change(GameState state)
    {
        Validate(state);
        State = state;
        StateChanged?.Invoke(state);
    }

    private static void Validate(GameState state)
    {
        state.ReplayJournal?.Validate();
        if (state.PreviousFieldId is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Invalid native previous-field provenance.");
        }
        state.SavedWorld?.Validate();
        state.WorldControls?.Validate();
        state.WorldConfiguration?.Validate();
        state.ExamTimer?.Validate();
        state.SeeD?.Validate();
        if (state.SeeD is not null)
        {
            ValidateSeeDAliases(state.Variables, state.NativeVariableValues);
        }
        state.CharacterVitals?.Validate();
        if (state.CharacterVitals is not null)
        {
            ValidateJunctionPartyBackupAliases(state.Variables, state.NativeVariableValues);
        }
        state.CharacterAppearance?.Validate();
        state.CardInventory?.Validate();
        state.Economy?.Validate();
        state.GlobalSoundProcess?.Validate();
        state.WorldProcess?.Validate();
        state.SalaryPresentation?.Validate();
        state.FieldShake?.Validate();
        state.EventRandom?.Validate();
        if (state.EventRandom is not null)
        {
            ValidateEventRandomAliases(state.Variables, state.NativeVariableValues);
        }
        state.InputConfiguration?.Validate();
        state.FieldButtonInput?.Validate();
        if (state.Economy is not null)
        {
            ValidateEconomyAliases(state.Variables, state.NativeVariableValues);
            if (state.CharacterVitals is not null && state.Economy.InitOutSha256 != state.CharacterVitals.InitOutSha256)
            {
                throw new InvalidDataException("Native economy and character state have different New Game template identities.");
            }
        }
        if (state.CharacterAppearance is not null && state.CharacterVitals is not null && state.CharacterAppearance.InitOutSha256 != state.CharacterVitals.InitOutSha256)
        {
            throw new InvalidDataException("Native appearance and character vitals have different New Game template identities.");
        }
        if (state.NativeVariableValues is not null)
        {
            NativeMemoryValue.Validate(state.NativeVariableValues, state.Variables);
        }
        state.DebugView?.Validate();
        if (state.DebugView is not null && state.DebugView.FieldId != state.FieldId)
        {
            throw new InvalidDataException("Debug view belongs to another field.");
        }
        if (state.EntryPoint != 0)
        {
            throw new NotSupportedException("Nonzero original field entry points are not implemented; use a full recreation checkpoint.");
        }
        if (state.Version != 1 || !Enum.IsDefined(state.Scene) || state.FieldId < 0 || !float.IsFinite(state.X) || !float.IsFinite(state.Y) || !double.IsFinite(state.MovieSeconds) || state.MovieSeconds < 0)
        {
            throw new InvalidDataException("Unsupported or invalid recreation checkpoint.");
        }
        if (state.FieldArrival is FieldTransition arrival && (arrival.FieldId != state.FieldId || !float.IsFinite(arrival.X) || !float.IsFinite(arrival.Y) || arrival.Triangle < 0 || arrival.Direction is < 0 or > 255))
        {
            throw new InvalidDataException("Invalid coordinate field arrival.");
        }
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
            ValidateWorldState(world);
            if (world.NativeFootRuntime?.Forest is NativeWorldForestState forest && (state.WorldProcess is null || forest.CameraCooldown != state.WorldProcess.CameraCooldown))
            {
                throw new InvalidDataException("Active native foot forest cooldown disagrees with the world process authority.");
            }
            if (world.NativeFootRuntime is NativeFootWorldRuntimeState foot && (state.Scene != SceneKind.WorldMap || state.WorldArrival is null || unchecked((byte)state.WorldArrival.Mode) != foot.EntityType))
            {
                throw new InvalidDataException("Native foot world state disagrees with its original transition entity type.");
            }
            if (world.NativeCarRuntime is not null && (state.Scene != SceneKind.WorldMap || state.WorldArrival is null || unchecked((sbyte)state.WorldArrival.Mode) != 33 || state.SavedWorld?.Slot4.ShapeId != 73))
            {
                throw new InvalidDataException("Native car world state disagrees with its original transition or active saved vehicle.");
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
        if ((world.NativeCarRuntime is not null ? 1 : 0) + (world.NativeFootRuntime is not null ? 1 : 0) + (world.NativeScriptedRuntime is not null ? 1 : 0) > 1)
        {
            throw new InvalidDataException("A world scene cannot simultaneously own foot, car and scripted runtime authority.");
        }
        if (world.NativeScriptedRuntime is NativeScriptedWorldRuntimeState scripted && (world.Frame != scripted.Path.Callback || world.X != scripted.Path.EntityPosition.X || world.Z != unchecked(-scripted.Path.EntityPosition.Z) || world.NativeHeight != scripted.Path.EntityPosition.Y || world.BodyYaw != scripted.Load.RetainedBodyYaw || world.CameraYaw != NativeSavedWorldScriptedReturn.CameraYaw(scripted.Path.Camera)))
        {
            throw new InvalidDataException("World scripted projection globals disagree with their executed native callback.");
        }
        if (world.Frame < 0 || world.X is < -131072 or >= 131072 || world.Z is < -98304 or >= 98304 || world.NativeHeight is < short.MinValue or > short.MaxValue || world.BodyYaw is < 0 or >= 4096 || world.NativeScriptedRuntime is null && world.CameraYaw is < 0 or >= 4096 || world.AnimationId < 0 || world.AnimationCounter < 0)
        {
            throw new InvalidDataException("Invalid world checkpoint.");
        }
    }

    private static void ValidatePreparedWorldActions(WorldState liveWorld, WorldState finalWorld)
    {
        if (ReferenceEquals(finalWorld, liveWorld))
        {
            return;
        }
        if (liveWorld.Actions is null || finalWorld.Actions is null || finalWorld.Actions.Count != liveWorld.Actions.Count + 1 || !finalWorld.Actions.Take(liveWorld.Actions.Count).SequenceEqual(liveWorld.Actions))
        {
            throw new InvalidOperationException("Prepared field entry must preserve the complete ordinary world action prefix.");
        }
        WorldActionRecord tail = finalWorld.Actions[^1];
        if (tail is null || tail.Index != liveWorld.Actions.Count + 1 || tail.Frame != finalWorld.Frame || tail.Kind is not "Move" and not "Wait" || !float.IsFinite(tail.Horizontal) || !float.IsFinite(tail.Vertical) || Math.Abs(tail.Horizontal) > 1 || Math.Abs(tail.Vertical) > 1 || tail.RequestedFrames is < 1 or > 30000 || tail.AdvancedFrames < 0 || tail.AdvancedFrames > tail.RequestedFrames || liveWorld.Frame > long.MaxValue - tail.AdvancedFrames || finalWorld.Frame != liveWorld.Frame + tail.AdvancedFrames || tail.X != finalWorld.X || tail.Z != finalWorld.Z || tail.NativeHeight != finalWorld.NativeHeight)
        {
            throw new InvalidOperationException("Prepared field entry must retain its exact final ordinary world action record.");
        }
        if (tail.AdvancedFrames == 0 && (finalWorld with { Actions = liveWorld.Actions }) != liveWorld)
        {
            throw new InvalidOperationException("A zero-frame entrance cannot alter the current world pose or clocks.");
        }
    }
}
