using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>The live host and diagnostic commands share one persistent VM across field loads.</summary>
internal sealed partial class GameWorld : IDisposable
{
    private readonly GameSession session;
    private readonly GameAssets assets;
    private FieldRenderer? renderer;
    private WorldMapScene? worldScene;
    private int renderWidth = FieldRenderer.Width;
    private int renderHeight = FieldRenderer.Height;

    public GameWorld(GameSession session, string gameDirectory, MovieClock? movieClock = null, bool persistReplayJournal = true)
    {
        this.session = session;
        assets = new GameAssets(gameDirectory);
        bool initializedNativeParty = false;
        if (session.State.WeaponRecipes is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.ReplayJournal is { EntryCount: 0, IsComplete: true } && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameWeaponRecipes(assets.ReadNewGameWeaponRecipes());
        }
        if (session.State.ShopHistory is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.ReplayJournal is { EntryCount: 0, IsComplete: true } && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameShopHistory(assets.ReadNewGameShopHistory());
        }
        if (session.State.ShotAvailability is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.ReplayJournal is { EntryCount: 0, IsComplete: true } && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        }
        if (session.State.InputConfiguration is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.ReplayJournal is { EntryCount: 0, IsComplete: true } && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        }
        if (session.State.SeeD is { SalaryTable: null } && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.ReplayJournal is { EntryCount: 0, IsComplete: true } && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        }
        if (session.State.Economy is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.ReplayJournal is { EntryCount: 0, IsComplete: true } && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        }
        if (session.State.CharacterVitals is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.ReplayJournal is { EntryCount: 0, IsComplete: true } && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            initializedNativeParty = true;
        }
        if (session.State.CharacterAppearance is null && session.State.Scene == SceneKind.Field && session.State.FieldId == 74 && session.State.EventState is null && session.State.ReplayJournal is { EntryCount: 0, IsComplete: true } && session.State.SavedWorld?.Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame)
        {
            session.InitializeNewGameCharacterAppearance(assets.ReadNewGameCharacterAppearance());
        }
        MovieClock clock = movieClock ?? new MovieClock(session, gameDirectory);
        Events = new EventEngine(session);
        ConfigureBattleHost();
        if (assets.NativeRuntimeProfile is { IsVerified: true })
        {
            Events.ShakeTables = assets.ReadNativeShakeTables();
        }
        if (initializedNativeParty)
        {
            // Native470560 writes field-party DWORD1CFE990=FFFFFF00 after actual New Game.
            Events.Scene.Party.Clear();
            Events.Scene.Party.AddRange([0, 255, 255]);
        }
        Events.MoviePlayback = () => clock.State;
        clock.AdvanceFrame = AdvanceMovieFrame;
        Events.ReadMovieCameraFlags = ReadNativeMovieCameraFlags;
        Events.MovieSkipRequested += clock.Skip;
        Events.LoadSoundResource = _ => SoundResourceStatus.Muted;
        Events.ResolveGlobalSoundResource = ResolveGlobalSound;
        Events.PrepareWorldTransition = PrepareNativeWorldEntry;
        Events.ReadNativeVibrationBank = assets.ReadNativeVibrationBank;
        Events.ResolveNativeShopDefinitions = assets.ReadNativeShopDefinitions;
        Events.ResolveNativeWeaponShopDefinitions = assets.ReadNativeWeaponShopDefinitions;
        Events.ReadNativeDrawPointCatalog = assets.ReadNativeDrawPointCatalog;
        Events.ReadSteamDiscMount = selectedDisc => NativeSteamDiscMountReader.Read(assets.GameDirectory, selectedDisc);
        if (persistReplayJournal)
        {
            journalArchive = new ReplayJournalArchive(ReplayJournalArchive.DefaultDirectory);
            session.ReplayJournalChanged += PersistReplayJournal;
            if (session.ReplayJournal is ReplayJournal journal)
            {
                FlushReplayJournal(journal);
            }
        }
    }

    public EventEngine Events { get; }

