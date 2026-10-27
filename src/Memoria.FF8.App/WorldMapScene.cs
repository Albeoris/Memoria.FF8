using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

/// <summary>Original normal-foot world component with bounded native field reentry and complete saved-foot entry62 reload. Object collision, encounters and idle gestures remain unsupported.</summary>
internal sealed partial class WorldMapScene : IDisposable
{
    public const double SimulationFps = ModelExchange.WorldFramesPerSecond;
    private const double NativeAngleUnit = 0.0015339808305725455;
    private const double NativeAtanScale = 651.8986206054688;
    private GameSession session;
    private readonly WorldAssets assets;
    private readonly IReadOnlyDictionary<int, int> replacements;
    private readonly byte storyByte;
    private WorldTerrainRenderer? renderer;
    private readonly WorldPresentation presentation = new();
    private bool disposed;
    private bool rebound;
    public Func<WorldFieldSelection, WorldState, short, NativeWorldSalaryState, Action>? PrepareFieldEntry { get; set; }
    public Func<NativeSalaryResult, NativeSalaryPresentation?, NativeGlobalSoundProcess?, NativeSalaryEffects>? PrepareSalaryEffects { get; set; }
    public Action<NativeSalaryEffects>? PublishSalaryEffects { get; set; }
    public Func<WorldState, int, RandomEncounterSessionState?, (WorldState World, RandomEncounterSessionState? Encounters)>? ProcessRandomEncounters { get; set; }
    private int encounterMovementMagnitude;
    public Func<bool>? HasPendingBattle { get; set; }
    public WorldState? LastDeparture { get; private set; }

