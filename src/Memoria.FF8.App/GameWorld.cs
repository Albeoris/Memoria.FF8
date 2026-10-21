using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

/// <summary>The live host and diagnostic commands share one persistent VM across field loads.</summary>
internal sealed partial class GameWorld : IDisposable
{
    private readonly GameSession session;
    private readonly MovieClock movieClock;
    private readonly bool ownsMovieClock;
    private readonly GameAssets assets;
    private readonly bool ownsAssets;
    private readonly ResourceService? resources;
    private readonly bool validateWorldPresentation;
    private readonly IDisposable snapshotPreparation;
    private FieldRenderer? renderer;
    private WorldMapScene? worldScene;
    private SpuWaveAudioOutput? spuAudio;
    private int renderWidth = FieldRenderer.Width;
    private int renderHeight = FieldRenderer.Height;

    internal static GameSession CreateColdProcess(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        return GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
    }

    public GameWorld(GameSession session, string gameDirectory, MovieClock? movieClock = null, ResourceService? resources = null, nint audioWindow = default, bool audioEnabled = false, bool validateWorldPresentation = true, Func<string, string>? resolveGameFile = null, GameAssets? sharedAssets = null, Func<FieldSceneMods>? readSceneMods = null)
    {
        this.session = session;
        this.readSceneMods = readSceneMods;
        this.resources = resources;
        this.validateWorldPresentation = validateWorldPresentation;
        ownsAssets = sharedAssets is null;
        assets = sharedAssets ?? new GameAssets(gameDirectory, resources: resources);
        bool initializedNativeParty = false;
        if (session.State.KnownMagic is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameKnownMagic(assets.ReadNewGameKnownMagic());
        }
        if (session.State.ScannedModels is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameScannedModels(assets.ReadNewGameScannedModels());
        }
        if (session.State.SavedBattleFlags is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameSavedBattleFlags(assets.ReadNewGameSavedBattleFlags());
        }
        if (session.State.LimitProgress is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameLimitProgress(assets.ReadNewGameLimitProgress());
        }
        if (session.State.PocketStation is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGamePocketStation(assets.ReadNewGamePocketStation());
        }
        if (session.State.GlobalRandom is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameGlobalRandom(assets.ReadNewGameGlobalRandom());
        }
        if (session.State.WeaponRecipes is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameWeaponRecipes(assets.ReadNewGameWeaponRecipes());
        }
        if (session.State.ShopHistory is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameShopHistory(assets.ReadNewGameShopHistory());
        }
        if (session.State.ShotAvailability is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        }
        if (session.State.InputConfiguration is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        }
        if (session.State.SeeD is { SalaryTable: null } && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        }
        if (session.State.Economy is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        }
        if (session.State.CharacterVitals is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            initializedNativeParty = true;
        }
        if (session.State.CharacterAppearance is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameCharacterAppearance(assets.ReadNewGameCharacterAppearance());
        }
        MovieClock clock = movieClock ?? new MovieClock(session, gameDirectory);
        this.movieClock = clock;
        ownsMovieClock = movieClock is null;
        if (resolveGameFile is not null)
        {
            clock.ResolveGameFile = resolveGameFile;
        }
        Events = new EventEngine(session);
        Events.IsInspectionScriptEnabled = (objectId, scriptId) => !inspectionDisabledScripts.Contains((objectId, scriptId));
        Events.PrepareSpuAudio = () => (spuAudio ??= new SpuWaveAudioOutput(assets.GameDirectory, clock.ToolsDirectory, assets.ResolveMusicCue(MusicCue.EyesOnMe), audioWindow, audioEnabled)).Prepare();
        Events.StopSpuAudio = () => spuAudio?.Stop();
        Events.HasLiveSpuAudio = () => spuAudio?.HasLiveAudio == true;
        Events.ReadFieldMagicStatRules = (vitals, row) => BattleResources.ReadPlayerNativeStatRules(vitals, row);
        Events.ResolveNativeFieldItem = id => FieldItemCatalog.FieldItem(id);
        Events.ResolveNativeFieldItemStatRules = assets.ReadFieldItemStatRules;
        Events.ResolveNativeAutoJunction = (vitals, row, priority) => assets.ReadNativeTutorialCatalog().AutoJunction(vitals, row, priority);
        ConfigureTripleTriadHost();
        ConfigureBattleHost();
        ConfigureRandomEncounterHost();
        Events.OperationCompleted += ObservePendingBattle;
        if (assets.NativeRuntimeProfile is { IsVerified: true })
        {
            Events.ShakeTables = assets.ReadNativeShakeTables();
            Events.DrawPointParticleTables = assets.ReadNativeDrawPointParticleTables();
            Events.ResolveNativeDrawPointDirection = FieldNativeScriptDirectionProbe.ResolveFacing;
        }
        if (initializedNativeParty)
        {
            // Native470560 writes field-party DWORD1CFE990=FFFFFF00 after actual New Game.
            Events.Scene.Party.Clear();
            Events.Scene.Party.AddRange([0, 255, 255]);
        }
        if (session.State.OriginalSave is not null && session.State.EventState is null && (session.State.Scene == SceneKind.WorldMap || session.State.FieldLoadContext?.Origin == NativeFieldLoadOrigin.OriginalSave))
        {
            Events.InitializeOriginalSave(assets.NativeRuntimeProfile ?? throw new NotSupportedException("Original PC save loading requires a supported installed native runtime profile."));
        }
        Events.MoviePlayback = () => clock.State;
        clock.AdvanceFrame = AdvanceMovieFrame;
        Events.AdvanceMovieFrames = clock.AdvanceFrames;
        Events.ReadMovieCameraFlags = ReadNativeMovieCameraFlags;
        Events.MovieSkipRequested += clock.Skip;
        Events.LoadSoundResource = _ => SoundResourceStatus.Muted;
        Events.ResolveGlobalSoundResource = ResolveGlobalSound;
        Events.PrepareWorldTransition = PrepareNativeWorldEntry;
        Events.ReadNativeVibrationBank = assets.ReadNativeVibrationBank;
        Events.ResolveNativeShopDefinitions = assets.ReadNativeShopDefinitions;
        Events.ResolveNativeWeaponShopDefinitions = assets.ReadNativeWeaponShopDefinitions;
        Events.ResolveNativeMagazinePage = (itemId, pageId) => FieldItemCatalog.MagazinePage(itemId, pageId);
        Events.ResolveNativeRefinementDefinitions = assets.ReadNativeRefinementDefinitions;
        Events.ReadNativeDrawPointCatalog = assets.ReadNativeDrawPointCatalog;
        Events.ReadNativeActorInfoArena = assets.ReadNativeActorInfoArena;
        Events.ReadSteamDiscMount = selectedDisc => NativeSteamDiscMountReader.Read(assets.GameDirectory, selectedDisc);
        snapshotPreparation = session.BindSnapshotPreparation(PrepareSnapshot);
    }

