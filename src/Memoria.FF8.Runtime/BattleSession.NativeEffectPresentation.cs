using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private Dictionary<int, NativeBattleForeignActor>? presentationForeignActors;
    private NativeBattleGraphicsResource? presentationSourceResource;
    private bool presentationFirstAdvance = true;
    private NativeBattleCameraState? presentationHostCamera;
    private readonly List<NativeBattleContactPacket> presentationContacts = [];
    private readonly List<NativeBattleModuleSound> presentationSounds = [];

    /// <summary>The host retains its camera projection. Native control resources may register without acquiring a camera lease; their control programs are not executed here.</summary>
    private BattleSession(NativeBattleEffectPresentationInputs inputs)
    {
        definition = inputs.Resources;
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
        NativeCamera = NativeBattleCameraState.FromColdProcess() with { Projection = inputs.Projection };
        presentationHostCamera = NativeCamera;
        NativeFileStaging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.FromColdProcess());
        NativeFileIo = NativeBattleFileIoState.FromColdProcess();
        NativeVibrationProcess = definition.NativeVibrationProcess ?? definition.NativeRetainedMemory?.VibrationProcess ?? NativeVibrationProcessState.FromColdProcess();
        if (inputs.SourceResource.ModelLoadSources is { IsEmpty: false } modelSources)
        {
            ushort maximum = 0;
            foreach (NativeBattleModelDatSource model in modelSources)
            {
                maximum = NativeBattleModelResourceCopies.UpdateScratchMaximum(maximum, NativeBattleModelResourceCopies.ReadScratchCandidate(model));
            }
            NativeModelCpuResources = NativeBattleModelCpuResources.FromColdProcess() with { ScratchMaximum = maximum };
        }
        NativeStage = NativeBattleStageState.FromColdProcess();
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray().SetItem(0, inputs.Source);
        models = BindPresentationForeignModels(models);
        NativeGraphics = InitializeNativePrimitiveFrame500400(new(models, Enumerable.Range(0, models.Length).Select(index => index == 0 ? "source" : presentationForeignActors.ContainsKey(index) ? $"host-{index}" : null).ToImmutableArray(), 0, 0));
        InitializeNativePrimitiveDepth47CE10();
        NativeGraphics = BeginNativeOriginalEffect(NativeGraphics!, inputs.Effect);
        NativeGraphics = NativeGraphics with { OriginalEffect = NativeGraphics.OriginalEffect! with { Parameters = inputs.Parameters } };
        NativeGraphics = InvokeNativeOriginalEffect(NativeGraphics);
        ValidateEffectPresentationServices();
    }

    internal static BattleSession CreateEffectPresentation(NativeBattleEffectPresentationInputs inputs) => new(inputs);

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

    internal NativeBattleStageColor ReadEffectPresentationStageColor()
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("The host has no native arena color owner.");
        return NativeBattleStageColor.ReadUniform(stage.Groups);
    }

    internal bool HasPendingEffectPresentationEvents => presentationContacts.Count != 0 || presentationSounds.Count != 0;
    internal NativeGlobalRandomState? ReadEffectPresentationGlobalRandom() => nativeGlobalRandom;
    internal NativeCrtRandomState ReadEffectPresentationRandom() => nativeCrtRandom ?? throw new InvalidDataException("Native presentation lost its random owner.");

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
        NativeCamera = NativeCamera! with { Projection = projection, ViewTranslationScratch = new(0, 0, 0) };
        presentationHostCamera = presentationHostCamera! with { Projection = projection };
        NativeBattleGraphicsState graphics = NativeGraphics! with { Models = BindPresentationForeignModels(NativeGraphics!.Models.SetItem(0, source), previousForeignActors), ModuleDraws = presentationFirstAdvance ? NativeGraphics.ModuleDraws : [], PrimitiveFrame = presentationFirstAdvance ? NativeGraphics.PrimitiveFrame : NativeBattlePrimitiveFrame.Clear() };
        NativeEffectMemory = NativeEffectMemory! with { PrimitiveProcess = presentationFirstAdvance ? NativeEffectMemory.PrimitiveProcess : NativeEffectMemory.PrimitiveProcess!.Reset500900(graphics.Flags), Textures = textures };
        presentationFirstAdvance = false;
        NativeGraphics = graphics;
        graphics = AdvanceNativeAuxiliary(graphics);
        NativeGraphics = graphics;
        graphics = AdvanceNativeModuleEffects(graphics);
        NativeGraphics = graphics;
        graphics = AdvanceNativeOriginalEffect(graphics);
        NativeGraphics = graphics;
        graphics = ConsumeNativeGraphicsContacts(graphics);

        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeEffectMemory = memory with { Textures = memory.Textures!.Drain505D20(memory.Arena, NativeFileStaging, address => NativeBattleProcessTim.Resolve(memory, address), memory.ScriptedEffects).State };
        NativeEffectMemory = NativeEffectMemory.PublishSound();
        ValidateEffectPresentationServices();
        NativeGraphics = graphics with { Callback = checked(graphics.Callback + 1) };
        NativeBattleEffectPresentationStep result = new(presentationContacts.ToImmutableArray(), presentationSounds.ToImmutableArray());
        presentationContacts.Clear();
        presentationSounds.Clear();
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
            NativeBattleGraphicsModel model = models[actor.Index];
            NativeBattleModelTransform transform = actor.Transform;
            short rotationX = 0;
            short rotationY = actor.Facing;
            short rotationZ = 0;
            if (previous?.TryGetValue(actor.Index, out NativeBattleForeignActor? old) == true && model.Transform is { } current)
            {
                NativeBattleVector position = new(unchecked((short)(transform.Position28.X + current.Position28.X - old.Transform.Position28.X)), unchecked((short)(transform.Position28.Y + current.Position28.Y - old.Transform.Position28.Y)), unchecked((short)(transform.Position28.Z + current.Position28.Z - old.Transform.Position28.Z)));
                transform = transform with { Position28 = position, Vertical34 = unchecked((short)(transform.Vertical34 + current.Vertical34 - old.Transform.Vertical34)) };
                rotationX = model.Rotation12 ?? 0;
                rotationY = unchecked((short)(actor.Facing + (model.Rotation14 ?? old.Facing) - old.Facing));
                rotationZ = model.Rotation16 ?? 0;
            }
            uint normal = definition.NativeGraphics?.Rules.SummonColors?.Normal ?? throw new NotSupportedException("Foreign model presentation requires its selected normal color.");
            NativeBattleGraphicsModel bound = model with { Flags0 = (ushort)(model.Flags0 | 2), Flags2 = model.Flags2 ?? 0, Transform = transform, Rotation12 = rotationX, Rotation14 = rotationY, Rotation16 = rotationZ, GeometryBounds52 = actor.Geometry?.Anchors.LocalBounds, Color40 = model.Color40 ?? 0x808080, Color44 = model.Color44 ?? (normal & 0xFFFFFFu) | 0x32000000u, HasLinkedModelRing140 = false, LinkedModelIndex140 = null };
            models = models.SetItem(actor.Index, bound with { GeometryWorldMatrix = actor.Geometry is { } geometry ? NativeBattleGfMatrices.Compose56C2F0(NativeBattleForeignPlacement.Delta(actor, bound), geometry.World) : null });
        }
        return models;
    }
}
