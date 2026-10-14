using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Original normal-foot world component with bounded native field reentry and complete saved-foot entry62 reload. Object collision, encounters and idle gestures remain unsupported.</summary>
internal sealed partial class WorldMapScene : IDisposable
{
    public const double SimulationFps = 30.5;
    private const double NativeAngleUnit = 0.0015339808305725455;
    private const double NativeAtanScale = 651.8986206054688;
    private GameSession session;
    private readonly WorldAssets assets;
    private readonly IReadOnlyDictionary<int, int> replacements;
    private readonly byte storyByte;
    private WorldTerrainRenderer? renderer;
    private bool disposed;
    private bool rebound;
    public Func<WorldFieldSelection, WorldState, short, NativeWorldSalaryState, Action>? PrepareFieldEntry { get; set; }
    public Func<NativeSalaryResult, NativeSalaryPresentation?, NativeGlobalSoundProcess?, NativeSalaryEffects>? PrepareSalaryEffects { get; set; }
    public Action<NativeSalaryEffects>? PublishSalaryEffects { get; set; }
    public WorldState? LastDeparture { get; private set; }

    public WorldMapScene(GameSession session, string gameDirectory)
    {
        this.session = session;
        if (session.State.Scene != SceneKind.WorldMap || session.State.WorldArrival is not WorldTransition arrival)
        {
            throw new InvalidOperationException("A world scene requires its original field-to-world transition.");
        }
        bool car = unchecked((sbyte)arrival.Mode) == 33;
        bool boat = IsSupportedBoatPacket(arrival);
        byte footEntityType = unchecked((byte)arrival.Mode);
        if (footEntityType is not (0 or 1) && !car && !boat)
        {
            throw new NotSupportedException($"World entity type {unchecked((sbyte)arrival.Mode)} has no implemented original model/control path.");
        }
        int entry = unchecked((sbyte)arrival.EntryIndex);
        if (session.State.SavedWorld is null)
        {
            throw new NotSupportedException("Native saved-world provenance is unknown; original vehicle state must be established before world use.");
        }
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        if (!boat && (car || footEntityType == 1 ? inputs.StoryWord is < 34 or >= 205 : inputs.StoryWord is >= 34 and < 205 or >= 290))
        {
            throw new NotSupportedException("World entry story lies outside this entity's proved native object/contact clause.");
        }
        NativeSavedWorldFootReturn? reload = null;
        NativeFootWorldRuntimeState? initialFootRuntime = null;
        NativeCarWorldRuntimeState? initialCarRuntime = null;
        NativeSavedWorldState? carSavedDeparture = null;
        if (boat)
        {
            if (session.State.World is not null && session.State.World.NativeBoatRuntime is null)
            {
                throw new NotSupportedException("Native boat restoration has no executed scripted runtime authority.");
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
                session.SetSavedWorldState(session.State.SavedWorld.WithOrdinaryCarEntry());
            }
            else if (session.State.World.NativeCarRuntime is null)
            {
                throw new NotSupportedException("Native car restoration has no executed vehicle runtime authority.");
            }
        }
        else if (session.State.World is null)
        {
            RequireUnaliasedFootAuxiliary(session.State);
            if (entry == 62)
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
        else
        {
            NativeFootWorldRuntimeState runtime = session.State.World.NativeFootRuntime ?? throw new NotSupportedException("Native foot restoration requires executed entity-type loader authority.");
            runtime.RequireSupportedNormalFoot();
            if (runtime.EntityType != footEntityType)
            {
                throw new InvalidDataException("The restored native foot entity type differs from its world packet.");
            }
            if (entry == 62 && session.State.World.NativeFootRuntime?.Rule != NativeFootWorldRuntimeState.SavedReloadRule)
            {
                throw new NotSupportedException("Historical entry62 world state has no proved native foot-reload authority.");
            }
        }
        assets = new WorldAssets(gameDirectory);
        try
        {
            storyByte = inputs.StoryByte;
            if (boat)
            {
                if (session.State.World is null)
                {
                    session.SetWorldState(CreateBoatState(arrival, inputs));
                }
                replacements = WorldTerrainSelection.Replacements(State.ReplacementMask);
                ValidateBoatState(State);
                return;
            }
            if (session.State.World is null)
            {
                if (entry != 62 && (uint)entry >= assets.Spawns.Count)
                {
                    throw new InvalidDataException($"Original world spawn {entry} is absent.");
                }
                WorldSpawn spawn = entry == 62 ? default : assets.Spawns[entry];
                int x = reload?.Pose0.X ?? spawn.X;
                int z = reload?.Pose0.RawZ ?? spawn.Z;
                int height = reload?.Pose0.Height ?? spawn.NativeHeight;
                ushort mask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte);
                replacements = WorldTerrainSelection.Replacements(mask);
                WorldGround? ground;
                if (car)
                {
                    WorldGroundSelection initialGround = assets.LookupNativeGround(x, z, replacements, []) ?? throw new NotSupportedException("Original car spawn has no native ground lookup result.");
                    initialCarRuntime = NativeCarWorldRuntimeState.Create(carSavedDeparture!, NativeWorldGroundCacheState.CreateFresh(initialGround.Identity, session.State.DepartingWorldState?.NativeFootRuntime?.GroundCache ?? session.State.DepartingWorldState?.NativeCarRuntime?.GroundCache));
                    ground = initialGround.Ground;
                }
                else
                {
                    WorldGroundSelection initialGround = assets.LookupNativeGround(x, z, replacements, []) ?? throw new NotSupportedException("Original foot spawn has no native53EB80 ground lookup result.");
                    NativeWorldGroundCacheState? retained = session.State.DepartingWorldState?.NativeFootRuntime?.GroundCache ?? session.State.DepartingWorldState?.NativeCarRuntime?.GroundCache;
                    initialFootRuntime = initialFootRuntime! with { GroundCache = NativeWorldGroundCacheState.CreateFresh(initialGround.Identity, retained) };
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
                    NativeCarRuntime = initialCarRuntime
                };
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
                EnsureContactCertificate(State);
            }
        }
        catch
        {
            assets.Dispose();
            throw;
        }
    }