    private GameState PrepareSnapshot(GameState state)
    {
        if (state.Scene == SceneKind.WorldMap && Field is not null && worldScene is not null)
        {
            // The dormant VM remains frozen between publications; its services supply the current shared owners.
            return state with { EventState = Events.Capture() };
        }
        return state;
    }

    public EventEngine Events { get; }

    internal GameAssets ValidationAssets => assets;

    internal event Action<GameWorld>? BeforeFieldAllocation;

    internal bool HasCurrentFieldAllocation => session.State.Scene != SceneKind.WorldMap && worldScene is null && Events.FieldId == session.State.FieldId && Events.NextTrigger.Kind is not (PlayerTriggerKind.FieldTransition or PlayerTriggerKind.WorldMap);

    internal bool AdvanceMovieFrame(MoviePlaybackState playback, double seconds) => AdvanceMovieFrame(playback, seconds, 0, 0);

    internal bool AdvanceMovieFrame(MoviePlaybackState playback, double seconds, float horizontal, float vertical)
    {
        int field = session.State.FieldId;
        int movie = session.State.MovieIndex;
        NativeMovieSource source = MovieClock.RequireSource(session.State);
        session.SetMoviePlayback(playback);
        session.SetMoviePosition(seconds);
        // MOVIE owns startup records0/1; later decoded records each own one ordinary callback.
        // A resumed decoder first redisplays the saved frame. Its executed CAM receipt already
        // owns that callback: running it twice repeats scripts and violates consecutive CAM order.
        bool alreadyExecuted = Events.Scene.NativeMovieMask?.Playback?.AppliedRecord == playback.FrameIndex;
        if (playback.FrameIndex >= 2 && !alreadyExecuted)
        {
            Events.StepMovieFrame(horizontal, vertical);
        }
        return session.IsMoviePlaying && session.State.MovieIndex == movie && session.State.FieldId == field && session.State.MovieSource == source;
    }
    public FieldAssets? Field { get; private set; }
    private WorldMapLocations? worldLocations;