    internal bool AdvanceMovieFrame(MoviePlaybackState playback, double seconds)
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
            Events.Step();
        }
        return session.IsMoviePlaying && session.State.MovieIndex == movie && session.State.FieldId == field && session.State.MovieSource == source;
    }
    public FieldAssets? Field { get; private set; }
    public WorldMapScene? WorldScene => worldScene;
    public DialogueState? CurrentDialogue => session.State.Scene == SceneKind.WorldMap ? worldScene?.CurrentDialogue : Events.CurrentDialogue;
    public PlayerTrigger NextTrigger => worldScene is not null && session.State.Scene == SceneKind.WorldMap ? worldScene.CurrentDialogue is { } dialogue ? new PlayerTrigger(PlayerTriggerKind.Dialogue, session.State.FieldId, worldScene.State.Frame, dialogue.MessageId, dialogue.Text) : new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, worldScene.State.Frame, null, null) : Events.FieldId != session.State.FieldId ? new PlayerTrigger(PlayerTriggerKind.FieldTransition, session.State.FieldId, worldScene?.LastDeparture?.Frame ?? Events.Frame, null, null) : Events.NextTrigger;

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
                throw new NotSupportedException("World restoration requires its executed native music-loader receipt; recreate the authentic journal.");
            }
            worldScene = new WorldMapScene(session, assets.GameDirectory);
            worldScene.PrepareFieldEntry = PrepareNativeWorldFieldEntry;
            ConfigureWorldSalaryEffects(worldScene);
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
        if (!returningFromWorld && Field?.Id == session.State.FieldId && Events.NextTrigger.Kind != PlayerTriggerKind.FieldTransition)
        {
            return false;
        }
        if (Field is null && session.State.DepartingFieldState is System.Text.Json.JsonElement departing)
        {
            LoadField(departing.GetProperty("FieldId").GetInt32(), null, departing, restoreDeparting: true);
        }
        LoadField(session.State.FieldId, session.State.FieldArrival, session.State.EventState);
        session.SetDepartingFieldState(null);
        return true;
    }

    private void LoadField(int fieldId, FieldTransition? arrival, System.Text.Json.JsonElement? snapshot, bool restoreDeparting = false)
    {
        ValidateDiscJumpArrival(arrival, snapshot is null);
        if (restoreDeparting && (session.EventRandom is null || session.VibrationProcess is null || session.FieldFade is null || session.ShopHistory is null || session.MenuRandom is null || session.WeaponRecipes is null))
        {
            throw new NotSupportedException("Restoring a dormant field cannot supply the unknown current world event/menu random, vibration, fade or shop owners from its earlier snapshot.");
        }
        using IDisposable? restoredLoad = snapshot is null ? null : session.BeginReplayCapture();
        FieldAssets next = assets.LoadField(fieldId);
        Events.ResolveFieldSoundEffectTable = () =>
        {
            FieldSoundEffectTable table = assets.LoadFieldSoundEffects(next.Id);
            return new NativeFieldSoundTable(table.MemberName, table.Sha256, table.EffectIds);
        };
        Events.AnimationFrameCount = next.GetAnimationFrameCount;
        Events.ReadNativeModelParts = model => (uint)model < next.Models.Count ? next.Models[model].NativeParts : null;
        Events.CaptureFootstepModelOrigin = query => NativeFootstepSoundAdapter.Capture(next, Events.Scene, query, session.IsMoviePlaying);
        Events.ResolveFootstepSound = (actor, mode) => NativeFootstepSoundAdapter.Resolve(next, Events.Scene, actor, mode, () => assets.LoadFieldSoundEffects(next.Id), session.IsMoviePlaying);
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
        Events.CanRestoreNativeActorScroll = (scene, actors) => ActorBackgroundScrollCertificate.CanRestore(next, scene, actors);
        Events.CanSetNativeActorCosineBackgroundScroll = (actor, duration) => ActorBackgroundScrollCertificate.CanSetInterpolated(next, Events.Scene, Events.Actors, actor, duration);
        Events.CanSetNativeActorLinearBackgroundScroll = (actor, duration) => ActorBackgroundScrollCertificate.CanSetInterpolated(next, Events.Scene, Events.Actors, actor, duration);
        Events.CanSetPartyBackgroundScroll = (partySlot, nativeActorId, duration) => BackgroundScrollCertificate.CanSetPartyPosition(next, Events.Scene, Events.Actors, partySlot, nativeActorId, duration);
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
        NativeWorldControlState? sceneControls = restoreDeparting ? session.WorldControls : null;
        NativeExamTimerState? sceneTimer = restoreDeparting ? session.State.ExamTimer : null;
        NativeSeeDState? sceneSeeD = restoreDeparting ? session.SeeD : null;
        NativeEconomy? sceneEconomy = restoreDeparting ? session.Economy : null;
        NativeGlobalSoundProcess? sceneSoundProcess = restoreDeparting ? session.GlobalSoundProcess : null;
        NativeSalaryPresentation? sceneSalaryPresentation = restoreDeparting ? session.SalaryPresentation : null;
        NativeEventRandomState? sceneEventRandom = restoreDeparting ? session.EventRandom : null;
        NativeVibrationProcessState? sceneVibration = restoreDeparting ? session.VibrationProcess : null;
        NativeFieldFadeState? sceneFade = restoreDeparting ? session.FieldFade : null;
        NativeShopHistory? sceneShopHistory = restoreDeparting ? session.ShopHistory : null;
        NativeMenuRandomState? sceneMenuRandom = restoreDeparting ? session.MenuRandom : null;
        NativeWeaponRecipes? sceneWeaponRecipes = restoreDeparting ? session.WeaponRecipes : null;
        Events.LoadField(next.Id, next.Jsm, next.Messages, arrival, previousFieldId: snapshot is null ? session.State.PreviousFieldId : null, nativeRuntimeProfile: assets.NativeRuntimeProfile, worldEntityAllocationContext: snapshot);
        if (next.Information is FieldInformation information)
        {
            Events.InfGateways = information.Gateways.Select((gateway, index) => (Gateway: gateway, Index: index)).Where(item => item.Gateway.Defined || item.Gateway.DestinationField < 72).Select(item => CreateInfGateway(item.Gateway, item.Index)).ToArray();
            Events.InfAreaTriggers = information.AreaTriggers.Select((trigger, index) => (Trigger: trigger, Index: index)).Where(item => item.Trigger.AreaId != 255).Select(item => new InfAreaTrigger(item.Trigger.Start, item.Trigger.End, item.Trigger.AreaId, item.Trigger.Side, item.Index)).ToArray();
        }
        if (snapshot is System.Text.Json.JsonElement saved)
        {
            Events.Restore(saved);
            if (sceneControls is not null)
            {
                // The departing VM precedes world flags/menu changes; the current session owns DWORD104 and BYTE207/209.
                session.SetWorldControls(sceneControls);
            }
            if (sceneTimer is not null)
            {
                // The frozen departing VM precedes world callbacks; the session owns the shared native clock.
                session.SetExamTimer(sceneTimer);
            }
            if (sceneSeeD is not null)
            {
                session.SetSeeD(sceneSeeD);
            }
            if (sceneEconomy is not null)
            {
                session.SetEconomy(sceneEconomy);
            }
            if (sceneSoundProcess is not null)
            {
                session.SetGlobalSoundProcess(sceneSoundProcess);
            }
            if (sceneSalaryPresentation is not null)
            {
                session.SetSalaryPresentation(sceneSalaryPresentation);
            }
            if (sceneEventRandom is not null)
            {
                session.SetEventRandom(sceneEventRandom);
            }
            if (sceneWeaponRecipes is not null)
            {
                session.SetWeaponRecipes(sceneWeaponRecipes);
            }
            if (sceneMenuRandom is not null)
            {
                session.SetMenuRandom(sceneMenuRandom);
            }
            if (sceneShopHistory is not null)
            {
                session.SetShopHistory(sceneShopHistory);
            }
            if (sceneFade is not null)
            {
                session.SetFieldFade(sceneFade);
            }
            if (sceneVibration is not null)
            {
                session.SetVibrationProcess(sceneVibration);
            }
        }
        Field = next;
        ValidateBattleHostState();
        renderer = new FieldRenderer(next, renderWidth, renderHeight);
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
            renderer = new FieldRenderer(Field ?? throw new InvalidOperationException("A field must be loaded before selecting its render target."), width, height);
        }
        renderWidth = width;
        renderHeight = height;
        return true;
    }

    public byte[] Render(TimeSpan elapsed)
    {
        RequireSupportedMovieMaskRender();
        if (session.State.Scene == SceneKind.WorldMap)
        {
            EnsureField();
            return worldScene!.Render(renderWidth, renderHeight);
        }
        IEnumerable<ActorVisual> visuals = Events.Actors.Select(CreateVisual);
        BackgroundScrollFrame scroll = ActorBackgroundScrollCertificate.Prepare(Field!, Events.Scene) ?? DirectBackgroundScrollCertificate.Prepare(Field!, Events.Scene) ?? NativeWrappedBackgroundScrollCertificate.Prepare(Field!, Events.Scene, Events.Actors) ?? NormalCameraCertificate.Prepare(Field!, Events.Scene, Events.Actors) ?? throw new NotSupportedException("Field rendering requires a supported current native camera contract.");
        DebugSceneView? view = session.State.DebugView;
        NativeFieldShake shake = Events.FieldShake ?? throw new NativeShakeRenderException("Native field rendering requires its authentic shake process owner; recreate the session journal.");
        shake.Validate();
        NativeFieldFadeState fade = Events.FieldFade ?? throw new NotSupportedException("Native field rendering requires its actual fade process owner.");
        fade.Validate();
        byte[] pixels = renderer!.Render(Events.BackgroundStates, visuals, elapsed, cameraIndex: Events.Scene.CameraIndex, backgroundModuleShades: Events.GetBackgroundModuleShades(), backgroundVisible: view?.BackgroundVisible ?? true, hiddenBackgroundModules: view?.HiddenBackgroundModules, backgroundScrollOffsets: scroll.LayerOffsets, actorScrollOffset: scroll.ActorOffset, nativeWrapFrame: scroll.NativeWrap, drawingJitterX: shake.X.Output, drawingJitterY: shake.Y.Output);
        FieldColorComposite.Apply(pixels, fade.Draw);
        return pixels;
    }

    public PlayerActionResult Apply(PlayerAction action)
    {
        EnsureField();
        if (session.State.Scene == SceneKind.WorldMap)
        {
            PlayerActionResult worldResult = worldScene!.Apply(action);
            if (Field is not null)
            {
                // The field VM is dormant; Capture reads the current process-owned globals.
                if (session.State.Scene == SceneKind.WorldMap)
                {
                    session.SetEventState(Events.Capture());
                }
                else
                {
                    session.SetDepartingFieldState(Events.Capture());
                }
            }
            return worldResult;
        }
        PlayerActionResult result = Events.Apply(action);
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
        session.ReplayJournalChanged -= PersistReplayJournal;
        if (journalArchive is not null && session.ReplayJournal is ReplayJournal journal)
        {
            FlushReplayJournal(journal);
        }
        assets.Dispose();
        battleAssets?.Dispose();
        preparedWorldEntry?.Dispose();
        worldScene?.Dispose();
    }
}

internal static class FieldColorComposite
{
    public static void Apply(byte[] rgba, NativeFadeDraw draw)
    {
        if (draw.Mode == NativeFadeDrawMode.Black)
        {
            for (int offset = 0; offset < rgba.Length; offset += 4)
            {
                rgba[offset] = 0;
                rgba[offset + 1] = 0;
                rgba[offset + 2] = 0;
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
        for (int offset = 0; offset < rgba.Length; offset += 4)
        {
            rgba[offset] = red[rgba[offset]];
            rgba[offset + 1] = green[rgba[offset + 1]];
            rgba[offset + 2] = blue[rgba[offset + 2]];
        }
    }
}
