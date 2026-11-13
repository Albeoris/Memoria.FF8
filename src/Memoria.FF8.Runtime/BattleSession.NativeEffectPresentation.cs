using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private Dictionary<int, NativeBattleForeignActor>? presentationForeignActors;
    internal NativeBattleArenaHost? presentationArena;
    private NativeBattleEffectSceneContext? presentationSceneContext;
    private NativeBattleRibbonProjectionPolicy presentationRibbonProjectionPolicy;
    private NativeBattleGraphicsResource? presentationSourceResource;
    private bool presentationFirstAdvance = true;
    private NativeBattleCameraState? presentationHostCamera;
    private NativeBattleStageState? presentationInitialStage;
    private NativeBattleUiServices? presentationInitialUi;
    private ImmutableArray<NativeBattleGraphicsModel> presentationInitialModels = [];
    private int? presentationPartyActorCount;
    private int? presentationInitialMusicGain;
    private readonly List<NativeBattleContactPacket> presentationContacts = [];
    private readonly List<NativeBattleModuleSound> presentationSounds = [];
    private readonly List<NativeBattleForeignSequenceRequest> presentationSequences = [];
    internal bool EffectPresentationInvoked { get; private set; }

    /// <summary>The host retains its camera projection. Native control resources may register without acquiring a camera lease; their control programs are not executed here.</summary>
    private BattleSession(NativeBattleEffectPresentationInputs inputs)
    {
        definition = inputs.Resources;
        presentationArena = inputs.Arena;
        presentationSceneContext = inputs.SceneContext;
        presentationSceneContext?.Validate();
        if (!Enum.IsDefined(inputs.RibbonProjectionPolicy))
        {
            throw new InvalidDataException("The presentation ribbon projection policy is unsupported.");
        }
        presentationRibbonProjectionPolicy = inputs.RibbonProjectionPolicy;
        presentationPartyActorCount = inputs.PartyActorCount;
        combatants = [];
        byId = new(StringComparer.Ordinal);
        handlers = [];
        Combatants = combatants.AsReadOnly();
        random = NativeBattleRandomState.Initialize(0);
        nativeCrtRandom = inputs.Random;
        nativeGlobalRandom = definition.NativeGlobalRandom;
        presentationSourceResource = inputs.SourceResource;
        presentationForeignActors = inputs.ForeignActors.ToDictionary(actor => actor.Index);
        nativeActionPackets = inputs.Packets.ActivateHeader(inputs.Parameters.HeaderIndex7C);
        NativeEffectMemory = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(definition.NativeRetainedMemory?.EffectMemory ?? NativeBattleEffectMemory.FromColdProcess())) with { Textures = inputs.Textures };
        NativeEffectMemory = NativeBattleGraphicsInitialization.Initialize500520(NativeEffectMemory);
        NativeCamera = BindEffectPresentationHostProjection(NativeBattleCameraState.FromColdProcess(), inputs.Projection);
        presentationHostCamera = NativeCamera;
        NativeFileStaging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.FromColdProcess());
        NativeFileIo = NativeBattleFileIoState.FromColdProcess();
        NativeGlobalSoundProcess = definition.NativeGlobalSoundProcess ?? NativeGlobalSoundProcess.FromBufferlessColdProcess();
        presentationInitialMusicGain = inputs.InitialMusicGain;
        if (inputs.InitialMusicGain is { } musicGain)
        {
            if (musicGain is < 0 or > 127)
            {
                throw new InvalidDataException("The presentation music gain must be within its native relative range0..127.");
            }
            NativeMusicPlaybackState playback = NativeGlobalSoundProcess.MusicPlayback ?? throw new NotSupportedException("The presentation music gain requires its current playback owner.");
            NativeGlobalSoundProcess = NativeGlobalSoundProcess with { MusicPlayback = playback.SetGain46BB40(0, musicGain) };
        }
        NativeVibrationProcess = definition.NativeVibrationProcess ?? definition.NativeRetainedMemory?.VibrationProcess ?? NativeVibrationProcessState.FromColdProcess();
        NativeStage = inputs.Arena?.CreatePresentationStage() ?? NativeBattleStageState.FromColdProcess();
        presentationInitialStage = NativeStage;
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray().SetItem(0, inputs.Source);
        models = BindPresentationForeignModels(models);
        presentationInitialUi = NativeBattleUiServices.Initialize();
        NativeGraphics = InitializeNativePrimitiveFrame500400(new(models, Enumerable.Range(0, models.Length).Select(index => index == 0 ? "source" : presentationForeignActors.ContainsKey(index) ? $"host-{index}" : null).ToImmutableArray(), 0, 0) { UiServices = presentationInitialUi });
        NativeGraphics = InitializeEffectPresentationSourceResources(NativeGraphics, inputs.SourceResource);
        InitializeEffectPresentationServices(inputs.Parameters);
        presentationInitialModels = NativeGraphics!.Models;
        InitializeNativePrimitiveDepth47CE10();
        NativeGraphics = BeginNativeOriginalEffect(NativeGraphics!, inputs.Effect);
        NativeGraphics = NativeGraphics with { OriginalEffect = NativeGraphics.OriginalEffect! with { Parameters = inputs.Parameters } };
        if (!inputs.DeferInvocation)
        {
            InvokeEffectPresentation();
        }
        ValidateEffectPresentationServices();
    }

    internal static BattleSession CreateEffectPresentation(NativeBattleEffectPresentationInputs inputs) => new(inputs);

    private NativeBattleEffectSceneContext ReadRequiredEffectSceneContext()
    {
        return presentationSceneContext ?? NativeBattleEffectSceneContext.FromNativeEncounter(checked((ushort)(definition.EncounterId ?? throw new NotSupportedException("The native effect requires its scene placement context."))));
    }

    internal void ReleaseEffectPresentation()
    {
        presentationArena?.ClearPresentation();
        NativeStage = presentationInitialStage;
        if (presentationInitialMusicGain is { } musicGain && NativeGlobalSoundProcess is { MusicPlayback: { } playback } sound)
        {
            NativeGlobalSoundProcess = sound with { MusicPlayback = playback.SetGain46BB40(0, musicGain) };
        }
        if (NativeGraphics is { } graphics)
        {
            ImmutableArray<NativeBattleGraphicsModel> models = graphics.Models.Select((model, index) =>
            {
                NativeBattleGraphicsModel original = presentationInitialModels[index];
                return model with { Flags0 = (ushort)((model.Flags0 & ~0x804) | (original.Flags0 & 0x804)), Color40 = original.Color40, Color44 = original.Color44, Alpha7 = original.Alpha7 };
            }).ToImmutableArray();
            NativeGraphics = graphics with { UiServices = presentationInitialUi, Models = models };
        }
    }

    internal void InvokeEffectPresentation()
    {
        if (EffectPresentationInvoked)
        {
            throw new InvalidOperationException("The native presentation descriptor was already invoked.");
        }
        NativeGraphics = InvokeNativeOriginalEffect(NativeGraphics!);
        EffectPresentationInvoked = true;
    }

    internal void InvokeEffectPresentation(NativeBattleGraphicsModel source, NativeCrtRandomState randomState, NativeBattleEffectTextureState textures)
    {
        if (EffectPresentationInvoked)
        {
            throw new InvalidOperationException("The native presentation descriptor was already invoked.");
        }
        NativeBattleGraphicsModel loaded = NativeGraphics!.Models[0];
        source = source with { ActiveResourceRow100 = loaded.ActiveResourceRow100, OriginalResourceRow104 = loaded.OriginalResourceRow104, TextureResourceIndex = loaded.TextureResourceIndex, LinkedTextureResourceIndex = loaded.LinkedTextureResourceIndex, BodyTextureSlots = loaded.BodyTextureSlots, LinkedTextureSlots = loaded.LinkedTextureSlots, TextureAllocationMask = loaded.TextureAllocationMask };
        NativeGraphics = NativeGraphics with { Models = NativeGraphics.Models.SetItem(0, source) };
        nativeCrtRandom = randomState;
        NativeEffectMemory = NativeEffectMemory! with { Textures = textures };
        InvokeEffectPresentation();
    }

    internal void ReloadEffectPresentationSourceResources()
    {
        if (!EffectPresentationInvoked || NativeEffectMemory!.ReadReturnedTask5044B0() != 0 || NativeGraphics!.ModuleEffects.Head is not null || NativeGraphics.Auxiliary.Head is not null || HasPendingEffectPresentationEvents)
        {
            throw new InvalidOperationException("Native presentation source reload waits for effect completion and consumed output.");
        }
        NativeGraphics = InitializeEffectPresentationSourceResources(NativeGraphics, presentationSourceResource!);
    }

    private void ValidateEffectPresentationServices()
    {
        _ = ReadEffectPresentationStageColor();
        NativeCamera = NativeCamera!;
    }

    /// <summary>The mixed host acknowledges camera requests without moving its view or running native camera tracks.</summary>
    internal static NativeBattleCameraState HoldEffectPresentationCamera(NativeBattleCameraState camera, NativeBattleCameraState host)
    {
        return host with { ControlResource = camera.ControlResource, ControlStagingSource = camera.ControlStagingSource, ControlCursor = null, ViewTranslationScratch = new(0, 0, 0) };
    }

    internal static NativeBattleCameraState BindEffectPresentationHostProjection(NativeBattleCameraState camera, NativeBattleCameraProjection projection)
    {
        return camera with { Projection = projection, FocalLength = projection.FocalLength is ushort focal ? unchecked((short)focal) : null };
    }

    internal NativeBattleStageColor ReadEffectPresentationStageColor()
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("The host has no native arena color owner.");
        return NativeBattleStageColor.ReadUniform(stage.Groups, presentationArena?.PresentationGroupMask ?? 15);
    }

    internal bool HasPendingEffectPresentationEvents => presentationContacts.Count != 0 || presentationSounds.Count != 0 || presentationSequences.Count != 0;
    internal NativeGlobalRandomState? ReadEffectPresentationGlobalRandom() => nativeGlobalRandom;
    internal int? ReadEffectPresentationMusicGain() => presentationInitialMusicGain is null ? null : (NativeGlobalSoundProcess?.MusicPlayback ?? throw new InvalidDataException("Native presentation lost its music gain owner.")).ReadGain(0);
    internal NativeCrtRandomState ReadEffectPresentationRandom() => nativeCrtRandom ?? throw new InvalidDataException("Native presentation lost its random owner.");
    internal NativeBattleGraphicsResource ReadEffectPresentationSourceResource() => NativeGraphicsPoseResource(NativeGraphics!, 0);

    internal NativeBattleEffectPresentationStep AdvanceEffectPresentation(NativeBattleGraphicsModel source, NativeCrtRandomState randomState, NativeBattleCameraProjection projection, ImmutableArray<NativeBattleForeignActor> foreignActors, NativeBattleEffectTextureState textures)
    {
        if (presentationForeignActors is null)
        {
            throw new InvalidOperationException("This session is not a presentation component.");
        }
        PumpNativeFileIo482590();
        Dictionary<int, NativeBattleForeignActor> previousForeignActors = presentationForeignActors;
        presentationForeignActors = foreignActors.ToDictionary(actor => actor.Index);
        nativeCrtRandom = randomState;
        presentationHostCamera = BindEffectPresentationHostProjection(presentationHostCamera!, projection);
        NativeCamera = NativeCamera! with { Projection = projection, ViewTranslationScratch = new(0, 0, 0) };
        NativeBattleGraphicsState graphics = NativeGraphics! with { Models = BindPresentationForeignModels(NativeGraphics!.Models.SetItem(0, source), previousForeignActors), ModuleDraws = presentationFirstAdvance ? NativeGraphics.ModuleDraws : [], PrimitiveFrame = presentationFirstAdvance ? NativeGraphics.PrimitiveFrame : NativeBattlePrimitiveFrame.Clear() };
        NativeEffectMemory = NativeEffectMemory! with { PrimitiveProcess = presentationFirstAdvance ? NativeEffectMemory.PrimitiveProcess : NativeEffectMemory.PrimitiveProcess!.Reset500900(graphics.Flags), Textures = textures };
        presentationFirstAdvance = false;
        NativeGraphics = graphics;
        graphics = AdvanceNativeAuxiliary(graphics);
        NativeGraphics = graphics;
        graphics = AdvanceNativeModuleEffects(graphics);
        NativeGraphics = graphics;
        if (EffectPresentationInvoked)
        {
            graphics = AdvanceNativeOriginalEffect(graphics);
        }
        NativeGraphics = graphics;
        graphics = ConsumeNativeGraphicsContacts(graphics);

        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeEffectMemory = memory with { Textures = memory.Textures!.Drain505D20(memory.Arena, NativeFileStaging, address => NativeBattleProcessTim.Resolve(memory, address), memory.ScriptedEffects).State };
        NativeEffectMemory = NativeEffectMemory.PublishSound();
        ValidateEffectPresentationServices();
        NativeGraphics = graphics with { Callback = checked(graphics.Callback + 1) };
        NativeBattleEffectPresentationStep result = new(presentationContacts.ToImmutableArray(), presentationSounds.ToImmutableArray()) { Sequences = presentationSequences.ToImmutableArray() };
        presentationContacts.Clear();
        presentationSounds.Clear();
        presentationSequences.Clear();
        return result;
    }

    private ImmutableArray<NativeBattleGraphicsModel> BindPresentationForeignModels(ImmutableArray<NativeBattleGraphicsModel> models, Dictionary<int, NativeBattleForeignActor>? previous = null)
    {
        foreach (NativeBattleForeignActor actor in presentationForeignActors!.Values)
        {
            if (actor.Index is < 1 or >= 7)
            {
                throw new InvalidDataException("Foreign presentation actors require a unique physical model slot outside source slot zero.");
            }
            if ((actor.NativeModel is null) != (actor.NativeResource is null))
            {
                throw new InvalidDataException("A native foreign participant requires both its actual model state and resource owner.");
            }
            NativeBattleGraphicsModel model = previous is null && actor.NativeModel is { } native ? native with { Index = actor.Index, ActiveResourceRow100 = null, OriginalResourceRow104 = null, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, TextureResourceIndex = null, LinkedTextureResourceIndex = null } : models[actor.Index];
            NativeBattleModelTransform transform = actor.Transform;
            short rotationX = 0;
            short rotationY = actor.Facing;
            short rotationZ = 0;
            if (previous?.TryGetValue(actor.Index, out NativeBattleForeignActor? old) == true && model.Transform is { } current)
            {
                NativeBattleVector position = new(unchecked((short)(transform.Position28.X + current.Position28.X - old.Transform.Position28.X)), unchecked((short)(transform.Position28.Y + current.Position28.Y - old.Transform.Position28.Y)), unchecked((short)(transform.Position28.Z + current.Position28.Z - old.Transform.Position28.Z)));
                transform = transform with { Position28 = position, Vertical34 = unchecked((short)(transform.Vertical34 + current.Vertical34 - old.Transform.Vertical34)), Scale148 = current.Scale148, Scale154 = current.Scale154 };
                rotationX = model.Rotation12 ?? 0;
                rotationY = unchecked((short)(actor.Facing + (model.Rotation14 ?? old.Facing) - old.Facing));
                rotationZ = model.Rotation16 ?? 0;
            }
            uint normal = definition.NativeGraphics?.Rules.SummonColors?.Normal ?? throw new NotSupportedException("Foreign model presentation requires its selected normal color.");
            NativeBattleGraphicsModel bound = model with { Flags0 = (ushort)(model.Flags0 | 2), Flags2 = model.Flags2 ?? 0, Flags8 = actor.NativeModel?.Flags8 ?? actor.StatusFlags, Transform = transform, Rotation12 = rotationX, Rotation14 = rotationY, Rotation16 = rotationZ, GeometryBounds52 = actor.Geometry?.Anchors.LocalBounds, Color40 = model.Color40 ?? 0x808080, Color44 = model.Color44 ?? (normal & 0xFFFFFFu) | 0x32000000u, HasLinkedModelRing140 = false, LinkedModelIndex140 = null };
            models = models.SetItem(actor.Index, bound with { GeometryWorldMatrix = actor.Geometry is { } geometry ? NativeBattleGfMatrices.Compose56C2F0(NativeBattleForeignPlacement.Delta(actor, bound), geometry.World) : null });
        }
        return models;
    }
}