    internal static bool HasWorldPlaceArrival(SceneEventState departingScene, GameState state) => departingScene.WorldTransitionRequest is not null && state.DepartingWorldState is not null;

    public WorldMapScene? WorldScene => worldScene;
    public DialogueState? CurrentDialogue => session.State.Scene == SceneKind.WorldMap ? worldScene?.CurrentDialogue : Events.CurrentDialogue;
    public PlayerTrigger NextTrigger => PendingEntityName is { } pendingName ? new(PlayerTriggerKind.Dialogue, session.State.FieldId, Events.Frame, null, pendingName.DefaultName) : session.State.RandomEncounters?.Pending is { } random ? new(PlayerTriggerKind.Battle, random.FieldId, random.Frame, null, null) : worldScene is not null && session.State.Scene == SceneKind.WorldMap ? worldScene.CurrentDialogue is { } dialogue ? new PlayerTrigger(PlayerTriggerKind.Dialogue, session.State.FieldId, worldScene.State.Frame, dialogue.MessageId, dialogue.Text) : new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, worldScene.State.Frame, null, null) : Events.FieldId != session.State.FieldId ? new PlayerTrigger(PlayerTriggerKind.FieldTransition, session.State.FieldId, worldScene?.LastDeparture?.Frame ?? Events.Frame, null, null) : Events.NextTrigger;

    public bool EnsureField()
    {
        if (session.State.Scene == SceneKind.WorldMap)
        {
            if (worldScene is not null)
            {
                if (session.State.DepartingFieldState is System.Text.Json.JsonElement departure)
                {
                    session.SetEventState(departure);
                }
                session.SetDepartingFieldState(null);
                return false;
            }
            if (Field is null && (session.State.DepartingFieldState ?? session.State.EventState) is System.Text.Json.JsonElement savedField)
            {
                LoadField(savedField.GetProperty("FieldId").GetInt32(), null, savedField, restoreDeparting: true);
            }
            if (Events.Scene.WorldMusicLoad is null)
            {
                throw new NotSupportedException("World restoration requires its executed native music-loader receipt; migrate this save explicitly.");
            }
            worldScene = new WorldMapScene(session, assets.GameDirectory, resources);
            ConfigureWorldScene(worldScene);
            if (session.State.DepartingFieldState is System.Text.Json.JsonElement frozenDeparture)
            {
                session.SetEventState(frozenDeparture);
            }
            session.SetDepartingFieldState(null);
            session.DismissDialog();
            return true;
        }
        bool returningFromWorld = worldScene is not null;
        if (returningFromWorld)
        {
            worldScene!.Dispose();
            worldScene = null;
        }
        // A world arrival starts a new visit even when its packet names the frozen departing field.
        if (!returningFromWorld && Field?.Id == session.State.FieldId && HasCurrentFieldAllocation)
        {
            return false;
        }
        if (Field is null && session.State.DepartingFieldState is System.Text.Json.JsonElement departing)
        {
            LoadField(departing.GetProperty("FieldId").GetInt32(), null, departing, restoreDeparting: true);
        }
        if (Field is not null)
        {
            BeforeFieldAllocation?.Invoke(this);
        }
        LoadField(session.State.FieldId, session.State.FieldArrival, session.State.EventState);
        session.SetDepartingFieldState(null);
        return true;
    }

    private void LoadField(int fieldId, FieldTransition? arrival, System.Text.Json.JsonElement? snapshot, bool restoreDeparting = false)
    {
        ClearSceneInspection();
        ValidateDiscJumpArrival(arrival, snapshot is null);
        if (restoreDeparting && (session.EventRandom is null || session.VibrationProcess is null || session.FieldFade is null || session.ShopHistory is null || session.MenuRandom is null || session.WeaponRecipes is null))
        {
            throw new NotSupportedException("Restoring a dormant field cannot supply the unknown current world event/menu random, vibration, fade or shop owners from its earlier snapshot.");
        }
        FieldAssets next = assets.LoadField(fieldId);
        Events.ReadNativeFieldRegistrationResources = () => next.NativeRegistration;
        Events.ResolveFieldSoundEffectTable = () =>
        {
            FieldSoundEffectTable table = assets.LoadFieldSoundEffects(next.Id);
            return new NativeFieldSoundTable(table.MemberName, table.Sha256, table.EffectIds);
        };
        Events.AnimationFrameCount = next.GetAnimationFrameCount;
        Events.ReadNativeEmptyAnimationTerminals = models => assets.ReadNativeEmptyAnimationTerminals(next.Id, models);
        Events.ReadNativeEyes = model => (uint)model < next.Models.Count ? next.Models[model].NativeEyes : null;
        Events.ReadNativeModelParts = model => (uint)model < next.Models.Count ? next.Models[model].NativeParts : null;
        Events.CaptureFootstepModelOrigin = query => NativeFootstepSoundAdapter.Capture(ReadNativeFootstepCamera(next, false), Events.Scene.CameraIndex, query);
        Events.ResolveFootstepSound = (actor, mode, beforeCameraSelection) => NativeFootstepSoundAdapter.Resolve(ReadNativeFootstepCamera(next, beforeCameraSelection), actor, mode, () => assets.LoadFieldSoundEffects(next.Id));
        Events.ReadFaceBaseline = actor =>
        {
            next.GetAnimationFrameCount(actor.RenderModelId, actor.AnimationId);
            Vector3 rotation = next.Models[actor.RenderModelId].ReadFirstControlRotation(actor.AnimationId, 0);
            return new FaceDirectionBaseline((int)MathF.Round(rotation.Y * 4096 / MathF.Tau) / 16, (int)MathF.Round(rotation.Z * 4096 / MathF.Tau) / 16, (int)MathF.Round(rotation.X * 4096 / MathF.Tau) / 16);
        };
        Events.ReadFaceAnchorHeight = actor => next.Models[actor.RenderModelId].GetFirstControlBoneHeight(actor.AnimationId, actor.AnimationFrame, actor.RootTranslationZ, actor.FirstControlRotation);
        Events.CanDisableAdditionalControl = actor =>
        {
            FieldModel model = next.Models[actor.RenderModelId];
            int additional = model.ControlBones.Additional;
            return (uint)additional >= model.Bones.Count || !model.Bones.Any(bone => bone.Parent == additional);
        };
        Events.ResolveWalkmesh = (x, y) => next.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle, Events.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
        Events.CanSetBackgroundScroll = (layer, settings) => BackgroundScrollCertificate.CanSetForState(next, Events.Scene, Events.Actors, layer, settings);
        Events.CanSetBackgroundScrollRatio = (layer, settings) => BackgroundScrollCertificate.CanSetRatioForState(next, Events.Scene, Events.Actors, layer, settings);
        Events.CanSetBackgroundScrollPosition = (layer, x, y) => DirectBackgroundScrollCertificate.CanSet(next, Events.Scene, layer, x, y) || NativeNonselectedBackgroundScrollCertificate.CanSetDirect(next, Events.Scene, layer, x, y) || NativeWrappedBackgroundScrollCertificate.CanSetDirect(next, Events.Scene, layer, x, y);
        Events.ResolveNativeActorScrollCenter = nativeActor => ActorBackgroundScrollCertificate.Resolve(next, Events.Scene, Events.Actors, nativeActor);
        Events.ResolveNativeLayerActorScrollCenter = (layer, nativeActor) => ActorBackgroundScrollCertificate.Resolve(next, Events.Scene, Events.Actors, nativeActor, layer);
        Events.CanSetNativeLayerActorLinearBackgroundScroll = (layer, actor, duration) => ActorBackgroundScrollCertificate.CanSetInterpolated(next, Events.Scene, Events.Actors, actor, duration, layer);
        Events.CanSetNativeLayerActorCosineBackgroundScroll = (layer, actor, duration) => ActorBackgroundScrollCertificate.CanSetInterpolated(next, Events.Scene, Events.Actors, actor, duration, layer);
        Events.CanRestoreNativeActorScroll = (scene, actors) => ActorBackgroundScrollCertificate.CanRestore(next, scene, actors);
        Events.CanSetNativeActorCosineBackgroundScroll = (actor, duration) => ActorBackgroundScrollCertificate.CanSetInterpolated(next, Events.Scene, Events.Actors, actor, duration);
        Events.CanSetNativeActorLinearBackgroundScroll = (actor, duration) => ActorBackgroundScrollCertificate.CanSetInterpolated(next, Events.Scene, Events.Actors, actor, duration);
        Events.CanSetPartyBackgroundScroll = (partySlot, nativeActorId, duration) => BackgroundScrollCertificate.CanSetPartyPosition(next, Events.Scene, Events.Actors, partySlot, nativeActorId, duration);
        Events.CanSetExplicitCosineBackgroundScroll = (query, scene) => ExplicitCosineScrollCertificate.CanSet(next, query, scene);
        Events.CanSetCosineBackgroundScroll = (layer, x, y, duration) => BackgroundScrollCertificate.CanSetCosinePosition(next, Events.Scene, layer, x, y, duration);
        Events.ResolveNativeNormalBackgroundScrollCenter = () => DirectBackgroundScrollCertificate.ResolveNormalCenter(next, Events.Scene, Events.Actors);
        Events.ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(next.Walkmesh, x, y, z);
        Events.ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(next.Walkmesh, triangle, x, y);
        Events.ResolveNativeTriangleCentroid = triangle => FieldPartyTraceGeometry.ResolveTriangleCentroid(next.Walkmesh, triangle);
        Events.CanUseNativeFieldEntryStack = arrival => NativeFieldEntryStackAdapter.Supports(next, arrival);
        Events.NativeFieldWalkmeshHash = next.WalkmeshHash;
        Events.CanSetLinearBackgroundScroll = query => DirectBackgroundScrollCertificate.CanSetLinear(next, query) || NativeWrappedBackgroundScrollCertificate.CanSet(next, query, Events.Scene.CameraIndex) || NativeNonselectedBackgroundScrollCertificate.CanSetLinear(next, query);
        Events.CanRestoreLinearBackgroundScroll = scene => DirectBackgroundScrollCertificate.SupportsLinearPhase(next, scene) || NativeWrappedBackgroundScrollCertificate.SupportsLinearPhase(next, scene) || NativeNonselectedBackgroundScrollCertificate.SupportsLinearPhase(next, scene);
        Events.CanBeginLinearBackgroundScroll = (query, scene) => DirectBackgroundScrollCertificate.CanBeginLinear(next, query, scene) || NativeWrappedBackgroundScrollCertificate.CanBegin(next, query, scene) || NativeNonselectedBackgroundScrollCertificate.CanBeginLinear(next, query, scene);
        Events.CanUseNativeJoinMovement = actor => NativeJoinMovementAdapter.Supports(next, Events.Scene, actor);
        Events.ResolveNativeJoinMovement = query => NativeJoinMovementAdapter.Resolve(next, Events.Scene, query);
        Events.CanUseNativePlayerMovement = actor => NativePlayerMovementAdapter.Supports(next, Events.Scene, actor);
        Events.ResolveNativePlayerMovement = query => NativePlayerMovementAdapter.Resolve(next, Events.Scene, query);
        Events.CanUseNativeScriptMove = actor => NativeScriptMovementAdapter.Supports(next, Events.Scene, actor);
        Events.ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY);
        Events.ResolveNativeScriptDirection = query => NativeScriptMovementAdapter.ResolveDirection(next, Events.Scene, query, Events.Actors.FirstOrDefault(actor => actor.ObjectId == query.ObjectId));
        Events.ResolveNativeScriptMovement = query => NativeScriptMovementAdapter.ResolveGeometry(next, Events.Scene, query, Events.Actors.FirstOrDefault(actor => actor.ObjectId == query.ObjectId));
        Events.ResolveFieldArrival = arrival =>
        {
            HashSet<int> locked = Enumerable.Range(0, next.Walkmesh.Triangles.Count).Where(index => index != arrival.Triangle).ToHashSet();
            return next.Walkmesh.TryPosition(arrival.X, arrival.Y, out Vector3 position, out int triangle, locked) ? new WalkmeshPosition(position, triangle) : null;
        };
        Events.InitInputAxisDirection = next.Information?.ControlDirection ?? 0;
        ushort? inheritedPlaceId = !restoreDeparting && snapshot is null && HasWorldPlaceArrival(Events.Scene, session.State) ? unchecked((ushort)(worldLocations ??= WorldMapLocations.ReadInstalled(assets.GameDirectory)).GetPlaceId(session.State.DepartingWorldState!.X, session.State.DepartingWorldState.Z)) : null;
        NativeFieldLoadContext? loadContext = snapshot is null ? session.State.FieldLoadContext : null;
        if (loadContext is not null)
        {
            loadContext.Validate();
            if (loadContext.FieldId != next.Id)
            {
                throw new InvalidDataException("The native field allocation request does not match the selected original resource.");
            }
        }
        if (snapshot is null && session.FieldRequestPacket is { } requestPacket)
        {
            session.SetFieldRequestPacket(requestPacket.WithConsumedRequest());
        }
        if (snapshot is null && loadContext?.Origin == NativeFieldLoadOrigin.BattleDefeat)
        {
            NativeBattleAbandonment abandoned = session.State.AbandonedNativeBattles is { Length: > 0 } history ? history[^1] : throw new InvalidDataException("Fresh defeat allocation lost its actual result1 writer.");
            Events.BindNativeFreshDefeatOutcome(abandoned);
        }
        Events.LoadField(next.Id, next.Jsm, next.Messages, arrival, previousFieldId: snapshot is null ? session.State.PreviousFieldId : null, nativeRuntimeProfile: assets.NativeRuntimeProfile, worldEntityAllocationContext: snapshot, inheritedPlaceId: inheritedPlaceId, restoringCheckpoint: snapshot is not null, fieldLoadReason: loadContext?.Reason);
        if (next.Information is FieldInformation information)
        {
            Events.InfGateways = information.Gateways.Select((gateway, index) => (Gateway: gateway, Index: index)).Where(item => item.Gateway.Defined || item.Gateway.DestinationField < 72).Select(item => CreateInfGateway(item.Gateway, item.Index)).ToArray();
            Events.InfAreaTriggers = information.AreaTriggers.Select((trigger, index) => (Trigger: trigger, Index: index)).Where(item => item.Trigger.AreaId != 255).Select(item => new InfAreaTrigger(item.Trigger.Start, item.Trigger.End, item.Trigger.AreaId, item.Trigger.Side, item.Index)).ToArray();
        }
        if (snapshot is System.Text.Json.JsonElement saved)
        {
            if (restoreDeparting)
            {
                Events.RestoreFieldState(saved);
            }
            else
            {
                Events.Restore(saved);
            }
        }
        if (snapshot is not null && session.State.Battle is null && Events.Scene.NativeFieldRegistration is { } registration)
        {
            NativeBattleRetainedMemory memory = restoreDeparting && Events.Scene.NativeEyeProcess is { } dormantEyes ? dormantEyes.Memory : session.State.NativeBattleMemory ?? throw new InvalidDataException("Registered field restore lost its shared physical resource memory.");
            Events.ValidateNativeBattleFieldRegistrationMemory(memory);
            next = assets.BindNativeRegisteredTextures(next, registration, memory.EffectMemory?.Textures ?? throw new InvalidDataException("Registered field restore lost its physical texture memory."));
        }
        Field = next;
        if (snapshot is null && session.State.RandomEncounters is { } encounter)
        {
            session.SetRandomEncounters(encounter with { Native = encounter.Native with { WorldInitialized = false } });
        }
        ValidateBattleHostState();
        RefreshSceneMods();
    }

    public int RenderWidth => renderWidth;
    public int RenderHeight => renderHeight;

    public bool SetRenderSize(int width, int height)
    {
        if (renderWidth == width && renderHeight == height)
        {
            return false;
        }
        if (session.State.Scene != SceneKind.WorldMap)
        {
            renderer = new FieldRenderer(presentationField ?? Field ?? throw new InvalidOperationException("A field must be loaded before selecting its render target."), width, height);
        }
        renderWidth = width;
        renderHeight = height;
        return true;
    }

    public byte[] Render(TimeSpan elapsed, bool rareCardMarkers = true, ITriangleRenderer? triangleRenderer = null, float worldInterpolation = 1, float fieldInterpolation = 1, bool smoothFieldAnimation = false)
    {
        if (Events.PendingTripleTriad is not null)
        {
            return RenderTripleTriad();
        }
        RequireSupportedMovieMaskRender();
        if (session.State.Scene == SceneKind.WorldMap)
        {
            EnsureField();
            return worldScene!.Render(renderWidth, renderHeight, triangleRenderer, worldInterpolation);
        }
        FieldPresentationFrame frame = CaptureFieldPresentation();
        FieldPresentationSample sample = fieldPresentation.Sample(frame, fieldInterpolation, smoothFieldAnimation);
        BackgroundScrollFrame scroll = frame.Scroll;
        DebugSceneView? view = session.State.DebugView;
        NativeFieldShake shake = Events.FieldShake ?? throw new NativeShakeRenderException("Native field rendering requires its authentic shake process owner; migrate this save explicitly.");
        shake.Validate();
        NativeFieldFadeState fade = Events.FieldFade ?? throw new NotSupportedException("Native field rendering requires its actual fade process owner.");
        fade.Validate();
        renderer!.TriangleRenderer = triangleRenderer;
        byte[] pixels = renderer.Render(Events.BackgroundStates, sample.Actors, elapsed, cameraIndex: Events.Scene.CameraIndex, backgroundModuleShades: Events.GetBackgroundModuleShades(), backgroundVisible: view?.BackgroundVisible ?? true, hiddenBackgroundModules: view?.HiddenBackgroundModules, backgroundScrollOffsets: scroll.LayerOffsets, actorScrollOffset: scroll.ActorOffset, nativeWrapFrame: scroll.NativeWrap, drawingJitterX: shake.X.Output, drawingJitterY: shake.Y.Output, cardMarkers: rareCardMarkers ? CreateCardMarkers(elapsed) : null, drawPointParticles: Events.DrawPointParticleFrame, drawPointParticleTables: Events.DrawPointParticleTables, drawPointDisplayMask: Events.Scene.NativeDrawPointDisplayMask, fade: fade.Draw, backgroundVisibility: SceneBackgroundVisibility(), backgroundBlend: sample.Background, backgroundLayerScales: scroll.LayerScales);
        if (triangleRenderer is null)
        {
            FieldColorComposite.Apply(pixels, fade.Draw, renderWidth, renderer.ContentBounds);
        }
        return pixels;
    }

    public PlayerActionResult Apply(PlayerAction action)
    {
        EnsureField();
        if (PendingEntityName is not null)
        {
            if (action.Kind == PlayerActionKind.EnterName)
            {
                if (action.Value != 0 || action.Quantity is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
                {
                    throw new ArgumentException("Entity naming accepts only its name text.", nameof(action));
                }
                CompleteEntityName(action.Text ?? PendingEntityName.DefaultName);
                return new PlayerActionResult(true, NextTrigger, 0);
            }
            return new PlayerActionResult(false, new PlayerTrigger(PlayerTriggerKind.Dialogue, session.State.FieldId, Events.Frame, null, PendingEntityName.DefaultName), 0, "Complete the required entity name before continuing.");
        }
        if (Events.Scene.NativeTutorial is not null || Events.Scene.NativeMenuTutorial is not null)
        {
            return ApplyNativeTutorial(action);
        }
        if (action.Kind == PlayerActionKind.UseFieldItem && FieldItemCatalog.FieldItem(action.Value) is { SupportsBattleUse: true } battleItem)
        {
            return ApplyMenuBattleItem(action, battleItem);
        }
        if (action.Kind == PlayerActionKind.UseFieldItem && FieldItemCatalog.FieldItem(action.Value) is { SupportsNameUse: true } namingItem)
        {
            return ApplyItemNaming(action, namingItem);
        }
        int acquiredBeforeItem = action.Kind == PlayerActionKind.UseFieldItem ? AcquiredGuardianForceMask() : 0;
        if (action.Kind is PlayerActionKind.TriadSelectCard or PlayerActionKind.TriadPlaceCard or PlayerActionKind.TriadAutoMove or PlayerActionKind.TriadTradeCard || Events.PendingTripleTriad is not null && action.Kind is PlayerActionKind.Confirm or PlayerActionKind.Cancel or PlayerActionKind.Wait)
        {
            return ApplyTripleTriad(action);
        }
        if (session.State.Scene == SceneKind.WorldMap)
        {
            PlayerActionResult worldResult = action.Kind == PlayerActionKind.ReadMagazinePage ? worldScene!.ReadMagazinePage(action, FieldItemCatalog.MagazinePage(action.Value, action.Quantity ?? -1)) : action.Kind == PlayerActionKind.UseFieldItem ? worldScene!.UseFieldItem(action, FieldItemCatalog.FieldItem(action.Value) ?? throw new NotSupportedException("World item use requires original installed metadata."), assets.ReadFieldItemStatRules) : action.Kind == PlayerActionKind.RefineItems ? worldScene!.RefineItems(action, assets.ReadNativeRefinementDefinitions()) : worldScene!.Apply(action);
            ObservePendingBattle();
            if (Field is not null && session.State.Scene != SceneKind.WorldMap)
            {
                session.SetDepartingFieldState(Events.Capture());
            }
            if (worldResult.Accepted && action.Kind == PlayerActionKind.UseFieldItem)
            {
                QueueItemGuardianForceNames(action.Value, acquiredBeforeItem);
            }
            return worldResult;
        }
        PlayerActionResult result = Events.Apply(action);
        if (result.Accepted && action.Kind == PlayerActionKind.UseFieldItem)
        {
            QueueItemGuardianForceNames(action.Value, acquiredBeforeItem);
        }
        return PublishFieldAction(result);
    }

    private PlayerActionResult PublishFieldAction(PlayerActionResult result)
    {
        if (Events.FieldId != session.State.FieldId || Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition || session.State.Scene == SceneKind.WorldMap)
        {
            session.SetDepartingFieldState(Events.Capture());
            return result;
        }
        ActorState? player = Events.PlayerActor;
        if (player is { Active: true, Visible: true })
        {
            session.SetPosition(player.X, player.Y);
        }
        return result;
    }

    public void Dispose()
    {
        try
        {
            session.CompleteSnapshotPreparation(snapshotPreparation);
        }
        finally
        {
            Events.OperationCompleted -= ObservePendingBattle;
            spuAudio?.Dispose();
            if (ownsMovieClock)
            {
                movieClock.Dispose();
            }
            if (ownsAssets)
            {
                assets.Dispose();
            }
            battleAssets?.Dispose();
            preparedWorldEntry?.Dispose();
            worldScene?.Dispose();
        }
    }
}