    public WorldMapScene(GameSession session, string gameDirectory, ResourceService? resources = null, NativeSavedWorldFootReturn? battleReload = null, IModelOverrideSource? modelOverrides = null)
    {
        this.session = session;
        if (session.State.Scene != SceneKind.WorldMap || session.State.WorldArrival is not WorldTransition arrival)
        {
            throw new InvalidOperationException("A world scene requires its original field-to-world transition.");
        }
        bool battleMode2 = battleReload is not null || session.State.World?.NativeFootRuntime?.Rule == NativeFootWorldRuntimeState.BattleReloadRule;
        battleReload ??= battleMode2 ? session.State.World?.NativeFootRuntime is { Rule: NativeFootWorldRuntimeState.BattleReloadRule } ? session.State.SavedWorld?.FootReturn : null : null;
        bool parkedCarFoot = session.State.World?.NativeFootRuntime?.ParkedCar is not null;
        bool car = !battleMode2 && !parkedCarFoot && unchecked((byte)arrival.Mode) is 32 or 33 or 35;
        bool garden = !battleMode2 && (unchecked((byte)arrival.Mode) == 48 || session.State.World is { NativeGardenRuntime: not null, NativeFootRuntime: null });
        bool ragnarok = !battleMode2 && unchecked((byte)arrival.Mode) == 50;
        bool stationFoot = !battleMode2 && session.State.World?.NativeFootRuntime?.StationTrains is not null;
        bool ridingTrain = IsRidingTrainPacket(arrival) && !stationFoot;
        bool scripted = !battleMode2 && (IsSupportedScriptedPacket(arrival) || ridingTrain);
        byte footEntityType = battleMode2 ? battleReload?.EntityType105 ?? session.State.World!.NativeFootRuntime!.EntityType : stationFoot || parkedCarFoot ? session.State.World!.NativeFootRuntime!.EntityType : unchecked((byte)arrival.Mode);
        if (!car && !garden && !ragnarok && !scripted && session.State.World is { } restoredFoot)
        {
            NativeFootWorldRuntimeState runtime = restoredFoot.NativeFootRuntime ?? throw new NotSupportedException("Native foot restoration requires executed entity-type loader authority.");
            if (arrival.EntryIndex == 62 && runtime.Rule is not (NativeFootWorldRuntimeState.SavedReloadRule or NativeFootWorldRuntimeState.BattleReloadRule))
            {
                throw new NotSupportedException("Historical entry62 world state has no proved native foot-reload authority.");
            }
        }
        if (footEntityType is not (0 or 1 or 2) && !car && !garden && !ragnarok && !scripted)
        {
            throw new NotSupportedException($"World entity type {unchecked((sbyte)arrival.Mode)} has no implemented original model/control path.");
        }
        int entry = unchecked((sbyte)arrival.EntryIndex);
        if (session.State.SavedWorld is null)
        {
            throw new NotSupportedException("Native saved-world provenance is unknown; original vehicle state must be established before world use.");
        }
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        assets = new WorldAssets(gameDirectory, resources, modelOverrides);
        try
        {
            bool ordinaryFoot = !parkedCarFoot && !scripted && !stationFoot && !car && !garden && !ragnarok && footEntityType == 0;
            NativeWorldAllocationClause? footClause = ordinaryFoot ? assets.ReadWorldAllocationClause(inputs.StoryWord) : null;
            //544860 establishes vehicle indexes from the selected clause's objects; loader-initialized pose2/3 do not establish vehicle presence.
            bool parkedRagnarok = footClause?.ContainsShape(1) == true;
            bool parkedGarden = !parkedRagnarok && footClause?.ContainsShape(64) == true && footClause.ContainsShape(65);
            if (!scripted && !stationFoot && !car && !garden && !ragnarok && !parkedGarden && !parkedRagnarok && !parkedCarFoot && (footEntityType == 2 ? inputs.StoryWord is < 2140 or >= 2500 : footEntityType == 1 ? inputs.StoryWord is < 34 or >= 205 : inputs.StoryWord is >= 34 and < 205 or >= 392))
            {
                throw new NotSupportedException("World entry story lies outside this entity's proved native object/contact clause.");
            }
            NativeSavedWorldFootReturn? reload = null;
            NativeFootWorldRuntimeState? initialFootRuntime = null;
            NativeCarWorldRuntimeState? initialCarRuntime = null;
            NativeSavedWorldState? carSavedDeparture = null;
            if (scripted)
            {
                if (session.State.World is not null && session.State.World.NativeScriptedRuntime is null && session.State.World.NativeTrainRuntime is null)
                {
                    throw new NotSupportedException("Native scripted restoration has no executed scripted runtime authority.");
                }
            }
            else if (car)
            {
                if (entry == 62 || unchecked((sbyte)arrival.Extra) != -1)
                {
                    throw new NotSupportedException("Native car saved-position or scripted-route entry is outside the ordinary car contract.");
                }
                if (session.State.World is null)
                {
                    carSavedDeparture = session.State.SavedWorld;
                    session.SetSavedWorldState(session.State.SavedWorld.WithOrdinaryCarEntry(unchecked((byte)arrival.Mode)));
                }
                else if (session.State.World.NativeCarRuntime is null)
                {
                    throw new NotSupportedException("Native car restoration has no executed vehicle runtime authority.");
                }
            }
            else if (!garden && !ragnarok && !parkedRagnarok && session.State.World is null)
            {
                RequireUnaliasedFootAuxiliary(session.State);
                if (battleReload is not null)
                {
                    battleReload.RequireCompleteFootReload();
                    if (session.State.SavedWorld.FootReturn != battleReload)
                    {
                        throw new InvalidDataException("World battle reload differs from its actual saved physical foot owner.");
                    }
                    reload = battleReload;
                    initialFootRuntime = NativeFootWorldRuntimeState.CreateBattleReload(battleReload);
                }
                else if (entry == 62)
                {
                    reload = session.State.SavedWorld.FootReturn ?? throw new NotSupportedException("World entry62 requires an executed native foot-save receipt.");
                    if (reload.EntityType105 != footEntityType)
                    {
                        throw new NotSupportedException("Native foot reload changes the packet entity type outside this implemented loader contract.");
                    }
                    initialFootRuntime = NativeFootWorldRuntimeState.CreateSavedReload(reload);
                }
                else
                {
                    initialFootRuntime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(session.State.SavedWorld, footEntityType);
                }
                initialFootRuntime.RequireSupportedNormalFoot();
            }
            else if (!garden && !ragnarok && !parkedRagnarok)
            {
                NativeFootWorldRuntimeState runtime = session.State.World!.NativeFootRuntime ?? throw new NotSupportedException("Native foot restoration requires executed entity-type loader authority.");
                runtime.RequireSupportedNormalFoot();
                if (runtime.EntityType != footEntityType)
                {
                    throw new InvalidDataException("The restored native foot entity type differs from its world packet.");
                }
            }
            storyByte = inputs.StoryByte;
            if (parkedRagnarok)
            {
                replacements = WorldTerrainSelection.Replacements(session.State.World?.ReplacementMask ?? WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte));
                InitializeParkedRagnarokFoot(arrival, inputs, battleReload);
                return;
            }
            if (garden)
            {
                replacements = WorldTerrainSelection.Replacements(session.State.World?.ReplacementMask ?? WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte));
                InitializeGarden(arrival, inputs);
                return;
            }
            if (ragnarok)
            {
                replacements = WorldTerrainSelection.Replacements(session.State.World?.ReplacementMask ?? WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte));
                InitializeRagnarok(arrival, inputs);
                return;
            }
            if (scripted)
            {
                if (session.State.World is null)
                {
                    session.SetWorldState(ridingTrain ? CreateTrainRide(arrival, inputs) : CreateScriptedState(arrival, inputs));
                }
                replacements = WorldTerrainSelection.Replacements(State.ReplacementMask);
                if (ridingTrain)
                {
                    ValidateTrainRide(State.NativeTrainRuntime!);
                }
                else
                {
                    ValidateScriptedState(State);
                }
                return;
            }
            if (session.State.World is null)
            {
                if (reload is null && entry != 62 && (uint)entry >= assets.Spawns.Count)
                {
                    throw new InvalidDataException($"Original world spawn {entry} is absent.");
                }
                WorldSpawn spawn = reload is not null || entry == 62 ? default : assets.Spawns[entry];
                int x = reload?.Pose0.X ?? spawn.X;
                int z = reload?.Pose0.RawZ ?? spawn.Z;
                int height = reload?.Pose0.Height ?? spawn.NativeHeight;
                ushort mask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte);
                replacements = WorldTerrainSelection.Replacements(mask);
                WorldGround? ground;
                if (car)
                {
                    WorldGroundSelection initialGround = assets.LookupNativeGround(x, z, replacements, []) ?? throw new NotSupportedException("Original car spawn has no native ground lookup result.");
                    NativeWorldProcess carProcess = session.State.WorldProcess ?? throw new NotSupportedException("Native car loader requires its executed process camera-follow owner.");
                    carProcess.Validate();
                    byte carType = unchecked((byte)arrival.Mode);
                    NativeWorldFootCameraState? carProjection = carType is 32 or 35 ? new NativeWorldFootCameraState(x, z, height, x, z, height - 256, (spawn.Unknown & 255) * 16, -112, -341, -7168) : null;
                    initialCarRuntime = NativeCarWorldRuntimeState.Create(carSavedDeparture!, CreateFreshGroundCache(initialGround.Identity), carProcess, carType, carType is 32 or 35 ? assets.ReadTrainWorldAllocation(inputs.StoryWord, inputs.EventFlags265) : null, carProjection);
                    ground = initialGround.Ground;
                }
                else
                {
                    WorldGroundSelection initialGround = assets.LookupNativeGround(x, z, replacements, []) ?? throw new NotSupportedException("Original foot spawn has no native53EB80 ground lookup result.");
                    NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Native forest loader requires the executed main-loop/camera process history; migrate this save explicitly.");
                    initialFootRuntime = initialFootRuntime! with { GroundCache = CreateFreshGroundCache(initialGround.Identity), PreviousHorizontalInput = PreviousFootHorizontalInput(process, worldLoad: true), Forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown) with { CameraPitch = initialFootRuntime.AnimationVariant == 0 ? (short)-112 : (short)-512 } };
                    ground = initialGround.Ground;
                }
                WorldState initial = new()
                {
                    X = x,
                    Z = z,
                    NativeHeight = height,
                    CameraYaw = reload?.CameraYaw96 ?? (spawn.Unknown & 255) * 16,
                    BodyYaw = reload?.Pose0.Yaw ?? (spawn.Unknown >> 8) * 16,
                    CameraTargetX = x,
                    CameraTargetZ = z,
                    CameraTargetNativeHeight = height - 256,
                    GroundType = ground?.Triangle.GroundType,
                    ReplacementMask = mask,
                    NativeFootRuntime = initialFootRuntime,
                    NativeCarRuntime = initialCarRuntime,
                    EventUi = NativeWorldEventUi.Create()
                };
                if (!car)
                {
                    initial = parkedGarden ? InitializeParkedGarden(initial, arrival, inputs, unchecked((short)-(int)ground!.Value.Height), battleReload) : InitializeNormalFootObjects(initial, inputs);
                    if (!parkedGarden)
                    {
                        initial = initial with { NativeFootRuntime = initial.NativeFootRuntime! with { PriorTerrainOcclusion = ReadFootTerrainOcclusion(initial, initial) } };
                    }
                }
                EnsureContactCertificate(initial);
                session.SetWorldState(initial);
            }
            else
            {
                replacements = WorldTerrainSelection.Replacements(State.ReplacementMask);
                if (!car)
                {
                    State.NativeFootRuntime!.RequireNativeGroundState();
                    assets.ValidateNativeGroundCache(State.NativeFootRuntime.GroundCache!, replacements);
                }
                if (car)
                {
                    State.NativeCarRuntime!.Validate();
                    assets.ValidateNativeGroundCache(State.NativeCarRuntime!.GroundCache!, replacements);
                }
                if (parkedGarden)
                {
                    ValidateParkedGarden(State, arrival, inputs, battleReload);
                }
                EnsureContactCertificate(State);
                if (State.NativeFootRuntime?.NormalObjects is not null)
                {
                    ValidateNormalFootObjects(State, true);
                }
                if (stationFoot)
                {
                    ValidateStationTrainResources(State.NativeFootRuntime!);
                }
            }
        }
        catch
        {
            assets.Dispose();
            throw;
        }
    }

    public short PlaceId => assets.GetNativeMapLocationId(State.X, State.Z);
    public WorldState State => session.State.World ?? LastDeparture ?? throw new InvalidOperationException("This world scene no longer has a current state.");
    internal string AllocationProgramSha256 => assets.AllocationProgramSha256;
    internal NativeWorldEncounterDefinition? ReadRandomEncounterDefinition(WorldState state) => assets.ReadRandomEncounterDefinition(state.X, state.Z, state.GroundType ?? throw new NotSupportedException("Encounters require native ground."), session.State.DiscProcess!.SavedDisc204);
    public bool IsPresentationDeferred => State.NativeScriptedRuntime?.Path.Callback == 0 || State.NativeTrainRuntime?.Callback == 0;

    internal static void RequireUnaliasedFootAuxiliary(GameState state)
    {
        // Event base1CFE9B8 aliases saved-world base1CFEEB8 at +108/+110.
        // Reject every typed cell whose physical span overlaps either saved word.
        if (state.EventMemory.Cells.Any(pair => pair.Key < 1392 && (long)pair.Key + pair.Value.Width > 1388))
        {
            throw new NotSupportedException("Native saved-world foot auxiliary bytes overlap an generic event-memory cell.");
        }
    }

    internal void ValidatePreparedSession(GameState candidate)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (rebound || candidate.Scene != SceneKind.WorldMap || candidate.WorldArrival != session.State.WorldArrival || candidate.World != State || candidate.SavedWorld != session.State.SavedWorld || !System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(candidate.WorldEntryMemory), System.Text.Json.JsonSerializer.SerializeToElement(session.State.WorldEntryMemory)) || !System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(candidate.EventMemory), System.Text.Json.JsonSerializer.SerializeToElement(session.State.EventMemory)))
        {
            throw new InvalidOperationException("The committed world session does not match its unused prepared entry.");
        }
    }

    internal void RebindPreparedSession(GameSession committedSession)
    {
        ArgumentNullException.ThrowIfNull(committedSession);
        ValidatePreparedSession(committedSession.State);
        session = committedSession;
        rebound = true;
    }

    public PlayerActionResult Apply(PlayerAction action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(action);
        if (action.Frames is < 1 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(action), "World action duration must be between one and 30000 simulation frames.");
        }
        if (action.Kind == PlayerActionKind.SwitchPartyMembers)
        {
            NativePartySwitchInput.Validate(action);
            NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Switch requires the saved character owner.");
            byte entry = session.State.WorldControls?.Menu209 ?? 0;
            if (HasPendingBattle?.Invoke() == true || CurrentDialogue is not null || !NativePartySwitchInput.CanExchange(vitals, action.Value, action.Quantity!.Value, entry))
            {
                return new PlayerActionResult(false, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0, "The current world menu does not permit this party-member exchange.");
            }
            session.SetCharacterVitals(NativePartySwitchInput.Apply(vitals, action, entry));
            return new PlayerActionResult(true, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0);
        }
        if (NativeMagicMenuInput.IsMenuAction(action.Kind))
        {
            return ApplyMagicMenuAction(action);
        }
        if (NativeJunctionMenuInput.IsMenuAction(action.Kind))
        {
            return ApplyJunctionMenuAction(action);
        }
        if (HasPendingBattle?.Invoke() == true)
        {
            return new PlayerActionResult(false, new PlayerTrigger(PlayerTriggerKind.Battle, session.State.FieldId, State.Frame, null, null), 0, "A random battle owns this world scene.");
        }
        if (!float.IsFinite(action.Horizontal) || !float.IsFinite(action.Vertical) || !float.IsFinite(action.CameraHorizontal) || Math.Abs(action.Horizontal) > 1 || Math.Abs(action.Vertical) > 1 || Math.Abs(action.CameraHorizontal) > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Movement axes must be finite and between -1 and 1.");
        }
        if (action.CameraHorizontal != 0 && action.Kind is not (PlayerActionKind.Move or PlayerActionKind.Wait or PlayerActionKind.WorldButtons))
        {
            throw new ArgumentException("Manual world camera input requires an ordinary Move or Wait action.", nameof(action));
        }
        DialogueState? sourceDialogue = CurrentDialogue;
        bool stationChoice = action.Kind == PlayerActionKind.Choose && sourceDialogue is { FirstChoice: not null } && State.NativeTrainRuntime is not null;
        if (stationChoice && action.Value is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Native world station choices use visible lines1 and2.");
        }
        bool worldMessageConfirm = sourceDialogue is { CanConfirm: true, FirstChoice: null } && action.Kind == PlayerActionKind.Confirm;
        bool gardenAction = (State.NativeGardenRuntime is not null || State.NativeRagnarokRuntime is not null || State.NativeCarRuntime is not null || State.NativeFootRuntime?.ParkedCar is not null) && action.Kind == PlayerActionKind.Confirm;
        bool ragnarokButtons = State.NativeRagnarokRuntime is not null && State.NativeFootRuntime is null && action.Kind == PlayerActionKind.WorldButtons;
        bool carButtons = (State.NativeCarRuntime is not null || State.NativeFootRuntime?.ParkedCar is not null) && action.Kind == PlayerActionKind.WorldButtons;
        bool gardenButtons = State.NativeGardenRuntime is not null && State.NativeFootRuntime is null && action.Kind == PlayerActionKind.WorldButtons;
        bool vehicleButtons = ragnarokButtons || carButtons || gardenButtons;
        if (action.WorldButtons != 0 && (action.Kind is not (PlayerActionKind.Move or PlayerActionKind.Wait or PlayerActionKind.WorldButtons) || (action.WorldButtons & ~(State.NativeFootRuntime?.ParkedCar is not null ? 0x142 : 0x102)) != 0))
        {
            throw new ArgumentException("Ordinary world button levels support camera and map display controls on Move or Wait actions.", nameof(action));
        }
        if (vehicleButtons && action.Value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Native world digital button masks require an unsigned WORD.");
        }
        if (vehicleButtons && (action.Value & 0x800) != 0)
        {
            throw new NotSupportedException("The ordinary world menu button requires its native menu transition and buffered input mutation owner.");
        }
        if (State.NativeRagnarokRuntime is not null && State.NativeFootRuntime is null && action.Kind == PlayerActionKind.Move && (action.Horizontal is not (-1 or 0 or 1) || action.Vertical is not (-1 or 0 or 1)))
        {
            throw new NotSupportedException("Ragnarok Move adapts only digital throttle/steering; submit WorldButtons for exact ordinary vertical controls.");
        }
        bool accepted = action.Kind is PlayerActionKind.Move or PlayerActionKind.Wait || stationChoice || gardenAction || vehicleButtons || worldMessageConfirm;
        int advanced = 0;
        WorldState candidate = State;
        WorldState previousCallback = candidate;
        NativeWorldSalaryState salary = NativeWorldSalaryState.Begin(session.State);
        List<GlobalSoundRequest> salaryRequests = [];
        WorldFieldSelection? fieldEntry = null;
        if (accepted)
        {
            for (int index = 0; index < action.Frames; index++)
            {
                salary = salary.BeginWorldEventCallback();
                bool advanceUi = candidate.NativeScriptedRuntime?.Path.EndpointRequested != true;
                if (advanceUi && candidate.NativeCarRuntime is NativeCarWorldRuntimeState car)
                {
                    salary = salary.PrepareCarVibration(car.EntityType, assets.VibrationBank);
                }
                int priorGraphics = salary.VibrationProcess?.GraphicsSpeed20409F0 ?? throw new NotSupportedException("Native world movement requires its canonical retained graphics magnitude.");
                bool gardenFootAtCallback = candidate.NativeGardenRuntime is not null && candidate.NativeFootRuntime is not null;
                bool ragnarokFootAtCallback = candidate.NativeRagnarokRuntime is not null && candidate.NativeFootRuntime is not null;
                uint buttons = vehicleButtons ? unchecked((uint)action.Value) : (gardenAction || worldMessageConfirm) && index == 0 ? 0x40u : 0;
                buttons |= action.WorldButtons;
                if (action.Kind == PlayerActionKind.Move)
                {
                    buttons |= action.Vertical == -1 ? 0x80u : action.Vertical == 1 ? 0x10u : 0;
                    buttons |= action.Horizontal == -1 ? 0x2000u : action.Horizontal == 1 ? 0x8000u : 0;
                }
                byte inputScriptMode = salary.EventUi?.ScriptMode ?? throw new NotSupportedException("World controls require the preceding script mode.");
                salary = salary.WithWorldInputSample(buttons);
                candidate = PrepareMapDisplay(candidate, salary, out byte mapScriptMode);
                salary = salary.WithWorldScriptMode(mapScriptMode);
                candidate = candidate with { EventUi = salary.EventUi };
                (float horizontal, float vertical) = carButtons || gardenButtons ? ControllerGameplayInput.ReadVehicleAxes(buttons, action.Horizontal, action.Vertical) : (action.Kind == PlayerActionKind.Move ? action.Horizontal : 0, action.Kind == PlayerActionKind.Move ? action.Vertical : 0);
                if (inputScriptMode != 0)
                {
                    horizontal = 0;
                    vertical = 0;
                }
                WorldState callbackStart = candidate;
                previousCallback = candidate;
                candidate = Step(candidate, horizontal, vertical, inputScriptMode != 0 ? 0 : action.CameraHorizontal, priorGraphics, buttons, salary.WorldEntryMemory, ref salary, out bool consumeGardenRequest, out NativeGardenWorldObject? boardingTarget, out fieldEntry, out uint movementUnits, out int graphicsSpeed, mapOnly: mapScriptMode == 12, motorButtons: inputScriptMode == 0 ? buttons : 0);
                if (consumeGardenRequest)
                {
                    salary = salary.ConsumeGardenScriptRequest();
                }
                salary = salary.WithGraphicsSpeed(graphicsSpeed);
                WorldState beforeToggle = candidate;
                candidate = PrepareFootCameraToggle(candidate, salary.InputHistory);
                if (!ReferenceEquals(beforeToggle, candidate))
                {
                    WorldEntryVariables variables = WorldEntryVariables.Read(session.State);
                    salary = salary.WithFootCameraToggle(candidate, checked((ushort)session.State.FieldId), variables.StoryWord, assets.GetNativeMapLocationId(candidate.X, candidate.Z));
                }
                if (candidate.NativeCarRuntime is { } fuelCar)
                {
                    salary = salary.PrepareCarFuel(fuelCar.EntityType, candidate.GroundType ?? throw new NotSupportedException("Native car fuel requires its actual current terrain type."), graphicsSpeed, PrepareFuelMessage);
                    salary = salary.AdvanceCarCameraCooldown(ReadSignedMovementComponent(-vertical) != 0, ReadSignedMovementComponent(-horizontal) != 0);
                    if (candidate.NativeCarRuntime!.Forest is { } carForest)
                    {
                        candidate = candidate with { NativeCarRuntime = candidate.NativeCarRuntime with { Forest = carForest with { CameraCooldown = salary.CarCameraCooldown!.Value } } };
                    }
                }
                else
                {
                    salary = salary.AdvanceFuelMessage(graphicsSpeed);
                }
                WorldState? carTransfer = mapScriptMode != 12 ? PrepareCarDisembark(candidate, salary) ?? PrepareCarReboard(candidate, salary) : null;
                if (advanceUi && (callbackStart.NativeRagnarokRuntime is null || ragnarokFootAtCallback) && (callbackStart.NativeGardenRuntime is null || callbackStart.NativeFootRuntime is not null))
                {
                    byte scriptMode = ragnarokFootBoardingTarget is not null ? (byte)6 : callbackStart.NativeScriptedRuntime?.Path.Mode ?? (callbackStart.NativeTrainRuntime is not null ? mapScriptMode : (byte)0);
                    //54A7F0 sets2040A40 before54D7E0 for either admitted boarding action; Garden leaves2036B70 unchanged.
                    bool? activity = carTransfer is not null || boardingTarget is not null || ragnarokFootBoardingTarget is not null ? true : null;
                    fieldEntry = SelectWorldCallback(candidate, callbackStart, scriptMode, ref salary, buttons, activity) ?? fieldEntry;
                }
                if (carTransfer is not null && fieldEntry is null)
                {
                    salary = salary.WithCarTransferCooldown(carTransfer, candidate.NativeFootRuntime?.Forest?.CameraCooldown);
                    candidate = carTransfer;
                }
                if (stationChoice && fieldEntry is null)
                {
                    NativeTrainWorldRuntimeState train = candidate.NativeTrainRuntime ?? throw new InvalidDataException("Native station choice lost its held train owner.");
                    int station = TrainStation(train.Primary);
                    candidate = action.Value == 1 ? CreateTrainDismount(candidate, station) : (train with { Primary = train.Primary with { Pause = 0 }, StationDeclines = train.StationDeclines.Add(train.Callback) }).ToWorldState(candidate);
                    salary = salary.WithWorldScriptMode(candidate.NativeTrainRuntime is null ? (byte)0 : mapScriptMode);
                }
                if (mapScriptMode != 12 && candidate.NativeScriptedRuntime is null && candidate.NativeTrainRuntime is null)
                {
                    bool currentOcclusion = candidate.NativeFootRuntime is null ? false : candidate.NativeGardenRuntime?.PriorTerrainOcclusion ?? candidate.NativeRagnarokRuntime?.PriorTerrainOcclusion ?? ReadFootTerrainOcclusion(callbackStart, candidate);
                    if (candidate.NativeFootRuntime is not null)
                    {
                        candidate = candidate with { NativeFootRuntime = candidate.NativeFootRuntime with { PriorTerrainOcclusion = currentOcclusion } };
                    }
                    //54B460 follows station handling and runs558D70 in ordinary mode.
                    if (candidate.NativeFootRuntime?.CameraTransition is { } transition)
                    {
                        candidate = candidate with { NativeFootRuntime = transition.Step(candidate.NativeFootRuntime, currentOcclusion) };
                        NativeFootWorldRuntimeState transitionedFoot = candidate.NativeFootRuntime;
                        if (candidate.NativeGardenRuntime is { } parkedGarden)
                        {
                            candidate = candidate with { NativeGardenRuntime = parkedGarden with { AnimationVariant = transitionedFoot.AnimationVariant, Forest = transitionedFoot.Forest!, CameraPitch = transitionedFoot.Forest!.CameraPitch } };
                        }
                        if (candidate.NativeRagnarokRuntime is { } parkedRagnarok)
                        {
                            candidate = candidate with { NativeRagnarokRuntime = parkedRagnarok with { AnimationVariant = transitionedFoot.AnimationVariant, Forest = transitionedFoot.Forest } };
                        }
                    }
                }
                if (mapScriptMode != 12 && candidate.NativeCarRuntime is { CameraTransition: { } carCameraTransition } transitioningCar)
                {
                    NativeWorldForestState forest = transitioningCar.Forest!;
                    var cameraWords = carCameraTransition.StepWords(false, forest.CameraPitch, -341, -7168, transitioningCar.PriorTerrainOcclusion!.Value, false);
                    candidate = candidate with { NativeCarRuntime = transitioningCar with { CameraTransition = cameraWords.Transition, Forest = forest with { CameraPitch = cameraWords.Pitch } } };
                }
                if (candidate.NativeScriptedRuntime is null && candidate.NativeTrainRuntime is null)
                {
                    //6519D0 still consumes the retained accepted magnitude when script12 skips the position query.
                    salary = salary.PrepareDrawPointAging(movementUnits);
                }
                NativeSalaryResult? salaryResult = candidate.NativeScriptedRuntime is null && candidate.NativeTrainRuntime is null ? GameSession.SampleWorldSalary(session.State, salary.SeeD, salary.Economy, movementUnits) : null;
                NativeSalaryEffects effects;
                if (salaryResult?.Payment is not null)
                {
                    if (PrepareSalaryEffects is null || PublishSalaryEffects is null)
                    {
                        throw new NotSupportedException("Native world salary requires its original UI and global sound producer before commit.");
                    }
                    effects = PrepareSalaryEffects(salaryResult, salary.Presentation, salary.SoundProcess);
                }
                else
                {
                    NativeSalaryPresentation? presentation = salary.Presentation;
                    if (salaryResult?.CloseDisplay == true)
                    {
                        presentation = presentation?.Close() ?? throw new NotSupportedException("Native world salary closure requires its actual retained UI process.");
                    }
                    effects = new NativeSalaryEffects(presentation, salary.SoundProcess, []);
                }
                //53FAC0 calls49A970 after ordinary body update; its already-requested departure fade returns before that call.
                salary = salary.Advance(salaryResult, effects.Presentation?.Advance(advanceUi ? 2 : 0), effects.SoundProcess, movementUnits, advanceUi ? 2 : 0);
                salaryRequests.AddRange(effects.Requests);
                advanced++;
                if (mapScriptMode != 12 && fieldEntry is null && ProcessRandomEncounters is not null && (candidate.NativeRagnarokRuntime is null || ragnarokFootAtCallback) && (candidate.NativeGardenRuntime is null || gardenFootAtCallback))
                {
                    var encounterResult = ProcessRandomEncounters(candidate, encounterMovementMagnitude, salary.RandomEncounters);
                    candidate = encounterResult.World;
                    salary = salary with { RandomEncounters = encounterResult.Encounters };
                }
                if (mapScriptMode != 12 && fieldEntry is null && salary.RandomEncounters?.Pending is null && gardenFootAtCallback)
                {
                    candidate = StepGardenCactuar(candidate, out fieldEntry);
                }
                if (mapScriptMode != 12 && fieldEntry is null && salary.RandomEncounters?.Pending is null && candidate.NativeFootRuntime?.EntityType == 2)
                {
                    candidate = StepFootType2Cactuar(candidate, out fieldEntry);
                }
                if (mapScriptMode != 12 && fieldEntry is null && salary.RandomEncounters?.Pending is null && ragnarokFootAtCallback)
                {
                    candidate = StepRagnarokCactuar(candidate, out fieldEntry);
                }
                if (boardingTarget is not null)
                {
                    if (fieldEntry is not null || salary.RandomEncounters?.Pending is not null)
                    {
                        throw new NotSupportedException("Simultaneous Garden reboarding and field/battle departure requires the native pre-transfer saved-pose transaction.");
                    }
                    candidate = CompleteGardenReboarding(candidate, boardingTarget);
                }
                if (ragnarokFootBoardingTarget is not null)
                {
                    if (fieldEntry is null && salary.RandomEncounters?.Pending is null)
                    {
                        candidate = CompleteRagnarokFootReboarding(candidate);
                    }
                    else
                    {
                        //54D7E0 can select the original first-boarding cabin
                        //before late54B460; save the real pre-takeoff foot/ship owners.
                        ragnarokFootBoardingTarget = null;
                    }
                }
                candidate = candidate with { EventUi = salary.EventUi };
                if (fieldEntry is not null || salary.RandomEncounters?.Pending is not null || candidate.NativeTrainRuntime?.Primary.Pause == -1 || stationChoice || candidate.EventUi?.Windows.Length > 0)
                {
                    break;
                }
            }
        }
        if (fieldEntry is not null)
        {
            if (PrepareFieldEntry is null)
            {
                throw new NotSupportedException($"Original world field entry {fieldEntry.Entry.Index}/field{fieldEntry.Entry.FieldId} has no prepared field backend.");
            }
            Action commit = PrepareFieldEntry(fieldEntry, candidate, assets.GetNativeMapLocationId(candidate.X, candidate.Z), salary);
            commit();
            PublishSalaryEffects?.Invoke(new NativeSalaryEffects(salary.Presentation, salary.SoundProcess, salaryRequests));
            LastDeparture = candidate;
            PlayerActionResult departed = new(true, new PlayerTrigger(PlayerTriggerKind.FieldTransition, fieldEntry.Entry.FieldId, candidate.Frame, null, null), advanced);
            return departed;
        }
        if (accepted)
        {
            session.CommitWorldAction(candidate, salary);
            if (advanced > 0)
            {
                presentation.Commit(previousCallback, State, PrepareRenderFrame);
            }
            PublishSalaryEffects?.Invoke(new NativeSalaryEffects(salary.Presentation, salary.SoundProcess, salaryRequests));
        }
        else
        {
            session.SetWorldState(candidate);
        }
        PlayerTrigger trigger = session.State.RandomEncounters?.Pending is not null ? new(PlayerTriggerKind.Battle, session.State.FieldId, State.Frame, null, null) : CurrentDialogue is { } dialogue ? new(PlayerTriggerKind.Dialogue, session.State.FieldId, State.Frame, dialogue.MessageId, dialogue.Text) : new(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null);
        PlayerActionResult result = new(accepted, trigger, advanced, accepted ? null : "This original world action is not implemented.");
        return result;

    }

    public byte[] Render(int width, int height, ITriangleRenderer? triangleRenderer = null, float interpolation = 1)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (renderer is null || renderer.Width != width || renderer.Height != height)
        {
            renderer = new WorldTerrainRenderer(assets, width, height);
        }
        renderer.TriangleRenderer = triangleRenderer;
        WorldRenderFrame frame = presentation.Sample(State, interpolation, PrepareRenderFrame);
        uint mainLoopCounter = session.State.WorldProcess?.MainLoopCounter ?? throw new NotSupportedException("World presentation requires the executed native main-loop counter.");
        frame = frame with { TerrainReplacementMask = State.ReplacementMask, MapDisplayMode = MapDisplayMode(State), MapProcessMode = State.EventUi?.ScriptMode ?? throw new NotSupportedException("World map presentation requires the current native process mode."), MapFrame = mainLoopCounter, NativeMainLoopCounter = mainLoopCounter, WorldDiscNumber = session.State.DiscProcess?.SavedDisc204, Shadows = CaptureShadows(State, frame) };
        return renderer.Render(frame, replacements);
    }

    private WorldRenderFrame PrepareRenderFrame(WorldState state)
    {
        if (state.NativeFootRuntime is { StationTrains: { } source, StationTrainProcess.ProjectionCamera: null })
        {
            return PrepareTrainFrame(source.ToWorldState(state));
        }
        if (state.NativeTrainRuntime is not null)
        {
            return PrepareTrainFrame(state);
        }
        if (state.NativeScriptedRuntime is not null)
        {
            return PrepareScriptedFrame(state);
        }
        if (state.NativeRagnarokRuntime is not null)
        {
            return PrepareRagnarokFrame(state);
        }
        if (state.NativeGardenRuntime is not null)
        {
            return PrepareGardenFrame(state);
        }
        EnsureContactCertificate(state);
        WorldCamera camera = FootCamera(state);
        NativeWorldFootCameraState? drawnCamera = state.NativeCarRuntime?.ProjectionCamera ?? state.NativeFootRuntime?.StationTrainProcess?.ProjectionCamera ?? state.NativeFootRuntime?.NormalObjects?.Camera;
        if (drawnCamera is not null)
        {
            //558D70 runs after drawing. Retain the actual projection for this
            //presented callback while the live words prepare the next one.
            camera = WorldCamera.Foot(drawnCamera.Yaw, 2048, projectionDistance: drawnCamera.ProjectionDistance) with { TargetPosition = new Vector3(drawnCamera.TargetX, -drawnCamera.TargetNativeHeight, drawnCamera.TargetRawZ), NativePitch = drawnCamera.Pitch, NativeOffsetY = drawnCamera.OffsetY, NativeOffsetZ = drawnCamera.OffsetZ };
        }
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        Vector3 position = new(state.X, -state.NativeHeight, state.Z);
        IReadOnlyList<NativeWorldStaticModelDraw>? models = state.NativeCarRuntime is NativeCarWorldRuntimeState car ? ReadCarModels(state, position, car) : state.NativeFootRuntime?.ParkedCar is not null ? ReadParkedCarModels(state) : state.NativeFootRuntime?.StationTrains is not null ? ReadStationTrainModels(state.NativeFootRuntime) : state.NativeFootRuntime?.NormalObjects is not null ? ReadNormalFootObjectModels(state) : null;
        return new WorldRenderFrame(position, state.BodyYaw * MathF.Tau / 4096, state.AnimationId, state.AnimationCounter >> 4, camera, sky, state.NativeCarRuntime is null && state.GroundType is null or > 5, AvatarModel: state.NativeCarRuntime is null ? assets.GetFootModel(state.NativeFootRuntime!.EntityType) : null, StaticModels: models, Forest: state.NativeFootRuntime?.Forest ?? state.NativeCarRuntime?.Forest);
    }

    private static int? PreviousFootHorizontalInput(NativeWorldProcess process, bool worldLoad = false)
    {
        if (process.InputHistory is not { } input)
        {
            return null;
        }
        ushort? buttons = worldLoad ? input.PreviousButtonsAfterWorldLoad : input.CurrentButtons;
        return buttons is null ? null : (buttons & 0x8000) != 0 ? 1 : (buttons & 0x2000) != 0 ? -1 : 0;
    }

    private NativeWorldGroundCacheState CreateFreshGroundCache(NativeWorldGroundIdentity ground)
    {
        NativeWorldGroundSelectionState? selection = session.State.WorldProcess?.GroundSelection;
        if (selection is null && session.State.DepartingWorldState is { } departure)
        {
            NativeWorldGroundCacheState? retained = departure.NativeFootRuntime?.GroundCache ?? departure.NativeCarRuntime?.GroundCache ?? departure.NativeTrainRuntime?.GroundCache ?? departure.NativeScriptedRuntime?.GroundCache ?? departure.NativeGardenRuntime?.GroundCache ?? departure.NativeRagnarokRuntime?.GroundCache;
            selection = retained is null ? null : NativeWorldGroundSelectionState.FromCache(retained);
        }
        return NativeWorldGroundCacheState.CreateFresh(ground, processSelection: selection);
    }

    private WorldState Step(WorldState before, float horizontal, float vertical, float cameraHorizontal, int priorGraphicsSpeed, uint buttons, NativeWorldEntryMemory? entryMemory, ref NativeWorldSalaryState salary, out bool consumeGardenRequest, out NativeGardenWorldObject? boardingTarget, out WorldFieldSelection? fieldEntry, out uint salaryMovementUnits, out int graphicsSpeed, bool mapOnly = false, uint? motorButtons = null)
    {
        encounterMovementMagnitude = 0;
        consumeGardenRequest = false;
        boardingTarget = null;
        if (before.NativeTrainRuntime is not null)
        {
            return StepTrainRide(before, horizontal, vertical, priorGraphicsSpeed, out fieldEntry, out salaryMovementUnits, out graphicsSpeed);
        }
        if (before.NativeScriptedRuntime is not null)
        {
            // Native53FAC0 does not call6519D0 for scripted entities16..22/65/66.
            salaryMovementUnits = 0;
            graphicsSpeed = before.NativeScriptedRuntime.Path.EndpointRequested ? priorGraphicsSpeed : 0;
            return StepScripted(before, buttons, out fieldEntry, (node, camera, origin) => NativeWorldPathProjectionCertificate.Create(camera, origin, before.NativeScriptedRuntime.Path.EntityType).ProjectNode(node));
        }
        if (before.NativeFootRuntime is null && (before.NativeCarRuntime is not null || before.NativeGardenRuntime is not null || before.NativeRagnarokRuntime is not null))
        {
            cameraHorizontal = NativeVehicleCamera.ReadManualCameraDirection(motorButtons ?? buttons, cameraHorizontal);
        }
        if (before.NativeCarRuntime is not null)
        {
            // Original5573C0 admits manual vehicle camera offset14 only for variant1.
            return StepCar(before, horizontal, vertical, cameraHorizontal, priorGraphicsSpeed, ref salary, out fieldEntry, out salaryMovementUnits, out graphicsSpeed, mapOnly);
        }
        if (before.NativeRagnarokRuntime is not null && before.NativeFootRuntime is null)
        {
            return StepRagnarok(before, cameraHorizontal, priorGraphicsSpeed, buttons, entryMemory!, ref salary, out consumeGardenRequest, out fieldEntry, out salaryMovementUnits, out graphicsSpeed, mapOnly, motorButtons ?? buttons);
        }
        if (mapOnly && before.NativeFootRuntime is not null)
        {
            return StepFoot(before, horizontal, vertical, cameraHorizontal, priorGraphicsSpeed, ref salary, out fieldEntry, out salaryMovementUnits, out graphicsSpeed, mapOnly: true, mapButtons: buttons);
        }
        if (before.NativeRagnarokRuntime is not null)
        {
            return StepRagnarokFoot(before, horizontal, vertical, cameraHorizontal, priorGraphicsSpeed, buttons, entryMemory!, ref salary, out consumeGardenRequest, out fieldEntry, out salaryMovementUnits, out graphicsSpeed);
        }
        if (before.NativeGardenRuntime is not null && before.NativeFootRuntime is null)
        {
            return StepGarden(before, horizontal, vertical, cameraHorizontal, priorGraphicsSpeed, buttons, entryMemory!, ref salary, out consumeGardenRequest, out fieldEntry, out salaryMovementUnits, out graphicsSpeed, mapOnly);
        }
        if (before.NativeGardenRuntime is not null)
        {
            return StepGardenFoot(before, horizontal, vertical, cameraHorizontal, priorGraphicsSpeed, buttons, entryMemory!, ref salary, out consumeGardenRequest, out boardingTarget, out fieldEntry, out salaryMovementUnits, out graphicsSpeed);
        }
        return StepFoot(before, horizontal, vertical, cameraHorizontal, priorGraphicsSpeed, ref salary, out fieldEntry, out salaryMovementUnits, out graphicsSpeed, mapOnly, mapButtons: buttons);
    }

    private WorldState StepFoot(WorldState before, float horizontal, float vertical, float cameraHorizontal, int priorGraphicsSpeed, ref NativeWorldSalaryState salary, out WorldFieldSelection? fieldEntry, out uint salaryMovementUnits, out int graphicsSpeed, bool mapOnly = false, uint mapButtons = 0)
    {
        int inputX = Math.Sign(horizontal);
        //53FAC0 seeds24 on the first normal loader callback and decrements before559140.
        //Frame is the actual callback age: callbacks1..23 retain the native zero-input hold.
        if (NativeWorldEntryControl.DelayAfterCallback(before.Frame + 1, 0) > 0)
        {
            horizontal = 0;
            vertical = 0;
            cameraHorizontal = 0;
        }
        fieldEntry = null;
        NativeFootWorldRuntimeState foot = before.NativeFootRuntime!;
        foot.RequireNativeGroundState();
        NativeWorldForestState forest = foot.Forest!;
        (int componentX, int componentZ) = ReadFootMovementComponents(horizontal, vertical);
        int localX = (16 * componentX) >> 6;
        int localZ = (16 * componentZ) >> 6;
        (int moveX, int graphicsMoveZ) = Rotate(localX, localZ, before.CameraYaw);
        bool moving = moveX != 0 || graphicsMoveZ != 0;
        int bodyYaw = before.BodyYaw;
        bool dampCamera = !moving;
        if (moving)
        {
            int desired = WrapYaw(NativeRound(Math.Atan2(-graphicsMoveZ, moveX) * NativeAtanScale) + 1024 + (foot.GroundYaw!.Value >> 1));
            int delta = YawDelta(desired, bodyYaw);
            if (Math.Abs(delta) <= 256)
            {
                bodyYaw = desired;
            }
            else
            {
                bodyYaw = WrapYaw(bodyYaw + (delta > 0 ? 512 : -512));
                dampCamera = true;
            }
        }
        // Native557A90 offset14 takes precedence over automatic follow/damping.
        int velocity = cameraHorizontal == 0 ? FollowCamera(before.CameraVelocity, YawDelta(bodyYaw, before.CameraYaw), dampCamera) : Math.Clamp(before.CameraVelocity + Math.Sign(cameraHorizontal) * 8, -128, 128);
        int cameraYaw = WrapYaw(before.CameraYaw + (velocity >> 3));
        int targetX = (before.X + NearWrapped(before.CameraTargetX, before.X, 262144)) >> 1;
        int targetZ = (before.Z + NearWrapped(before.CameraTargetZ, before.Z, 196608)) >> 1;
        int targetHeight = (3 * before.CameraTargetNativeHeight + before.NativeHeight - 256) >> 2;
        byte currentGroundType = before.GroundType ?? throw new NotSupportedException("Native forest camera/emission requires the current selected polygon type.");
        NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Native forest camera has no executed process history.");
        bool priorTerrainOcclusion = before.NativeRagnarokRuntime is { } parkedRagnarok ? parkedRagnarok.PriorTerrainOcclusion ?? throw new NotSupportedException("Parked Ragnarok foot camera lost its previous native terrain-render occlusion owner.") : before.NativeGardenRuntime is { } parkedGarden ? parkedGarden.PriorTerrainOcclusion ?? throw new NotSupportedException("Parked Garden foot camera lost its previous native terrain-render occlusion owner.") : foot.PriorTerrainOcclusion ?? throw new NotSupportedException("Native foot camera requires its previous terrain-render occlusion owner; migrate this save explicitly.");
        NativeForestCameraResult cameraStep = foot.AnimationVariant == 0 ? (process with { CameraCooldown = forest.CameraCooldown }).StepNormalFootCamera(forest.CameraPitch, currentGroundType, priorTerrainOcclusion, held8: moving) : new NativeForestCameraResult(forest.CameraPitch, process with { CameraCooldown = forest.CameraCooldown });
        graphicsSpeed = NativeWorldVectorMagnitude.Truncate(moveX, 0, graphicsMoveZ);
        if (mapOnly)
        {
            salaryMovementUnits = (uint)(foot.MovementMagnitude20409F4 ?? throw new NotSupportedException("Full map requires the preceding accepted movement magnitude; run a normal callback before using this historical save.")) / 8;
            foot = foot with { Forest = forest with { CameraPitch = cameraStep.Pitch, CameraCooldown = cameraStep.Process.CameraCooldown } };
            return CompleteMapFootMotor(before with { Frame = before.Frame + 1, BodyYaw = bodyYaw, CameraYaw = cameraYaw, CameraVelocity = velocity, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot }, mapButtons);
        }
        NativeWorldFootType2AnimationResult? type2Animation = null;
        if (foot.EntityType == 2)
        {
            foot = foot with { Forest = forest with { CameraPitch = cameraStep.Pitch, CameraCooldown = cameraStep.Process.CameraCooldown } };
            type2Animation = StepFootType2Draw(before, foot, bodyYaw, cameraYaw, targetX, targetZ, targetHeight, moving);
            forest = type2Animation.Forest;
        }
        int animation = type2Animation?.Animation ?? (moving ? 1 : 0);
        int animationCounter = type2Animation?.Counter ?? (animation == before.AnimationId && moving ? (before.AnimationCounter + 16) % (assets.GetFootModel(before.NativeFootRuntime!.EntityType).Animations[1].Frames.Count * 16) : 0);
        short particlePitch = before.NativeRagnarokRuntime?.Objects[0].Pitch ?? 0;
        short particleRoll = before.NativeRagnarokRuntime?.Objects[0].Roll ?? 0;
        NativeWorldWaterStepResult waterStep = forest.EmitFootWaterStep(currentGroundType, animation, animationCounter, salary.WaterStepEdges2045C7C, new(before.X, before.NativeHeight, -before.Z), particlePitch, checked((short)bodyYaw), particleRoll, assets.ForestTables.Effects);
        forest = waterStep.Forest;
        salary = salary.WithWaterStepEdges(waterStep.Edges);
        NativeWorldContactSpark? contactSpark = CaptureFootContactSpark(before, ref salary);
        forest = forest.Advance(currentGroundType, before.X, before.NativeHeight, before.Z, bodyYaw, priorGraphicsSpeed, assets.ForestTables.Random, assets.ForestTables.AllEffects, contactSpark) with { CameraPitch = cameraStep.Pitch, CameraCooldown = cameraStep.Process.CameraCooldown };
        foot = foot with { Forest = forest };
        if (foot.EntityType == 2 && NativeWorldEntryControl.DelayAfterCallback(before.Frame + 1, 0) == 0)
        {
            //53FAC0 calls54A7F0 after5472C0/550070 and before the ground query.
            foot = foot with { NormalObjects = foot.NormalObjects! with { ForcedAnimationC76628 = -1 } };
        }
        WorldState? gardenCameraState = null;
        WorldState? ragnarokCameraState = null;
        if (before.NativeGardenRuntime is not null)
        {
            gardenCameraState = PrepareGardenFootCallback(before with { BodyYaw = bodyYaw, CameraYaw = cameraYaw, CameraVelocity = velocity, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot });
            foot = gardenCameraState.NativeFootRuntime!;
        }
        if (before.NativeRagnarokRuntime is not null)
        {
            ragnarokCameraState = PrepareRagnarokFootCallback(before with { BodyYaw = bodyYaw, CameraYaw = cameraYaw, CameraVelocity = velocity, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot });
            foot = ragnarokCameraState.NativeFootRuntime!;
        }
        int x = WrapCoordinate(before.X + moveX, 262144);
        int z = WrapCoordinate(before.Z - graphicsMoveZ, 196608);
        if (foot.StationTrains is not null)
        {
            foot = StepStationTrains(before with { CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot });
        }
        else if (foot.NormalObjects is not null)
        {
            foot = StepNormalFootObjects(before with { CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot });
        }
        EnsureContactCertificate(before with { X = x, Z = z, CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot });
        int previousGroundHeight = -unchecked((short)(before.NativeHeight - (before.GroundType is <= 5 ? 320 : 0)));
        int? previousInput = foot.PreviousHorizontalInput ?? (session.State.WorldProcess is { } inputProcess ? PreviousFootHorizontalInput(inputProcess) : null);
        int? selectedGroup = inputX != 0 && previousInput != inputX ? inputX > 0 ? 0 : 1 : null;
        WorldNativeGroundResult ground = assets.ResolveNativeGround(before.X, before.Z, moveX, graphicsMoveZ, previousGroundHeight, selectedGroup, foot.GroundCache!, replacements, foot.EntityType, previousInput is null, foot.StationTrains is not null ? (candidateX, candidateZ, candidateHeight) => HasStationContact(foot, candidateX, candidateZ, candidateHeight) : foot.NormalObjects is not null ? (candidateX, candidateZ, candidateHeight) => HasNormalFootObjectContact(foot, candidateX, candidateZ, candidateHeight) : null, footprintContact: foot.ParkedCar is { } parkedCar ? (probes, padding) => CarTransferContact(before with { CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot }, parkedCar.Car, probes, foot.EntityType, padding, parkedCar) : ragnarokCameraState is not null ? (probes, padding) => RagnarokFootContact(ragnarokCameraState, probes, foot.EntityType, padding) : gardenCameraState is not null ? (probes, padding) => GardenFootContact(gardenCameraState, probes, foot.EntityType, padding) : null, commitSuppressed: gardenFootCallback?.BoardingTarget is not null || ragnarokFootCallback?.BoardingTarget is not null);
        x = ground.X;
        z = ground.Z;
        EnsureContactCertificate(before with { X = x, Z = z, CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot });
        WorldGround? center = ground.Center?.Ground;
        int nativeHeight = before.NativeHeight;
        byte? groundType = before.GroundType;
        if (center is WorldGround accepted)
        {
            nativeHeight = NativeWorldTerrain.ResolveHeight(foot.EntityType, -(int)accepted.Height, accepted.Triangle.GroundType, before.NativeHeight).Height;
            groundType = accepted.Triangle.GroundType;
        }
        if (foot.NormalObjects is not null)
        {
            foot = CompleteNormalFootObjectCallback(foot, x, z, nativeHeight);
        }
        salaryMovementUnits = NativeWorldSalaryMovement.AcceptedVectorUnits(ground.PositionCommitted ? moveX : 0, 0, ground.PositionCommitted ? graphicsMoveZ : 0);
        encounterMovementMagnitude = ground.PositionCommitted ? graphicsSpeed : 0;
        WorldState after = before with
        {
            Frame = before.Frame + 1,
            X = x,
            Z = z,
            NativeHeight = nativeHeight,
            BodyYaw = bodyYaw,
            CameraYaw = cameraYaw,
            CameraVelocity = velocity,
            CameraTargetX = targetX,
            CameraTargetZ = targetZ,
            CameraTargetNativeHeight = targetHeight,
            AnimationId = animation,
            AnimationCounter = animationCounter,
            GroundType = groundType,
            NativeFootRuntime = foot with { GroundCache = ground.Cache, GroundYaw = ground.GroundYaw, PreviousHorizontalInput = inputX, MovementMagnitude20409F4 = encounterMovementMagnitude }
        };
        return ragnarokCameraState is not null ? CompleteRagnarokFootCallback(after, ground) : gardenCameraState is not null ? CompleteGardenFootCallback(after, ground, graphicsSpeed) : after;
    }

    private void EnsureContactCertificate(WorldState state)
    {
        if (state.NativeTrainRuntime is not null)
        {
            ValidateTrainRide(state.NativeTrainRuntime);
            return;
        }
        if (state.NativeScriptedRuntime is not null)
        {
            ValidateScriptedState(state);
            return;
        }
        if (state.NativeCarRuntime is not null)
        {
            EnsureCarContactCertificate(state);
            return;
        }
        if (state.NativeRagnarokRuntime is not null)
        {
            state.NativeRagnarokRuntime.RequireSceneState();
            assets.ValidateNativeGroundCache(state.NativeRagnarokRuntime.GroundCache!, replacements);
            return;
        }
        if (state.NativeGardenRuntime is not null)
        {
            state.NativeGardenRuntime.Validate();
            assets.ValidateNativeGroundCache(state.NativeGardenRuntime.GroundCache, replacements);
            return;
        }
        if (state.NativeFootRuntime?.ParkedCar is { } parkedCar)
        {
            parkedCar.Validate();
            EnsureCarContactCertificate(state with { NativeCarRuntime = parkedCar.Car, NativeFootRuntime = null });
            assets.ValidateNativeGroundCache(state.NativeFootRuntime.GroundCache!, replacements);
            return;
        }
        if (state.NativeFootRuntime?.StationTrains is not null)
        {
            ValidateStationTrainProcess(state.NativeFootRuntime);
            return;
        }
        if (state.NativeFootRuntime?.NormalObjects is not null)
        {
            ValidateNormalFootObjects(state);
            return;
        }
        WorldCamera camera = FootCamera(state);
        double eyeX = camera.FootFrame(new Vector3(state.X, -state.NativeHeight, state.Z)).Eye.X;
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        byte entityType = state.NativeFootRuntime?.EntityType ?? throw new NotSupportedException("Native foot contacts require executed entity-type loader authority.");
        assets.ReadFootAllocation(entityType, inputs.StoryWord);
        WorldFootContactCertificateResult result = WorldFootContactCertificate.Evaluate(session.State.SavedWorld, (byte)inputs.StoryWord, (byte)(inputs.StoryWord >> 8), inputs.ReplacementFlags, state.X, state.Z, eyeX, allowTravel: true, entityType: entityType);
        if (!result.Certified)
        {
            throw new NotSupportedException($"Unsupported native world contact state: {result.Reason}");
        }
    }

    private static WorldCamera FootCamera(WorldState state)
    {
        return WorldCamera.Foot(state.CameraYaw, 2048, projectionDistance: state.NativeFootRuntime?.CameraTransition?.ProjectionDistance ?? 1024) with { TargetPosition = new Vector3(state.CameraTargetX, -state.CameraTargetNativeHeight, state.CameraTargetZ), NativePitch = state.NativeFootRuntime?.Forest?.CameraPitch, NativeOffsetY = state.NativeFootRuntime?.CameraOffsetY, NativeOffsetZ = state.NativeFootRuntime?.CameraOffsetZ };
    }

    private static int FollowCamera(int velocity, int delta, bool damping)
    {
        int next;
        if (damping)
        {
            next = Math.Abs(velocity) < 8 ? 0 : (3 * velocity) >> 2;
        }
        else if (Math.Abs(delta) < 128)
        {
            next = (3 * velocity) >> 2;
        }
        else if (Math.Abs(2048 - Math.Abs(delta)) < 128)
        {
            next = velocity >> 1;
        }
        else
        {
            next = velocity + (delta < 128 ? -8 : 8);
        }
        return Math.Clamp(next, -128, 128);
    }

    private static (int X, int Z) ReadFootMovementComponents(float horizontal, float vertical)
    {
        if (horizontal is -1 or 0 or 1 && vertical is -1 or 0 or 1)
        {
            // Original5573C0 gives digital diagonals90 and cardinals127.
            int magnitude = horizontal != 0 && vertical != 0 ? 90 : 127;
            return ((int)horizontal * magnitude, -(int)vertical * magnitude);
        }
        Vector2 axes = new(horizontal, vertical);
        if (axes.LengthSquared() > 1)
        {
            axes = Vector2.Normalize(axes);
        }
        // The same5573C0 motor accepts signed analogue WORD components;
        // 557A90 applies its existing integer scale, rotation and ground query.
        return (ReadSignedMovementComponent(axes.X), ReadSignedMovementComponent(-axes.Y));
    }

    private static int ReadSignedMovementComponent(float axis) => (int)(axis * 127);

    private static (int X, int Z) Rotate(int x, int z, int yaw)
    {
        int cosine = NativeRound(Math.Cos(yaw * NativeAngleUnit) * 4096);
        int sine = NativeRound(Math.Sin(yaw * NativeAngleUnit) * 4096);
        return (NativeRound((cosine * x + sine * z) / 4096d), NativeRound((-sine * x + cosine * z) / 4096d));
    }

    private static int NativeRound(double value) => checked((int)Math.Round(value, MidpointRounding.ToEven));

    private static int YawDelta(int first, int second)
    {
        int difference = first - second;
        if (difference > 2048)
        {
            difference -= 4096;
        }
        if (difference <= -2048)
        {
            difference += 4096;
        }
        return difference;
    }

    private static int WrapYaw(int yaw) => (yaw % 4096 + 4096) % 4096;

    private static int NearWrapped(int value, int reference, int period)
    {
        if (value - reference > period / 2)
        {
            value -= period;
        }
        if (value - reference < -period / 2)
        {
            value += period;
        }
        return value;
    }

    private static int WrapCoordinate(int value, int period) => (value + period / 2 + period) % period - period / 2;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        assets.Dispose();
        disposed = true;
    }
}
