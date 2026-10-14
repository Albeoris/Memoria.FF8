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
        Events = new EventEngine(session, session.State.Variables);
        if (assets.NativeRuntimeProfile is { IsVerified: true })
        {
            Events.ShakeTables = assets.ReadNativeShakeTables();
        }
        if (initializedNativeParty)
        {
            // Native470560 writes field-party DWORD1CFE990=FFFFFF00 after actual New Game.
            Events.Scene.Party.Add(0);
            Events.Scene.Members.Add(0);
        }
        Events.MoviePlayback = () => clock.State;
        Events.MovieSkipRequested += clock.Skip;
        Events.LoadSoundResource = _ => SoundResourceStatus.Muted;
        Events.ResolveGlobalSoundResource = ResolveGlobalSound;
        Events.PrepareWorldTransition = PrepareNativeWorldEntry;
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
    public FieldAssets? Field { get; private set; }
    public WorldMapScene? WorldScene => worldScene;
    public PlayerTrigger NextTrigger => worldScene is not null && session.State.Scene == SceneKind.WorldMap ? new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, worldScene.State.Frame, null, null) : Events.FieldId != session.State.FieldId ? new PlayerTrigger(PlayerTriggerKind.FieldTransition, session.State.FieldId, worldScene?.LastDeparture?.Frame ?? Events.Frame, null, null) : Events.NextTrigger;

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
            worldScene = new WorldMapScene(session, assets.GameDirectory);
            worldScene.PrepareFieldEntry = PrepareNativeWorldFieldEntry;
            if (session.State.DepartingFieldState is System.Text.Json.JsonElement frozenDeparture)
            {
                session.SetEventState(frozenDeparture);
            }
            session.SetDepartingFieldState(null);
            session.DismissDialog();
            return true;
        }
        if (worldScene is not null)
        {
            worldScene.Dispose();
            worldScene = null;
        }
        if (Field?.Id == session.State.FieldId && Events.NextTrigger.Kind != PlayerTriggerKind.FieldTransition)
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
        using IDisposable? restoredLoad = snapshot is null ? null : session.BeginReplayCapture();
        FieldAssets next = assets.LoadField(fieldId);
        Events.ResolveFieldSoundEffectTable = () =>
        {
            FieldSoundEffectTable table = assets.LoadFieldSoundEffects(next.Id);
            return new NativeFieldSoundTable(table.MemberName, table.Sha256, table.EffectIds);
        };
        Events.AnimationFrameCount = next.GetAnimationFrameCount;
        Events.ReadFaceBaseline = actor =>
        {
            next.GetAnimationFrameCount(actor.ModelId, actor.AnimationId);
            Vector3 rotation = next.Models[actor.ModelId].ReadFirstControlRotation(actor.AnimationId, 0);
            return new FaceDirectionBaseline((int)MathF.Round(rotation.Y * 4096 / MathF.Tau) / 16, (int)MathF.Round(rotation.Z * 4096 / MathF.Tau) / 16, (int)MathF.Round(rotation.X * 4096 / MathF.Tau) / 16);
        };
        Events.ReadFaceAnchorHeight = actor => next.Models[actor.ModelId].GetFirstControlBoneHeight(actor.AnimationId, actor.AnimationFrame, actor.RootTranslationZ, actor.FirstControlRotation);
        Events.CanDisableAdditionalControl = actor =>
        {
            FieldModel model = next.Models[actor.ModelId];
            int additional = model.ControlBones.Additional;
            return (uint)additional >= model.Bones.Count || !model.Bones.Any(bone => bone.Parent == additional);
        };
        Events.ResolveWalkmesh = (x, y) => next.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle, Events.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
        Events.CanSetBackgroundScroll = (layer, settings) => BackgroundScrollCertificate.CanSetForState(next, Events.Scene, Events.Actors, layer, settings);
        Events.CanSetBackgroundScrollPosition = (layer, x, y) => DirectBackgroundScrollCertificate.CanSet(next, Events.Scene, layer, x, y);
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
        Events.CanSetLinearBackgroundScroll = query => DirectBackgroundScrollCertificate.CanSetLinear(next, query) || NativeLinearBackgroundScrollCertificate.CanSet(next, query);
        Events.CanRestoreLinearBackgroundScroll = scene => DirectBackgroundScrollCertificate.SupportsLinearPhase(next, scene) || NativeLinearBackgroundScrollCertificate.SupportsPhase(next, scene);
        Events.CanBeginLinearBackgroundScroll = (query, scene) => DirectBackgroundScrollCertificate.CanBeginLinear(next, query, scene) || NativeLinearBackgroundScrollCertificate.CanBegin(next, query, scene);
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
        Events.LoadField(next.Id, next.Jsm, next.Messages, arrival, previousFieldId: snapshot is null ? session.State.PreviousFieldId : null, nativeRuntimeProfile: assets.NativeRuntimeProfile, worldEntityAllocationContext: snapshot);
        if (next.Information is FieldInformation information)
        {
            Events.InfGateways = information.Gateways.Select((gateway, index) => (Gateway: gateway, Index: index)).Where(item => item.Gateway.Defined || item.Gateway.DestinationField < 72).Select(item => CreateInfGateway(item.Gateway, item.Index)).ToArray();
            Events.InfAreaTriggers = information.AreaTriggers.Where(trigger => trigger.AreaId != 255).Select(trigger => new InfAreaTrigger(trigger.Start, trigger.End, trigger.AreaId, trigger.Side)).ToArray();
        }
        if (snapshot is System.Text.Json.JsonElement saved)
        {
            NativeExamTimerState? sceneTimer = restoreDeparting ? session.State.ExamTimer : null;
            Events.Restore(saved);
            if (sceneTimer is not null)
            {
                // The frozen departing VM precedes world callbacks; the session owns the shared native clock.
                session.SetExamTimer(sceneTimer);
            }
        }
        Field = next;
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
        if (session.State.Scene == SceneKind.WorldMap)
        {
            EnsureField();
            return worldScene!.Render(renderWidth, renderHeight);
        }
        IEnumerable<ActorVisual> visuals = Events.Actors.Select(CreateVisual);
        BackgroundScrollFrame scroll = ActorBackgroundScrollCertificate.Prepare(Field!, Events.Scene) ?? DirectBackgroundScrollCertificate.Prepare(Field!, Events.Scene) ?? NativeLinearBackgroundScrollCertificate.Prepare(Field!, Events.Scene, Events.Actors) ?? NormalCameraCertificate.Prepare(Field!, Events.Scene, Events.Actors) ?? throw new NotSupportedException("Field rendering requires a supported current native camera contract.");
        DebugSceneView? view = session.State.DebugView;
        if (Events.Scene.Shake is NativeFieldShake shake)
        {
            Vector2 jitter = new(shake.X.RequireUniqueOutput(), shake.Y.RequireUniqueOutput());
            if (jitter != Vector2.Zero)
            {
                if (scroll.NativeWrap is not null)
                {
                    throw new NotSupportedException("Native wrapped background shake placement requires a separate shared renderer contract.");
                }
                scroll = new BackgroundScrollFrame(Field!.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, layer => scroll.LayerOffsets.GetValueOrDefault(layer) + jitter), scroll.ActorOffset + jitter);
            }
        }
        byte[] pixels = renderer!.Render(Events.BackgroundStates, visuals, elapsed, cameraIndex: Events.Scene.CameraIndex, backgroundModuleShades: Events.GetBackgroundModuleShades(), backgroundVisible: view?.BackgroundVisible ?? true, hiddenBackgroundModules: view?.HiddenBackgroundModules, backgroundScrollOffsets: scroll.LayerOffsets, actorScrollOffset: scroll.ActorOffset, nativeWrapFrame: scroll.NativeWrap);
        FieldColorComposite.Apply(pixels, Events.Scene.AdditiveColor, Events.Scene.SubtractiveColor);
        return pixels;
    }

    public PlayerActionResult Apply(PlayerAction action)
    {
        EnsureField();
        if (session.State.Scene == SceneKind.WorldMap)
        {
            return worldScene!.Apply(action);
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
        ActorState? player = Events.Actors.FirstOrDefault(actor => actor.Active && actor.Visible && actor.CharacterId == 0);
        if (player is not null)
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
        preparedWorldEntry?.Dispose();
        worldScene?.Dispose();
    }
}

internal static class FieldColorComposite
{
    public static void Apply(byte[] rgba, Vector3 additive, Vector3 subtractive)
    {
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
