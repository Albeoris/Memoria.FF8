using System.Numerics;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>A cooperative field VM; it never executes Albeoris's placeholder actor runtime.</summary>
public sealed partial class EventEngine
{
    private readonly IEventServices services;
    private readonly IDictionary<int, int> variables;
    private readonly List<ActorState> actors = [];
    private readonly List<Fiber> fibers = [];
    // Native529FF0 visits its eight fixed channels in ascending order. Keep
    // host presentation, receipts and checkpoints independent of removed slots.
    private readonly SortedDictionary<int, DialogueState> dialogues = [];
    private readonly Dictionary<int, int> backgroundStates = [];
    private readonly Dictionary<int, BackgroundAnimation> backgroundAnimations = [];
    private IReadOnlyList<string> messages = [];
    private JsmProgram? program;
    private double accumulatedSeconds;
    private int preparedMovie = -1;
    private int generation;
    private long sequence;
    private bool defaultsStarted;
    private bool transitionRequested;
    private int backgroundFps = 8;
    private int nameEntity;

    public EventEngine(IEventServices services, IDictionary<int, int> variables)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        this.variables = variables ?? throw new ArgumentNullException(nameof(variables));
        if (services is GameSession session)
        {
            session.MovieCompleting += CompleteNativeMovieActors;
        }
    }

    public const int SimulationFps = 30;
    public int FieldId { get; private set; }
    public long Frame { get; private set; }
    private bool userControlEnabled = true;
    public bool IsUserControlEnabled
    {
        get => partyMenu is null && userControlEnabled && (NativeRuntimeProfile is not { IsVerified: true } || defaultsStarted);
        private set => userControlEnabled = value;
    }
    public bool IsDialogueOpen => dialogues.Count != 0;
    public bool IsDialogueBlocking => dialogues.Values.Any(dialogue => dialogue.CanConfirm);
    // Native4767B0 forwards held directional input to4789A0 independently of
    // plain-text window masks. Choices/naming retain their host input ownership.
    public bool IsMovementBlockedByDialogue => dialogues.Values.Any(dialogue => dialogue.FirstChoice is not null || dialogue.IsNamePrompt);
    public IReadOnlyCollection<DialogueState> Dialogues => dialogues.Values.Select(ExpandMessageVariables).ToArray();
    public bool CanConfirmDialogue => dialogues.Values.Any(dialogue => dialogue.CanConfirm && dialogue.FirstChoice is null && !dialogue.IsNamePrompt);
    private DialogueState? currentDialogue;
    public DialogueState? CurrentDialogue
    {
        get => currentDialogue is null ? null : ExpandMessageVariables(currentDialogue);
        private set => currentDialogue = value;
    }
    public IReadOnlyList<ActorState> Actors => actors;
    public IReadOnlyDictionary<int, int> BackgroundStates => backgroundStates;
    public SceneEventState Scene { get; private set; } = new();
    public Func<int, int, int>? AnimationFrameCount { get; set; }
    public NativeFieldRuntimeProfile? NativeRuntimeProfile { get; private set; }
    public event Action<ScriptCommand>? CommandExecuted;

    private void LoadFieldCore(int fieldId, byte[] jsm, IReadOnlyList<string> fieldMessages, FieldTransition? arrival = null, int? previousFieldId = null, NativeFieldRuntimeProfile? nativeRuntimeProfile = null, System.Text.Json.JsonElement? worldEntityAllocationContext = null)
    {
        if (nativeRuntimeProfile is not null && !nativeRuntimeProfile.IsVerified)
        {
            throw new NotSupportedException("Native field loading requires a verified runtime profile.");
        }
        if (arrival is not null && (arrival.FieldId != fieldId || !float.IsFinite(arrival.X) || !float.IsFinite(arrival.Y) || arrival.Triangle < 0 || arrival.Direction is < 0 or > 255))
        {
            throw new ArgumentException("Coordinate arrival must match the loaded field and contain a valid native spawn.", nameof(arrival));
        }
        ValidatePreviousFieldLoad(previousFieldId);
        ValidatePreviousWorldEntityLoad(fieldId, jsm, worldEntityAllocationContext);
        ValidateNativeMusicFieldLoad(nativeRuntimeProfile);
        JsmProgram next = JsmProgram.Read(jsm);
        string nextHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(jsm));
        if (nativeRuntimeProfile is { IsVerified: true } && services.FieldFade is NativeFieldFadeState fieldFade)
        {
            services.SetFieldFade(fieldFade.ResetPhysicalForLoader().InitializeFreshActors());
        }
        ResetNativeShakeForLoad(nativeRuntimeProfile);
        InitializeNewGameMusic(nativeRuntimeProfile, fieldId);
        if (program is null && fieldId == 74)
        {
            // Native New Game entry 0x470560 seeds field party bytes 00 FF FF before start0 init SETPC.
            Scene.Party.Clear();
            Scene.Party.AddRange([0, 255, 255]);
        }
        generation++;
        FieldId = fieldId;
        Frame = 0;
        accumulatedSeconds = 0;
        program = next;
        pendingArrival = arrival;
        LastFault = null;
        axisStart = InitInputAxisDirection;
        axisTarget = InitInputAxisDirection;
        axisDuration = 0;
        axisElapsed = 0;
        InputAxisDirection = InitInputAxisDirection;
        partyMenu = null;
        programHash = nextHash;
        NativeRuntimeProfile = nativeRuntimeProfile;
        messages = fieldMessages ?? throw new ArgumentNullException(nameof(fieldMessages));
        actors.Clear();
        partyJoin = null;
        partySplit = null;
        preparedGlobalSounds.Clear();
        preparedWorldTransitions.Clear();
        preparedDiscMounts.Clear();
        fibers.Clear();
        InfGateways = [];
        InfAreaTriggers = [];
        dialogues.Clear();
        backgroundStates.Clear();
        backgroundAnimations.Clear();
        CurrentDialogue = null;
        services.DismissDialog();
        defaultsStarted = false;
        transitionRequested = false;
        IsUserControlEnabled = true;
        preparedMovie = -1;
        movieProgressExpected = false;
        CurrentMoviePlayback = null;
        // Native field initialization clears the 256 temporary bytes at variable offset 0x400.
        foreach (int address in variables.Keys.Where(address => address >= 1024 && address < 1280).ToArray())
        {
            variables.Remove(address);
        }
        SceneEventState previousScene = Scene;
        Scene = new SceneEventState { AudioLoadEpoch = checked(previousScene.AudioLoadEpoch + 1), RandomBattlesEnabled = previousScene.RandomBattlesEnabled, LoadedMusic = previousScene.LoadedMusic, MusicPlaying = previousScene.MusicPlaying, MusicVolume = previousScene.MusicVolume, BattleMusic = previousScene.BattleMusic, PartyPower = previousScene.PartyPower };
        Scene.NativeFieldInitialization = nativeRuntimeProfile is { IsVerified: true } ? new NativeFieldInitializationProof(1, nativeRuntimeProfile.ExecutableHash, fieldId, programHash, nativeRuntimeProfile.FieldClock, NativeFieldInitializationStage.Allocated, services.ExamTimer?.ClockCalls) : null;
        InitializeNativeBackgroundScrollSettings();
        Scene.FieldCallbackClock = new NativeFieldCallbackClock(1, 0, 0, 0, 0, InitializationTransactions: 0);
        Scene.NativeSalaryMovementUnits = nativeRuntimeProfile is { IsVerified: true } ? (byte)0 : null;
        Scene.NativeLadderSpeed490E = nativeRuntimeProfile is { IsVerified: true } ? nativeRuntimeProfile.AllocatedLadderSpeed : null;
        //471F70 normal load kinds0..2 reset BYTE1CE4907; menu resumes retain this scene in place.
        Scene.NativeMovieMask = nativeRuntimeProfile is { IsVerified: true } ? new NativeMovieMaskState(1, 1, null) : null;
        pendingNativePlayerInput = null;
        Scene.SoundVolumeTransition = previousScene.SoundVolumeTransition;
        Scene.SoundVolume = previousScene.SoundVolume;
        Scene.SoundPosition = previousScene.SoundPosition;
        Scene.VibrationRequest = previousScene.VibrationRequest;
        Scene.AllSoundVolumeTransition = previousScene.AllSoundVolumeTransition;
        Scene.AllSoundVolume = previousScene.AllSoundVolume;
        CopyNativeMediaHistory(previousScene);
        CopyNativeMusicLoad(previousScene, nativeRuntimeProfile);
        InitializePreviousField(previousFieldId);
        InitializePreviousWorldEntity();
        // Native52BC00 clears B240/241 before normal init; B242 persists globally.
        Scene.DrawPointFieldStateVersion = fieldId >= 72 ? 1 : 0;
        Scene.DrawPointIndex = previousScene.DrawPointIndex;
        //52BC00 clears dispatch bytes, not physical WORD1CDBFEA.
        Scene.NativeDrawPointDisplayMask = previousScene.NativeDrawPointDisplayMask;
        Scene.SoundInitialization = previousScene.SoundInitialization;
        Scene.SoundStop = previousScene.SoundStop;
        Scene.SoundMusicVolume197 = previousScene.SoundMusicVolume197;
        Scene.SoundMusicVolume198 = previousScene.SoundMusicVolume198;
        Scene.MusicChannel108 = previousScene.MusicChannel108;
        Scene.MusicVolumeRequest = previousScene.MusicVolumeRequest;
        Scene.MusicChannel112 = previousScene.MusicChannel112;
        Scene.MusicStop = previousScene.MusicStop;
        Scene.Party.Clear();
        Scene.Party.AddRange(previousScene.Party);
        Scene.PartyBindingMemory = previousScene.PartyBindingMemory;
        Scene.UnlockedTutorials.UnionWith(previousScene.UnlockedTutorials);
        Scene.MinimalMenuCompletions.AddRange(previousScene.MinimalMenuCompletions);
        Scene.NativeBattleCompletions = previousScene.NativeBattleCompletions is null ? null : [.. previousScene.NativeBattleCompletions];
        Scene.NativeBattleWaits = Scene.NativeBattleCompletions is null ? null : [];
        Scene.NativeBattleResult = previousScene.NativeBattleResult;
        CopyNativeBattleMode(previousScene);
        Scene.GlobalSoundRequests.AddRange(previousScene.GlobalSoundRequests);

        Scene.FieldLoadFirstRecordZero = previousScene.NativePartyTraceVersion == 1 ? previousScene.PartyTrace?.FirstTrail[0] : null;
        Scene.FieldLoadSecondRecordZero = previousScene.NativePartyTraceVersion == 1 ? previousScene.PartyTrace?.SecondTrail[0] : null;
        foreach ((int id, string name) in previousScene.Names)
        {
            Scene.Names.Add(id, name);
        }
        foreach (EventObject obj in next.Objects)
        {
            // Original471010 selects clock20 before52BC00 initializes every
            // allocated actor. Executed field scripts may override this default.
            actors.Add(new ActorState(obj.Index, obj.Label) { MovementSpeed = nativeRuntimeProfile?.AllocatedActorSpeed ?? 1000, NativeMovementBaseSpeed = nativeRuntimeProfile is not null ? checked((short)nativeRuntimeProfile.AllocatedActorSpeed) : null, LineEnabled = nativeRuntimeProfile is null || !obj.IsDoor, NativeMotionAuthority = new NativeMotionAuthority(1, 0, 0), NativeTurnAccumulator474 = nativeRuntimeProfile is not null && next.NativeObjects.Take(next.NativeActorCount).Contains(obj.Index) ? (short?)0 : null, NativeMovementCompletion542 = nativeRuntimeProfile is not null && next.NativeObjects.Take(next.NativeActorCount).Contains(obj.Index) ? (ushort?)0 : null, NativeLandingTriangle508 = nativeRuntimeProfile is not null && next.NativeObjects.Take(next.NativeActorCount).Contains(obj.Index) ? (ushort?)0 : null, NativeDisplayOffset = nativeRuntimeProfile is not null && next.NativeObjects.Take(next.NativeActorCount).Contains(obj.Index) ? NativeDisplayOffsetState.AllocatedZero : null });
            if (nativeRuntimeProfile is { IsVerified: true } && next.NativeObjects.Take(next.NativeActorCount).Contains(obj.Index))
            {
                actors[^1].NativePartyFlags = new NativeActorPartyFlags(false, false);
                actors[^1].NativeSavedCollisionDisabled16 = false;
                actors[^1].NativeActorWord398 = 0;
                // 52BC00 clears base bytes591..593; its42 stores target the unrelated bytes568..570.
                actors[^1].BaseAnimationId = 0;
                actors[^1].WalkAnimationId = 0;
                actors[^1].RunAnimationId = 0;
                actors[^1].NativeBaseAnimationVersion = 1;
                actors[^1].NativeAnimationHeader = new NativeActorAnimationHeader(1, 255, 0, 16, 0, 0, 0, null);
                actors[^1].NativeAnimationBackup = new NativeActorAnimationBackup(1, 0, 0, 0, 0, 0, 0);
                actors[^1].NativeShadowAllocation = new NativeActorShadowAllocation(1, fieldId, obj.Index, programHash, nativeRuntimeProfile.ExecutableHash, 96);
                //Fresh52BC00 writes actual actor BYTE394/395/396=128; generic objects have no such producer.
                actors[^1].PolygonColor = new Vector3(128);
                actors[^1].NativeLadderAnimation = new NativeLadderAnimationState(0, 0, 0);
                actors[^1].NativeMovementGroundContact600 = 0;
                actors[^1].NativeFootstepPair = new NativeFootstepPairState(1, 255, 255, new NativeFootstepPairSource(1, "Allocation", fieldId, programHash, nativeRuntimeProfile.ExecutableHash, obj.Index, -1, -1, 0));
            }
            if (obj.BackgroundModuleIndex is int moduleIndex)
            {
                // Native module setup at 0x52C1E6 selects frame 0xFFFF until BGDRAW/BGANIME.
                backgroundStates[moduleIndex] = -1;
                backgroundAnimations[moduleIndex] = new BackgroundAnimation();
            }
            Queue(obj.Index, obj.Scripts[0].Id, 0, true);
        }
    }

    private void UpdateCore(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero || elapsed.TotalSeconds > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Field updates must be between zero and ten seconds.");
        }
        accumulatedSeconds += elapsed.TotalSeconds;
        double secondsPerFrame = 1d / SimulationFps;
        while (accumulatedSeconds + 1e-9 >= secondsPerFrame)
        {
            accumulatedSeconds -= secondsPerFrame;
            Step();
            if (transitionRequested)
            {
                break;
            }
        }
    }

    private void StepCore()
    {
        if (LastFault is EventFault fault)
        {
            throw new EventScriptException(fault.Message);
        }
        if (program is null || transitionRequested)
        {
            return;
        }
        if (PendingInteractiveBattle is not null)
        {
            return;
        }
        if (partyMenu is not null)
        {
            StepPartyMenu();
            return;
        }
        if (!defaultsStarted)
        {
            //Diagnostic load transaction1 has no native callback: all entities
            //finish52C650 before any529FF0 budget, input, RNG, UI or body step.
            NativeFieldCallbackClock initializationClock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Native synchronous initialization requires its executed allocation clock owner.");
            if (Frame != 0 || initializationClock.InitializationTransactions != 0)
            {
                throw new NotSupportedException("Partial synchronous initialization requires authentic journal recreation; callback-based resume is unsupported.");
            }
            initializationClock.Validate(Frame);
            Frame++;
            Scene.FieldCallbackClock = initializationClock with { InitializationTransactions = 1 };
            if (Scene.NativeFieldInitialization is { } initialization)
            {
                Scene.NativeFieldInitialization = initialization with { Stage = NativeFieldInitializationStage.Initializing };
            }
            RunFieldInitialization();
            CompleteFieldInitialization();
            return;
        }
        ValidateNativeShakeCallback();
        NativeEventRandomState? randomSample = PrepareNativeEventRandomBeforeVm();
        NativeSalaryResult? salarySample = PrepareNativeFieldSalarySample();
        bool salaryChanged = salarySample is not null && (salarySample.SeeD != services.SeeD || salarySample.Economy != services.Economy);
        bool salaryPaid = salarySample is not null && salarySample.Economy != services.Economy;
        NativeEconomy? economySample = PrepareNativeGilBeforeVm(salarySample?.Economy ?? services.Economy);
        NativeSalaryEffects? salaryEffects = salarySample is null ? null : PrepareSalaryEffects(salarySample, services.SalaryPresentation, services.GlobalSoundProcess);
        Action? nativePlayerInput = pendingNativePlayerInput;
        ValidatePendingNativePushContacts();
        ValidatePendingNativeLineCrossings();
        if (randomSample is not null || salarySample is not null && (salarySample.SeeD != services.SeeD || economySample != services.Economy || salaryEffects!.Presentation != services.SalaryPresentation || salaryEffects.SoundProcess != services.GlobalSoundProcess))
        {
            services.SetNativeEventOwners(services.WorldControls!, seeD: salarySample?.SeeD, economy: economySample, salaryPresentation: salaryEffects?.Presentation, globalSoundProcess: salaryEffects?.SoundProcess, eventRandom: randomSample);
        }
        else if (economySample is not null && economySample != services.Economy)
        {
            services.SetEconomy(economySample);
        }
        if (salaryChanged)
        {
            RecordCausalReceipt(new ReplayReceipt(ReplayReceiptKind.PersistentEffect, FieldId, Frame, -1, -1, -1, (int)(salaryPaid ? ReplayPersistentEffectKind.SalaryPayment : ReplayPersistentEffectKind.SalaryProgress), Arguments: [checked((int)(semanticSalaryMovementUnits ?? Scene.NativeSalaryMovementUnits!.Value)), 1]));
        }
        if (salaryEffects is not null)
        {
            PublishSalaryEffects(salaryEffects);
        }
        pendingNativePlayerInput = null;
        AdvanceNativeFieldCallbackClock();
        Frame++;
        SampleFieldButtons();
        CompleteNativeBattleReturnBeforeVm();
        SampleMoviePlayback();
        StepInputAxis();
        foreach (ActorState actor in actors)
        {
            actor.StepRotation();
        }
        int currentGeneration = generation;
        foreach (ActorState actor in NativeCallbackActors())
        {
            // Native529FF0 advances animation on this actor's actual turn before its VM.
            StepNativeAnimation(actor);
            DispatchNativePushContact(actor);
            DispatchInfAreaEntry(actor);
            DispatchNativeLineCrossing(actor);
            if (partyJoin?.Targets.Any(target => target.ObjectId == actor.ObjectId) == true || partySplit?.Targets.Any(target => target.ObjectId == actor.ObjectId && actor.ObjectId != partySplit.CallerObjectId) == true)
            {
                continue;
            }
            if (!RunNativeDoorScheduler(actor.ObjectId) && !RunNativeActorContactScheduler(actor.ObjectId))
            {
                Fiber? fiber = SelectScheduledFiber(actor.ObjectId);
                if (fiber is not null)
                {
                    Run(fiber);
                }
            }
            CompleteNativeAsyncMovement(actor);
            if (generation != currentGeneration || transitionRequested)
            {
                return;
            }
            if (program.Objects[actor.ObjectId].BackgroundModuleIndex is int moduleIndex)
            {
                StepBackgroundShadeControl(moduleIndex);
                StepBackgroundAnimation(moduleIndex);
            }
        }
        StepNativeDisplayOffsets();
        foreach (ActorState actor in actors)
        {
            StepCosineRotation(actor);
        }
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            actors[objectId].NativeMovementGroundContact600 = 1;
        }
        StepNativePlayerInput(nativePlayerInput);
        StepNativeScriptMovement();
        StepNativeJump();
        StepNativeLadder();
        StepNativeLadderMode3();
        SampleNativeDoorActionEdges();
        StepNativeShake();
        StepNativeActorBackgroundScroll();
        StepNativeBackgroundScroll();
        StepBackgroundShadeLoops();
        fibers.RemoveAll(item => item.Complete && !item.Initialization && !fibers.Any(waiter => waiter.Request == item) && !actors.Any(actor => actor.NativeDoorScheduler?.CurrentSequence == item.Sequence));
        StepNativeNormalBackgroundScrollCenter();
        // Native4767B0 calls472B30 after VM, physical bodies and background scroll.
        foreach (ActorState actor in actors)
        {
            StepFaceDirection(actor);
        }
        StepNativeModelParts();
        StepNativeMovieMask();
        bool movieUi = services.NativeMovieClockFlag ?? (services.IsMoviePlaying ? throw new NotSupportedException("Native UI requires its actual movie callback flag.") : false);
        NativeSalaryPresentation? salaryUi = services.SalaryPresentation?.Advance(movieUi ? 4 : 2);
        NativeVibrationProcessState? vibrationUi = services.VibrationProcess?.Advance(movieUi ? 4 : 2);
        //4767B0 calls47B980 once after physical/model drawing; movie callbacks skip it.
        NativeFieldFadeState? fadeUi = movieUi ? services.FieldFade : services.FieldFade?.AdvanceCallback();
        if (vibrationUi is not null && Scene.NativeFieldInitialization is not null)
        {
            //471F70 state4 remains active: every4767B0 callback ends with4A2940(-1)/49E8E0.
            vibrationUi = vibrationUi.EndFieldCallback();
        }
        if (vibrationUi is not null || fadeUi is not null)
        {
            if (!services.CanStoreNativeEventOwners || services.WorldControls is not NativeWorldControlState uiControls)
            {
                throw new NotSupportedException("Native UI callback requires atomic current salary/vibration owners.");
            }
            services.SetNativeEventOwners(uiControls, salaryPresentation: salaryUi, vibrationProcess: vibrationUi, fieldFade: fadeUi);
        }
        else if (salaryUi is not null && salaryUi != services.SalaryPresentation)
        {
            services.SetSalaryPresentation(salaryUi);
        }
        ValidateNativeTimerExpiryAfterVm();
    }

    private void ConfirmDialogueCore(int? choice = null, string? name = null)
    {
        if (CurrentDialogue is not DialogueState dialogue)
        {
            return;
        }
        if (!dialogue.CanConfirm)
        {
            return;
        }
        if (dialogue.FirstChoice is null && !dialogue.IsNamePrompt)
        {
            // Native 0x529FF0 visits every automatic channel for the same confirm edge.
            foreach (DialogueState automatic in dialogues.Values.Where(item => item.CanConfirm && item.FirstChoice is null && !item.IsNamePrompt).ToArray())
            {
                RecordDialogueOutcome(automatic, null, null);
                CloseDialogue(automatic.Channel);
            }
            return;
        }
        if (dialogue.FirstChoice is int first)
        {
            int selected = choice ?? dialogue.InitialChoice ?? first;
            if (selected < first || selected > dialogue.LastChoice)
            {
                throw new ArgumentOutOfRangeException(nameof(choice));
            }
            foreach (Fiber fiber in fibers.Where(item => item.DialogueChannel == dialogue.Channel))
            {
                fiber.Results[0] = selected - first;
            }
        }
        if (dialogue.IsNamePrompt)
        {
            string selectedName = string.IsNullOrWhiteSpace(name) ? NativeCharacterName(nameEntity) : name.Trim();
            if (selectedName.Length > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(name), "A character name may contain at most twelve characters.");
            }
            Scene.Names[nameEntity] = selectedName;
        }
        RecordDialogueOutcome(dialogue, dialogue.FirstChoice is null ? null : choice ?? dialogue.InitialChoice ?? dialogue.FirstChoice, dialogue.IsNamePrompt ? Scene.Names[nameEntity] : null);
        CloseDialogue(dialogue.Channel);
    }

    public bool TalkTo(int objectId)
    {
        if (program is null || transitionRequested || services.IsMoviePlaying || !IsUserControlEnabled || IsDialogueBlocking || objectId < 0 || objectId >= actors.Count || !actors[objectId].Active || !actors[objectId].TalkEnabled)
        {
            return false;
        }
        EventObject obj = program.Objects[objectId];
        if (obj.Scripts.Count < 3)
        {
            return false;
        }
        if (replayCapture is null && !services.IsReplayCaptureActive)
        {
            services.MarkReplayUnavailable("A direct talk request bypassed the engine interaction journal.");
        }
        if (IsNativeActorRequest(objectId) && Scene.NativeActorObjectIds.Contains(objectId))
        {
            if (RequestPriorityOccupied(objectId, 7))
            {
                return false;
            }
            ActorState actor = actors[objectId];
            if (actor.MovementActorId is not null || actor.MovementTarget is not null && actor.NativeScriptMovement is not { Active: true } || actor.NativeJump is { Active: true })
            {
                throw new NotSupportedException("Native talk action requires proved body-mode suspension; original request preserved.");
            }
            string? suspension = UnsupportedNativeMovementRequest(actor, 7, obj.Scripts[2].Id);
            if (suspension is not null)
            {
                throw new NotSupportedException(suspension);
            }
            Fiber action = Queue(objectId, obj.Scripts[2].Id, 7, false);
            action.NativeActorSlot = 7;
            SuspendNativeMovementForRequest(actor, action);
            RecordSemanticAdmission(CreateSemanticTrigger(ReplaySemanticTriggerKind.ActorInteraction, objectId, 2, 7));
        }
        else
        {
            throw new NotSupportedException("Talk requests require the shared native actor slot7 scheduler.");
        }
        return true;
    }

    private IReadOnlyList<NativeLineContactChange> MovePlayerCore(float x, float y, float z, int walkmeshTriangle, FieldFixedPosition? nativePosition = null, int? nativeDirection = null, bool nativeMovement = false, FieldFixedPosition? nativeTriggerPosition = null)
    {
        if (program is null || transitionRequested || !IsUserControlEnabled || IsMovementBlockedByDialogue || services.IsMoviePlaying)
        {
            return [];
        }
        ActorState? player = PlayerActor;
        if (player is not { Active: true, Visible: true })
        {
            return [];
        }
        if (NativePlayerHasScriptedMode(player))
        {
            // 4789A0 skips ordinary input when this body's movement mode572
            // is nonzero; the post-VM scripted driver retains ownership.
            return [];
        }
        if (!nativeMovement && program.Objects.Any(obj => obj.IsDoor))
        {
            throw new NotSupportedException("Original native doors require certified fixed-point movement before querying contact; original movement preserved.");
        }
        if (Scene.NativePartyTraceVersion == 1 && nativePosition is null)
        {
            throw new NotSupportedException("Initialized native party movement requires an ordinary player action with its preserved fixed proposal.");
        }
        float deltaX = x - player.X;
        float deltaY = y - player.Y;
        Vector3 previous = new(player.X, player.Y, player.Z);
        int direction = nativeDirection ?? (int)player.Direction;
        if (nativeDirection is null && (deltaX != 0 || deltaY != 0))
        {
            direction = NativeDirection(player.X, player.Y, x, y);
        }
        bool nativeLines = nativeMovement || Scene.NativePlayerMovementVersion == 1;
        IReadOnlyList<NativeLineContactChange> lineChanges = nativeLines ? PrepareNativeLineMovement(player, nativeTriggerPosition ?? nativePosition ?? throw new NotSupportedException("Native line queries require the raw player proposal."), direction) : [];
        Vector3 triggerProposal = nativeTriggerPosition is FieldFixedPosition trigger ? new Vector3(trigger.X / 4096f, trigger.Y / 4096f, previous.Z) : new Vector3(x, y, previous.Z);
        DispatchInfMovement(player, previous, triggerProposal, direction);
        NativeMotionAuthority? motion = player.NativeMotionAuthority;
        player.InvalidateNativeMotionAuthority();
        bool certifiedNativeMotion = nativeMovement || nativePosition is not null && Scene.NativePlayerMovementVersion == 1;
        if (certifiedNativeMotion && motion is { Version: 1 })
        {
            // 4789A0 writes input direction575 before479C60, preserves576,
            // and copies575 to577 only when the facing hold is clear.
            player.NativeMotionAuthority = nativeMovement ? motion with { MovementDirection = unchecked((byte)direction) } : motion;
        }
        if (!certifiedNativeMotion || motion is not { Version: 1, FacingHold: 1 })
        {
            player.Direction = direction;
        }
        player.X = x;
        player.Y = y;
        player.Z = z;
        player.WalkmeshTriangle = walkmeshTriangle;
        if (nativePosition is not null)
        {
            ApplyPartyTraceMovement(player, nativePosition, direction, deltaX != 0 || deltaY != 0);
        }
        if (!nativeLines)
        {
            DispatchLineCrossings(previous, new Vector3(x, y, z));
        }
        return lineChanges;
    }

    private Fiber Queue(int objectId, int scriptId, int priority, bool initialization)
    {
        if (program is null || objectId < 0 || objectId >= program.Objects.Count)
        {
            throw new InvalidDataException($"Field {FieldId} request refers to unknown object {objectId}.");
        }
        EventScript script = program.Objects[objectId].Scripts.SingleOrDefault(item => item.Id == scriptId) ?? throw new InvalidDataException($"Field {FieldId} object {objectId} has no script label {scriptId}.");
        Fiber fiber = new(objectId, script, priority, ++sequence, initialization) { BackgroundId = program.Objects[objectId].BackgroundModuleIndex ?? 0 };
        fibers.Add(fiber);
        return fiber;
    }

    private void Run(Fiber fiber)
    {
        Run(fiber, 16);
    }

    private int Run(Fiber fiber, int instructionBudget)
    {
        if (fiber.WaitReason != WaitReason.None)
        {
            if (!WaitReady(fiber))
            {
                return 0;
            }
            bool nativeDialogueCompleted = fiber.WaitReason == WaitReason.Dialogue && fiber.Position > fiber.Script.Start && program!.Operations[fiber.Position - 1].Opcode is Jsm.Opcode.AMESW or Jsm.Opcode.AASK;
            bool nativeRequestCompleted = NativeRuntimeProfile is { IsVerified: true } && !fiber.Initialization && fiber.WaitReason is WaitReason.RequestStart or WaitReason.RequestEnd && fiber.Position > fiber.Script.Start && program!.Operations[fiber.Position - 1].Opcode is Jsm.Opcode.REQSW or Jsm.Opcode.REQEW or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW;
            bool nativeAnimationCompleted = fiber.WaitReason == WaitReason.NativeAnimationBlocking;
            if (fiber.WaitReason == WaitReason.MinimalMenu)
            {
                Scene.MinimalMenuWaits.Remove(fiber.Sequence);
            }
            if (fiber.WaitReason == WaitReason.MinimalBattle)
            {
                Scene.NativeBattleWaits!.Remove(fiber.Sequence);
            }
            fiber.WaitReason = WaitReason.None;
            if (fiber.NativeDoorRequestWait is null)
            {
                fiber.Request = null;
            }
            if (nativeDialogueCompleted)
            {
                // Native closing return3 consumes the held operation and yields.
                fiber.DialogueChannel = -1;
            }
            if (nativeRequestCompleted || nativeDialogueCompleted || nativeAnimationCompleted)
            {
                return 1;
            }
        }
        fiber.Started = true;
        // The original field VM limits each entity to 16 instructions per frame.
        // Exhausting that budget preserves its PC and resumes on the next frame.
        for (int count = 0; count < instructionBudget; count++)
        {
            if (program is null || fiber.Position < fiber.Script.Start || fiber.Position >= fiber.Script.End)
            {
                throw Fault(fiber, "Instruction pointer left its script range.");
            }
            int operationIndex = fiber.Position;
            JsmOperation operation = program.Operations[operationIndex];
            string? unsupportedContext = UnsupportedFieldInitializationInstruction(fiber, operation) ?? UnsupportedNativeAnimation(fiber, operation) ?? UnsupportedNativeRequestHeaderWrite(fiber, operation) ?? UnsupportedDirectedLinearTurn(fiber, operation) ?? UnsupportedNativeActorVisibility(fiber, operation) ?? UnsupportedRequestContext(fiber, operation) ?? UnsupportedNativeRequestTiming(fiber, operation);
            if (!Supports(operation.Opcode) || unsupportedContext is not null)
            {
                string reason = unsupportedContext ?? $"Unsupported required opcode {operation.Opcode} (parameter {operation.Parameter})";
                string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {operationIndex}: {reason}; instruction pointer and operands preserved.";
                LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, operationIndex, operation.Opcode.ToString(), message);
                throw new EventScriptException(message);
            }
            fiber.Position++;
            try
            {
                Execute(fiber, operation);
                RecordReplayExecution(fiber, operation);
            }
            catch (EventScriptException)
            {
                throw;
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or InvalidDataException or ArithmeticException or NotSupportedException)
            {
                throw Fault(fiber, $"{operation.Opcode}: {exception.Message}", exception);
            }
            if (fiber.Complete || fiber.WaitReason != WaitReason.None || transitionRequested || NativeRequestEndsTurn(fiber, operation))
            {
                return count + 1;
            }
        }
        return instructionBudget;
    }

    private EventScriptException Fault(Fiber fiber, string reason, Exception? inner = null)
    {
        return new EventScriptException($"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position - 1}: {reason}", inner);
    }

    private int[] Pop(Fiber fiber, int count)
    {
        if (fiber.Stack.Count < count)
        {
            throw Fault(fiber, $"Operand stack underflow: need {count}, have {fiber.Stack.Count}.");
        }
        int[] values = new int[count];
        string[] expressions = new string[count];
        for (int index = count - 1; index >= 0; index--)
        {
            values[index] = fiber.Stack.Pop();
            expressions[index] = fiber.StackExpressions.Pop();
        }
        fiber.LastExpressions = expressions;
        return values;
    }

    private void WaitFrames(Fiber fiber, int count)
    {
        fiber.WaitUntil = Frame + Math.Max(0, count);
        fiber.WaitReason = WaitReason.Frames;
    }

    private bool WaitForNativeDialogueChannel(Fiber fiber, int operandCount, bool choice)
    {
        if (fiber.Stack.Count < operandCount)
        {
            return false;
        }
        int channel = fiber.Stack.ElementAt(operandCount - 1);
        if (!dialogues.TryGetValue(channel, out DialogueState? opened) || choice && opened.FirstChoice is null)
        {
            return false;
        }
        // Native occupied return5 retains PC/issued bit; these handlers have
        // not reduced388. Keep every original operand while yielding one turn.
        fiber.Position--;
        WaitFrames(fiber, 1);
        return true;
    }

    private void ShowDialogue(Fiber fiber, DialogueState dialogue, bool wait)
    {
        dialogue = dialogue with { ObjectId = fiber.ObjectId, ScriptId = fiber.Script.Id, Operation = fiber.Position - 1 };
        if (dialogue.MessageId >= messages.Count)
        {
            throw Fault(fiber, $"Message {dialogue.MessageId} is outside the field MSD table ({messages.Count} messages).");
        }
        dialogues[dialogue.Channel] = dialogue;
        RefreshCurrentDialogue();
        if (wait)
        {
            fiber.DialogueChannel = dialogue.Channel;
            fiber.WaitReason = WaitReason.Dialogue;
        }
    }

    private void CloseDialogue(int channel)
    {
        dialogues.Remove(channel);
        RefreshCurrentDialogue();
    }

    private void RefreshCurrentDialogue()
    {
        // TODO(TD-012): persist the native UI focus marker; this is the host representative.
        
        CurrentDialogue = dialogues.Values.LastOrDefault(dialogue => dialogue.CanConfirm) ?? dialogues.Values.LastOrDefault();
        if (CurrentDialogue is not null)
        {
            services.ShowDialog(CurrentDialogue.Text);
        }
        else
        {
            services.DismissDialog();
        }
    }

    private sealed class Fiber(int objectId, EventScript script, int priority, long sequenceNumber, bool initialization)
    {
        public int ObjectId { get; } = objectId;
        public EventScript Script { get; } = script;
        public int Priority { get; } = priority;
        public long Sequence { get; } = sequenceNumber;
        public bool Initialization { get; } = initialization;
        public int Position = script.Start;
        public bool Complete;
        public bool Started;
        public bool NativeResultFrameEntered;
        public NativeOperandStack Stack { get; } = new();
        public Stack<string> StackExpressions { get; } = new();
        public string[] LastExpressions = [];
        public Dictionary<int, NativeScalarValue> Results { get; } = [];
        public WaitReason WaitReason;
        public long WaitUntil;
        public Fiber? Request;
        public NativeDoorRequestWaitState? NativeDoorRequestWait;
        public bool NativeDoorDefaultFrame;
        public NativeScalarValue[]? NativeDoorSavedResults;
        public int DialogueChannel = -1;
        public int BackgroundId;
        public int SoundResourceId;
        public byte? NativeDoorSlot;
        public byte? NativeActorSlot;
        public int? NativeMoveOperation;
        public int? NativeMoveTarget;
        public int? NativeShadeOperation;
    }

    private bool WaitReady(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        return fiber.WaitReason switch
        {
            WaitReason.SoundResource => SoundResourceReady(fiber),
            WaitReason.FaceDirection => actor.FaceProgress == actor.FaceDuration,
            WaitReason.NativeFaceSync => actor.FaceProgress == actor.FaceDuration,
            WaitReason.PartyJoin => true,
            WaitReason.PartySplit => true,
            WaitReason.NativeScriptMovement => true,
            WaitReason.NativeBackgroundShade => true,
            WaitReason.NativeJump => true,
            WaitReason.NativeLadder => true,
            WaitReason.NativeLadderMode3 => true,
            WaitReason.NativeHalt => false,
            WaitReason.WorldJump => false,
            WaitReason.NativePartyMenu => false,
            WaitReason.MinimalMenu => Frame >= fiber.WaitUntil,
            WaitReason.MinimalBattle => Frame >= fiber.WaitUntil && Scene.NativeBattleCompletions![Scene.NativeBattleWaits![fiber.Sequence]].ReturnFrame is not null,
            WaitReason.None => true,
            WaitReason.Frames => Frame >= fiber.WaitUntil,
            WaitReason.Animation => !actor.AnimationPlaying || actor.AnimationLoop,
            WaitReason.NativeAnimationBlocking or WaitReason.NativeAnimationSync => ((RequireNativeAnimationHeader(actor).Flags ?? throw new NotSupportedException("Native animation wait requires its actual completion bit.")) & AnimationComplete) != 0,
            WaitReason.Movement => actor.NativeMovementCompletion542 == 2,
            WaitReason.MoveActor => actor.MovementActorId is null,
            WaitReason.Rotation => actor.RotationTarget is null,
            WaitReason.Dialogue => !dialogues.ContainsKey(fiber.DialogueChannel),
            WaitReason.Movie => !services.IsMoviePlaying,
            WaitReason.Color => true,
            WaitReason.Fade => true,
            WaitReason.BackgroundAnimation => !backgroundAnimations.TryGetValue(fiber.BackgroundId, out BackgroundAnimation? animation) || animation.Complete,
            WaitReason.BackgroundScroll => BackgroundScrollWaitReady(fiber),
            WaitReason.NativeDoorRequest => NativeDoorRequestWaitReady(fiber),
            WaitReason.RequestStart => fiber.Request?.Started == true,
            WaitReason.RequestEnd => fiber.Request?.Complete == true,
            _ => throw Fault(fiber, "Invalid saved wait reason.")
        };
    }


}

public sealed class EventScriptException(string message, Exception? inner = null) : Exception(message, inner);
