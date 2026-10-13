using System.Numerics;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>A cooperative field VM; it never executes Albeoris's placeholder actor runtime.</summary>
public sealed partial class EventEngine
{
    private readonly IEventServices services;
    private readonly IDictionary<int, int> variables;
    private uint randomState;
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
    private long colorEndFrame;
    private long colorStartFrame;
    private Vector3 colorStart;
    private Vector3 colorEnd;
    private bool subtractColor;
    private long fadeEndFrame;
    private long fadeStartFrame;
    private float fadeStart;
    private float fadeEnd;
    private int backgroundFps = 8;
    private int nameEntity;

    public EventEngine(IEventServices services, IDictionary<int, int> variables, int randomSeed = 0)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        this.variables = variables ?? throw new ArgumentNullException(nameof(variables));
        randomState = unchecked((uint)randomSeed + 1);
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
        get => userControlEnabled && (NativeRuntimeProfile is not { IsVerified: true } || defaultsStarted);
        private set => userControlEnabled = value;
    }
    public bool IsDialogueOpen => dialogues.Count != 0;
    public bool IsDialogueBlocking => dialogues.Values.Any(dialogue => dialogue.CanConfirm);
    public IReadOnlyCollection<DialogueState> Dialogues => dialogues.Values;
    public bool CanConfirmDialogue => dialogues.Values.Any(dialogue => dialogue.CanConfirm && dialogue.FirstChoice is null && !dialogue.IsNamePrompt);
    public DialogueState? CurrentDialogue { get; private set; }
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
        InitializeNewGameMusic(nativeRuntimeProfile, fieldId);
        if (program is null && fieldId == 74)
        {
            // Native New Game entry 0x470560 seeds field party bytes 00 FF FF before start0 init SETPC.
            Scene.Party.Clear();
            Scene.Party.Add(0);
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
        programHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(jsm));
        NativeRuntimeProfile = nativeRuntimeProfile;
        messages = fieldMessages ?? throw new ArgumentNullException(nameof(fieldMessages));
        actors.Clear();
        partyJoin = null;
        partySplit = null;
        preparedGlobalSounds.Clear();
        preparedWorldTransitions.Clear();
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
        colorEndFrame = 0;
        fadeEndFrame = 0;
        SceneEventState previousScene = Scene;
        Scene = new SceneEventState { RandomBattlesEnabled = previousScene.RandomBattlesEnabled, SalaryEnabled = previousScene.SalaryEnabled, LoadedMusic = previousScene.LoadedMusic, MusicPlaying = previousScene.MusicPlaying, MusicVolume = previousScene.MusicVolume, BattleMusic = previousScene.BattleMusic, PartyPower = previousScene.PartyPower };
        Scene.NativeFieldInitialization = nativeRuntimeProfile is { IsVerified: true } ? new NativeFieldInitializationProof(1, nativeRuntimeProfile.ExecutableHash, fieldId, programHash, nativeRuntimeProfile.FieldClock, NativeFieldInitializationStage.Allocated, services.ExamTimer?.ClockCalls) : null;
        Scene.FieldCallbackClock = new NativeFieldCallbackClock(1, 0, 0);
        Scene.Shake = nativeRuntimeProfile is { IsVerified: true } ? (previousScene.Shake ?? NativeFieldShake.UnknownProcessHistory()).ResetForLoad() : null;
        Scene.SoundVolumeTransition = previousScene.SoundVolumeTransition;
        Scene.SoundVolume = previousScene.SoundVolume;
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
        Scene.SoundInitialization = previousScene.SoundInitialization;
        Scene.SoundStop = previousScene.SoundStop;
        Scene.SoundMusicVolume197 = previousScene.SoundMusicVolume197;
        Scene.SoundMusicVolume198 = previousScene.SoundMusicVolume198;
        Scene.MusicChannel108 = previousScene.MusicChannel108;
        Scene.MusicVolumeRequest = previousScene.MusicVolumeRequest;
        Scene.FadeMode216 = previousScene.FadeMode216;
        Scene.FadeNone = previousScene.FadeNone;
        Scene.MusicChannel112 = previousScene.MusicChannel112;
        Scene.MusicStop = previousScene.MusicStop;
        Scene.Members.UnionWith(previousScene.Members);
        Scene.Party.AddRange(previousScene.Party);
        Scene.PartyBindingMemory = previousScene.PartyBindingMemory;
        Scene.UnlockedTutorials.UnionWith(previousScene.UnlockedTutorials);
        Scene.AcquiredGuardianForces.UnionWith(previousScene.AcquiredGuardianForces);
        Scene.MinimalMenuCompletions.AddRange(previousScene.MinimalMenuCompletions);
        Scene.NativeBattleCompletions = previousScene.NativeBattleCompletions is null ? null : [.. previousScene.NativeBattleCompletions];
        Scene.NativeBattleWaits = Scene.NativeBattleCompletions is null ? null : [];
        Scene.NativeBattleResult = previousScene.NativeBattleResult;
        CopyNativeBattleMode(previousScene);
        Scene.GlobalSoundRequests.AddRange(previousScene.GlobalSoundRequests);
        foreach ((int voice, GlobalSoundRequest request) in previousScene.GlobalSoundVoices)
        {
            Scene.GlobalSoundVoices.Add(voice, request);
        }
        Scene.FieldLoadFirstRecordZero = previousScene.NativePartyTraceVersion == 1 ? previousScene.PartyTrace?.FirstTrail[0] : null;
        Scene.FieldLoadSecondRecordZero = previousScene.NativePartyTraceVersion == 1 ? previousScene.PartyTrace?.SecondTrail[0] : null;
        foreach ((int id, string name) in previousScene.Names)
        {
            Scene.Names.Add(id, name);
        }
        foreach ((int id, int[] hold) in previousScene.Holds)
        {
            Scene.Holds.Add(id, hold);
        }
        foreach (EventObject obj in next.Objects)
        {
            // Original471010 selects clock20 before52BC00 initializes every
            // allocated actor. Executed field scripts may override this default.
            actors.Add(new ActorState(obj.Index, obj.Label) { MovementSpeed = nativeRuntimeProfile?.AllocatedActorSpeed ?? 1000, NativeMovementBaseSpeed = nativeRuntimeProfile is not null ? checked((short)nativeRuntimeProfile.AllocatedActorSpeed) : null, LineEnabled = nativeRuntimeProfile is null || !obj.IsDoor, NativeMotionAuthority = new NativeMotionAuthority(1, 0, 0), NativeDisplayOffset = nativeRuntimeProfile is not null && next.NativeObjects.Take(next.NativeActorCount).Contains(obj.Index) ? NativeDisplayOffsetState.AllocatedZero : null });
            if (nativeRuntimeProfile is { IsVerified: true } && next.NativeObjects.Take(next.NativeActorCount).Contains(obj.Index))
            {
                actors[^1].NativeShadowAllocation = new NativeActorShadowAllocation(1, fieldId, obj.Index, programHash, nativeRuntimeProfile.ExecutableHash, 96);
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
        ValidatePendingNativePushContacts();
        ValidatePendingNativeLineCrossings();
        AdvanceNativeFieldCallbackClock();
        Frame++;
        SampleFieldButtons();
        CompleteNativeBattleReturnBeforeVm();
        if (!defaultsStarted && Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Allocated } initialization)
        {
            Scene.NativeFieldInitialization = initialization with { Stage = NativeFieldInitializationStage.Initializing };
        }
        StepJoinedCompanions();
        StepStationaryPartyTrace();
        SampleMoviePlayback();
        StepInputAxis();
        foreach (ActorState actor in actors)
        {
            if (actor.MovementActorId is int targetId)
            {
                actor.InvalidateNativeMotionAuthority();
                ActorState targetActor = actors[targetId];
                Vector3 position = new(actor.X, actor.Y, actor.Z);
                Vector3 target = new(targetActor.X, targetActor.Y, targetActor.Z);
                Vector3 delta = target - position;
                if (delta.Length() <= actor.MovementStopDistance)
                {
                    actor.MovementActorId = null;
                    actor.MovementTarget = null;
                }
                else
                {
                    actor.MovementTarget = target - Vector3.Normalize(delta) * actor.MovementStopDistance;
                }
            }
            actor.Step();
            StepCosineRotation(actor);
            StepFaceDirection(actor);
        }
        StepLegacyBackgroundAnimations();
        StepVisualTransitions();
        StepBackgroundShadeLoops();
        int currentGeneration = generation;
        foreach (ActorState actor in actors)
        {
            DispatchNativePushContact(actor);
            DispatchInfAreaEntry(actor);
            DispatchNativeLineCrossing(actor);
            if (partyJoin?.Targets.Any(target => target.ObjectId == actor.ObjectId) == true || partySplit?.Targets.Any(target => target.ObjectId == actor.ObjectId && actor.ObjectId != partySplit.CallerObjectId) == true)
            {
                continue;
            }
            if (!RunNativeDoorScheduler(actor.ObjectId) && !RunNativeActorContactScheduler(actor.ObjectId))
            {
                Fiber? fiber = fibers.Where(item => item.ObjectId == actor.ObjectId && !item.Complete).OrderBy(item => item.Priority).ThenBy(item => item.Sequence).FirstOrDefault();
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
                StepBackgroundAnimation(moduleIndex);
            }
        }
        StepNativeDisplayOffsets();
        StepNativeScriptMovement();
        StepNativeJump();
        SampleNativeDoorActionEdges();
        StepSplitParty();
        StepNativeShake();
        StepNativeActorBackgroundScroll();
        StepNativeBackgroundScroll();
        if (!defaultsStarted && fibers.Where(item => item.Initialization).All(item => item.Complete))
        {
            NativeFieldPartyLoad? nativeLoad = PrepareNativeFieldPartyLoad();
            CompactNativeActors();
            if (nativeLoad is not null)
            {
                ApplyNativeFieldPartyLoad(nativeLoad);
            }
            else
            {
                ApplyPendingArrival();
            }
            if (Scene.NativeFieldInitialization is NativeFieldInitializationProof proof)
            {
                Scene.NativeFieldInitialization = proof with { Stage = NativeFieldInitializationStage.Completed };
            }
            InitializeNativeDoorDriverAfterInitialization();
            defaultsStarted = true;
            foreach (EventObject obj in program.Objects)
            {
                if (actors[obj.Index].Active && obj.Scripts.Count > 1)
                {
                    Queue(obj.Index, obj.Scripts[1].Id, 7, false);
                }
            }
        }
        fibers.RemoveAll(item => item.Complete && !item.Initialization && !fibers.Any(waiter => waiter.Request == item));
        StepNativeNormalBackgroundScrollCenter();
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
            string selectedName = string.IsNullOrWhiteSpace(name) ? Scene.Names.GetValueOrDefault(nameEntity, "Squall") : name.Trim();
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
        Queue(objectId, obj.Scripts[2].Id, 1, false);
        return true;
    }

    public void MovePlayer(float x, float y, float z, int walkmeshTriangle)
    {
        if (replayCapture is null)
        {
            services.MarkReplayUnavailable("A direct player placement bypassed the engine navigation journal.");
        }
        MovePlayerCore(x, y, z, walkmeshTriangle);
    }

    private IReadOnlyList<NativeLineContactChange> MovePlayerCore(float x, float y, float z, int walkmeshTriangle, FieldFixedPosition? nativePosition = null, int? nativeDirection = null, bool nativeMovement = false, FieldFixedPosition? nativeTriggerPosition = null)
    {
        if (program is null || transitionRequested || !IsUserControlEnabled || IsDialogueBlocking || services.IsMoviePlaying)
        {
            return [];
        }
        ActorState? player = actors.FirstOrDefault(actor => actor.Active && actor.CharacterId == 0 && actor.Visible);
        if (player is null)
        {
            return [];
        }
        if (player.NativeScriptMovement is { Active: true })
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
            Scene.PartyTraceMovementPending = true;
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
            if (fiber.WaitReason == WaitReason.MinimalMenu)
            {
                Scene.MinimalMenuWaits.Remove(fiber.Sequence);
            }
            if (fiber.WaitReason == WaitReason.MinimalBattle)
            {
                Scene.NativeBattleWaits!.Remove(fiber.Sequence);
            }
            fiber.WaitReason = WaitReason.None;
            fiber.Request = null;
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
            string? unsupportedContext = UnsupportedRequestContext(fiber, operation);
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
            if (fiber.Complete || fiber.WaitReason != WaitReason.None || transitionRequested)
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

    private void StepVisualTransitions()
    {
        if (colorEndFrame > 0)
        {
            float amount = Math.Clamp((Frame - colorStartFrame) / (float)Math.Max(1, colorEndFrame - colorStartFrame), 0, 1);
            Vector3 color = Vector3.Lerp(colorStart, colorEnd, amount);
            if (subtractColor)
            {
                Scene.SubtractiveColor = color;
            }
            else
            {
                Scene.AdditiveColor = color;
            }
        }
        if (fadeEndFrame > 0 && Scene.FadeMode216 != 0)
        {
            float amount = Math.Clamp((Frame - fadeStartFrame) / (float)Math.Max(1, fadeEndFrame - fadeStartFrame), 0, 1);
            Scene.FadeOpacity = fadeStart + (fadeEnd - fadeStart) * amount;
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
        public NativeOperandStack Stack { get; } = new();
        public Stack<string> StackExpressions { get; } = new();
        public string[] LastExpressions = [];
        public Dictionary<int, NativeScalarValue> Results { get; } = [];
        public WaitReason WaitReason;
        public long WaitUntil;
        public Fiber? Request;
        public int DialogueChannel = -1;
        public int BackgroundId;
        public int SoundResourceId;
        public byte? NativeDoorSlot;
        public byte? NativeActorSlot;
    }

    private bool WaitReady(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        return fiber.WaitReason switch
        {
            WaitReason.SoundResource => SoundResourceReady(fiber),
            WaitReason.FaceDirection => actor.FaceProgress >= actor.FaceDuration,
            WaitReason.PartyJoin => true,
            WaitReason.PartySplit => true,
            WaitReason.NativeScriptMovement => true,
            WaitReason.NativeJump => true,
            WaitReason.WorldJump => false,
            WaitReason.MinimalMenu => Frame >= fiber.WaitUntil,
            WaitReason.MinimalBattle => Frame >= fiber.WaitUntil,
            WaitReason.None => true,
            WaitReason.Frames => Frame >= fiber.WaitUntil,
            WaitReason.Animation => !actor.AnimationPlaying || actor.AnimationLoop,
            WaitReason.Movement => actor.MovementTarget is null,
            WaitReason.MoveActor => actor.MovementActorId is null,
            WaitReason.Rotation => actor.RotationTarget is null,
            WaitReason.Dialogue => !dialogues.ContainsKey(fiber.DialogueChannel),
            WaitReason.Movie => !services.IsMoviePlaying,
            WaitReason.Color => Frame >= colorEndFrame,
            WaitReason.Fade => Frame >= fadeEndFrame,
            WaitReason.BackgroundAnimation => !backgroundAnimations.TryGetValue(fiber.BackgroundId, out BackgroundAnimation? animation) || animation.Complete,
            WaitReason.BackgroundScroll => BackgroundScrollWaitReady(fiber),
            WaitReason.RequestStart => fiber.Request?.Started == true,
            WaitReason.RequestEnd => fiber.Request?.Complete == true,
            _ => throw Fault(fiber, "Invalid saved wait reason.")
        };
    }

    private int NextRandom()
    {
        randomState = unchecked(randomState * 1664525u + 1013904223u);
        return (int)(randomState >> 16) & 255;
    }
}

public sealed class EventScriptException(string message, Exception? inner = null) : Exception(message, inner);