internal static class FieldColorComposite
{
    public static void Apply(byte[] rgba, NativeFadeDraw draw, int width = 0, PixelBounds? bounds = null)
    {
        int start = bounds is PixelBounds clip ? (clip.Y * width + clip.X) * 4 : 0;
        int rowBytes = bounds is PixelBounds area ? area.Width * 4 : rgba.Length;
        int rows = bounds?.Height ?? 1;
        int stride = bounds.HasValue ? width * 4 : rgba.Length;
        if (draw.Mode == NativeFadeDrawMode.Black)
        {
            for (int row = 0; row < rows; row++)
            {
                int rowStart = start + row * stride;
                for (int offset = rowStart; offset < rowStart + rowBytes; offset += 4)
                {
                    rgba[offset] = 0;
                    rgba[offset + 1] = 0;
                    rgba[offset + 2] = 0;
                }
            }
            return;
        }
        Vector3 additive = draw.Mode == NativeFadeDrawMode.Add ? new(draw.Red, draw.Green, draw.Blue) : Vector3.Zero;
        Vector3 subtractive = draw.Mode == NativeFadeDrawMode.Subtract ? new(draw.Red, draw.Green, draw.Blue) : Vector3.Zero;
        if (additive == Vector3.Zero && subtractive == Vector3.Zero)
        {
            return;
        }
        Span<byte> red = stackalloc byte[256];
        Span<byte> green = stackalloc byte[256];
        Span<byte> blue = stackalloc byte[256];
        for (int value = 0; value < 256; value++)
        {
            red[value] = (byte)Math.Clamp(MathF.Round(value + additive.X - subtractive.X), 0, 255);
            green[value] = (byte)Math.Clamp(MathF.Round(value + additive.Y - subtractive.Y), 0, 255);
            blue[value] = (byte)Math.Clamp(MathF.Round(value + additive.Z - subtractive.Z), 0, 255);
        }
        for (int row = 0; row < rows; row++)
        {
            int rowStart = start + row * stride;
            for (int offset = rowStart; offset < rowStart + rowBytes; offset += 4)
            {
                rgba[offset] = red[rgba[offset]];
                rgba[offset + 1] = green[rgba[offset + 1]];
                rgba[offset + 2] = blue[rgba[offset + 2]];
            }
        }
    }
}
