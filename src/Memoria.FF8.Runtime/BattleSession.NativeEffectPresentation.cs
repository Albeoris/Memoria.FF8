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
        NativeEffectMemory = NativeBattleEffectMemory.FromColdProcess() with { Textures = inputs.Textures };
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
        _ = ReadEffectPresentationStageDepth();
        NativeCamera = NativeCamera!;
    }

    /// <summary>The mixed host acknowledges camera requests without moving its view or running native camera tracks.</summary>
    internal static NativeBattleCameraState HoldEffectPresentationCamera(NativeBattleCameraState camera, NativeBattleCameraState host)
    {
        return host with { ControlResource = camera.ControlResource, ControlStagingSource = camera.ControlStagingSource, ControlCursor = null, ViewTranslationScratch = new(0, 0, 0) };
    }

    internal short ReadEffectPresentationStageDepth()
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("The host has no native stage color owner.");
        if (stage.Groups.Length != 4 || stage.Groups.Any(group => group.ColorDepth != stage.Groups[0].ColorDepth || group.ColorFlags != 0))
        {
            throw new NotSupportedException("The host can represent only uniform native stage color depth with unflagged colors across all four stage groups.");
        }
        return stage.Groups[0].ColorDepth;
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
        presentationForeignActors = foreignActors.ToDictionary(actor => actor.Index);
        nativeCrtRandom = randomState;
        NativeCamera = NativeCamera! with { Projection = projection, ViewTranslationScratch = new(0, 0, 0) };
        presentationHostCamera = presentationHostCamera! with { Projection = projection };
        NativeBattleGraphicsState graphics = NativeGraphics! with { Models = BindPresentationForeignModels(NativeGraphics!.Models.SetItem(0, source)), ModuleDraws = presentationFirstAdvance ? NativeGraphics.ModuleDraws : [], PrimitiveFrame = presentationFirstAdvance ? NativeGraphics.PrimitiveFrame : NativeBattlePrimitiveFrame.Clear() };
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

        NativeEffectMemory = NativeEffectMemory! with { Textures = NativeEffectMemory.Textures!.Drain505D20(NativeEffectMemory.Arena).State };
        NativeEffectMemory = NativeEffectMemory.PublishSound();
        ValidateEffectPresentationServices();
        NativeGraphics = graphics with { Callback = checked(graphics.Callback + 1) };
        NativeBattleEffectPresentationStep result = new(presentationContacts.ToImmutableArray(), presentationSounds.ToImmutableArray());
        presentationContacts.Clear();
        presentationSounds.Clear();
        return result;
    }

    private ImmutableArray<NativeBattleGraphicsModel> BindPresentationForeignModels(ImmutableArray<NativeBattleGraphicsModel> models)
    {
        foreach (NativeBattleForeignActor actor in presentationForeignActors!.Values)
        {
            if (actor.Index is < 1 or >= 7)
            {
                throw new InvalidDataException("Foreign presentation actors require a unique physical model slot outside source slot zero.");
            }
            NativeBattleGraphicsModel model = models[actor.Index];
            uint normal = definition.NativeGraphics?.Rules.SummonColors?.Normal ?? throw new NotSupportedException("Foreign model presentation requires its selected normal color.");
            models = models.SetItem(actor.Index, model with { Flags0 = (ushort)(model.Flags0 | 2), Flags2 = model.Flags2 ?? 0, Transform = actor.Transform, Rotation14 = actor.Facing, GeometryWorldMatrix = actor.Geometry?.World, GeometryBounds52 = actor.Geometry?.Anchors.LocalBounds, Color40 = model.Color40 ?? 0x808080, Color44 = model.Color44 ?? (normal & 0xFFFFFFu) | 0x32000000u, HasLinkedModelRing140 = false, LinkedModelIndex140 = null });
        }
        return models;
    }
}