    public WorldState State => session.State.World ?? LastDeparture ?? throw new InvalidOperationException("This world scene no longer has a current state.");
    public bool IsPresentationDeferred => State.NativeBoatRuntime?.Path.Callback == 0;

    internal static void RequireUnaliasedFootAuxiliary(GameState state)
    {
        // Event base1CFE9B8 aliases saved-world base1CFEEB8 at +108/+110.
        // Reject every byte of a legacy word/dword write that could overlap either.
        if (state.Variables.Keys.Any(address => address is >= 1385 and <= 1390))
        {
            throw new NotSupportedException("Native saved-world foot auxiliary bytes overlap an unproved legacy variable alias.");
        }
    }

    internal void ValidatePreparedSession(GameState candidate)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (rebound || candidate.Scene != SceneKind.WorldMap || candidate.WorldArrival != session.State.WorldArrival || candidate.World != State || candidate.SavedWorld != session.State.SavedWorld || candidate.Variables.Count != session.State.Variables.Count || candidate.Variables.Any(pair => !session.State.Variables.TryGetValue(pair.Key, out int value) || value != pair.Value))
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
        if (!float.IsFinite(action.Horizontal) || !float.IsFinite(action.Vertical) || Math.Abs(action.Horizontal) > 1 || Math.Abs(action.Vertical) > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Movement axes must be finite and between -1 and 1.");
        }
        bool accepted = action.Kind is PlayerActionKind.Move or PlayerActionKind.Wait;
        int advanced = 0;
        int sourceField = session.State.FieldId;
        long sourceFrame = State.Frame;
        List<ReplayWorldNavigationStimulus> navigation = [];
        WorldState candidate = State;
        NativeWorldSalaryState salary = NativeWorldSalaryState.Begin(session.State);
        List<GlobalSoundRequest> salaryRequests = [];
        WorldFieldSelection? fieldEntry = null;
        if (accepted)
        {
            for (int index = 0; index < action.Frames; index++)
            {
                bool advanceUi = candidate.NativeBoatRuntime?.Path.EndpointRequested != true;
                candidate = Step(candidate, action.Kind == PlayerActionKind.Move ? action.Horizontal : 0, action.Kind == PlayerActionKind.Move ? action.Vertical : 0, out fieldEntry, out ReplayWorldNavigationStimulus proposal, out uint movementUnits);
                NativeSalaryResult? salaryResult = candidate.NativeBoatRuntime is null ? GameSession.SampleWorldSalary(session.State, salary.SeeD, salary.Economy, movementUnits) : null;
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
                salary = salary.Advance(salaryResult, effects.Presentation?.Advance(advanceUi ? 2 : 0), effects.SoundProcess, movementUnits);
                salaryRequests.AddRange(effects.Requests);
                navigation.Add(proposal);
                advanced++;
                if (fieldEntry is not null)
                {
                    break;
                }
            }
        }
        List<WorldActionRecord> actions = [.. candidate.Actions, new WorldActionRecord(candidate.Actions.Count + 1, candidate.Frame, action.Kind.ToString(), action.Horizontal, action.Vertical, action.Frames, advanced, candidate.X, candidate.Z, candidate.NativeHeight)];
        candidate = candidate with { Actions = actions };
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
            RecordReplay(departed);
            return departed;
        }
        if (accepted)
        {
            session.CommitWorldAction(candidate, salary);
            PublishSalaryEffects?.Invoke(new NativeSalaryEffects(salary.Presentation, salary.SoundProcess, salaryRequests));
        }
        else
        {
            session.SetWorldState(candidate);
        }
        PlayerTrigger trigger = new(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null);
        PlayerActionResult result = new(accepted, trigger, advanced, accepted ? null : "This original world action is not implemented.");
        RecordReplay(result);
        return result;

        void RecordReplay(PlayerActionResult outcome)
        {
            session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.WorldAction, SceneKind.WorldMap, sourceField, sourceFrame) { Code = (int)action.Kind, Value = action.Value, Text = action.Text, Horizontal = action.Horizontal, Vertical = action.Vertical, Frames = action.Frames, Advanced = outcome.FramesAdvanced, Accepted = outcome.Accepted, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = candidate.Frame, ResultTrigger = (int)outcome.Trigger.Kind, WorldNavigation = navigation.ToArray() });
        }
    }

    public byte[] Render(int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (renderer is null || renderer.Width != width || renderer.Height != height)
        {
            renderer = new WorldTerrainRenderer(assets, width, height);
        }
        WorldState state = State;
        if (state.NativeBoatRuntime is not null)
        {
            return RenderBoat(state);
        }
        EnsureContactCertificate(state);
        WorldCamera camera = WorldCamera.Foot(state.CameraYaw, 2048) with { TargetPosition = new Vector3(state.CameraTargetX, -state.CameraTargetNativeHeight, state.CameraTargetZ) };
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        return renderer.Render(new Vector3(state.X, -state.NativeHeight, state.Z), state.BodyYaw * MathF.Tau / 4096, state.AnimationId, state.AnimationCounter >> 4, camera, replacements, sky, state.NativeCarRuntime is not null || state.GroundType is null or > 5, state.NativeCarRuntime is null ? null : assets.ExamCar, state.NativeCarRuntime?.ModelTilt ?? 0, avatarModel: state.NativeCarRuntime is null ? assets.GetFootModel(state.NativeFootRuntime!.EntityType) : null);
    }

    private WorldState Step(WorldState before, float horizontal, float vertical, out WorldFieldSelection? fieldEntry, out ReplayWorldNavigationStimulus inputProposal, out uint salaryMovementUnits)
    {
        if (before.NativeBoatRuntime is not null)
        {
            // Native53FAC0 does not call6519D0 for scripted entities65/66.
            salaryMovementUnits = 0;
            return StepBoat(before, out fieldEntry, out inputProposal, (node, camera, origin) => NativeWorldPathProjectionCertificate.Create(camera, origin, before.NativeBoatRuntime.Path.EntityType).ProjectNode(node));
        }
        if (before.NativeCarRuntime is not null)
        {
            return StepCar(before, horizontal, vertical, out fieldEntry, out inputProposal, out salaryMovementUnits);
        }
        fieldEntry = null;
        NativeFootWorldRuntimeState foot = before.NativeFootRuntime!;
        foot.RequireNativeGroundState();
        int inputX = Math.Sign(horizontal);
        int inputZ = -Math.Sign(vertical);
        int magnitude = inputX != 0 && inputZ != 0 ? 90 : 127;
        int localX = (16 * inputX * magnitude) >> 6;
        int localZ = (16 * inputZ * magnitude) >> 6;
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
        int velocity = FollowCamera(before.CameraVelocity, YawDelta(bodyYaw, before.CameraYaw), dampCamera);
        int cameraYaw = WrapYaw(before.CameraYaw + (velocity >> 3));
        int targetX = (before.X + NearWrapped(before.CameraTargetX, before.X, 262144)) >> 1;
        int targetZ = (before.Z + NearWrapped(before.CameraTargetZ, before.Z, 196608)) >> 1;
        int targetHeight = (3 * before.CameraTargetNativeHeight + before.NativeHeight - 256) >> 2;
        int animation = moving ? 1 : 0;
        int animationCounter = animation == before.AnimationId && moving ? (before.AnimationCounter + 16) % (assets.GetFootModel(before.NativeFootRuntime!.EntityType).Animations[1].Frames.Count * 16) : 0;
        int x = WrapCoordinate(before.X + moveX, 262144);
        int z = WrapCoordinate(before.Z - graphicsMoveZ, 196608);
        inputProposal = new ReplayWorldNavigationStimulus(x, z);
        EnsureContactCertificate(before with { X = x, Z = z, CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight });
        int previousGroundHeight = -before.NativeHeight + (before.GroundType is <= 5 ? 320 : 0);
        int? selectedGroup = inputX != 0 && foot.PreviousHorizontalInput != inputX ? inputX > 0 ? 1 : 0 : null;
        WorldNativeGroundResult ground = assets.ResolveNativeGround(before.X, before.Z, moveX, graphicsMoveZ, previousGroundHeight, selectedGroup, foot.GroundCache!, replacements, foot.EntityType, foot.PreviousHorizontalInput is null);
        x = ground.X;
        z = ground.Z;
        EnsureContactCertificate(before with { X = x, Z = z, CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight });
        WorldGround? center = ground.Center?.Ground;
        int nativeHeight = before.NativeHeight;
        byte? groundType = before.GroundType;
        if (center is WorldGround accepted)
        {
            if (accepted.Triangle.FieldEntrance)
            {
                WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
                fieldEntry = assets.FieldEntries.Select(new WorldFieldSelectionInput(x, z, inputs.StoryWord, foot.EntityType), accepted.Triangle.TextureFlags);
            }
            nativeHeight = unchecked((short)(-(int)accepted.Height + (accepted.Triangle.GroundType <= 5 ? 320 : 0)));
            groundType = accepted.Triangle.GroundType;
        }
        salaryMovementUnits = NativeWorldSalaryMovement.AcceptedVectorUnits(ground.Blocked ? 0 : moveX, 0, ground.Blocked ? 0 : graphicsMoveZ);
        return before with
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
            NativeFootRuntime = foot with { GroundCache = ground.Cache, GroundYaw = ground.GroundYaw, PreviousHorizontalInput = inputX }
        };
    }

    private void EnsureContactCertificate(WorldState state)
    {
        if (state.NativeBoatRuntime is not null)
        {
            ValidateBoatState(state);
            return;
        }
        if (state.NativeCarRuntime is not null)
        {
            EnsureCarContactCertificate(state);
            return;
        }
        WorldCamera camera = WorldCamera.Foot(state.CameraYaw, 2048) with { TargetPosition = new Vector3(state.CameraTargetX, -state.CameraTargetNativeHeight, state.CameraTargetZ) };
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
