namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly List<NativeBattleEffectActivation> presentationActivations = [];
    private readonly HashSet<BattleModelOwner> presentationActivationSequences = [];

    internal bool HasPendingEffectPresentationActivations => presentationNativeTargets is not null &&
        NativeGraphics is { } graphics && (graphics.Descriptors.Root is not null || graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Callback502F30));

    private NativeBattleGraphicsState InitializeEffectPresentationEnemyResources(NativeBattleGraphicsState graphics)
    {
        NativeModelCpuResources = NativeBattleModelCpuResources.FromColdProcess() with { EnemyCursor = 0, ScratchMaximum = 0, EnemyLinkedSlots = 0 };
        NativeModelTextureCache = NativeBattleModelTextureCache.Initialize508260(NativeBattleModelTextureCache.FromColdProcess());
        NativeEffectIoResult = 0;
        LoadNativeStage500EA0();
        foreach (BattleModelEntry<BattleModelOwner> entry in graphics.ActorModels.Entries)
        {
            if (entry.State.ModelId4 is not byte modelId || modelId < 16 || (entry.State.Flags0 & 2) == 0)
            {
                continue;
            }
            QueueNativeModelResource507080(graphics, modelId, entry.Owner);
            while (NativeModelLoader!.Task is not null)
            {
                PumpNativeFileIo482590();
                PollNativeModelResource507070(ref graphics);
            }
            NativeBattleGraphicsModel loaded = graphics.ActorModels.Get(entry.Owner);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(entry.Owner, loaded with { Alpha7 = 0 }) };
        }
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeEffectMemory = DrainNativeTextureUploads(memory, memory.Textures!);
        return graphics;
    }

    private NativeBattleGraphicsState QueueEffectPresentationActivation(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        if (presentationNativeTargets is not null && (!presentationNativeTargets.TryGetValue(owner, out NativeBattleEffectModelBinding? target) || !target.CanActivate))
        {
            throw new NotSupportedException("Native activation requires its currently allocated host participant.");
        }
        return QueueNativeModelDescriptor(graphics, 113, 0x487670, owner);
    }

    private NativeBattleGraphicsState CompleteEffectPresentationActivation(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        if (!presentationNativeTargets!.TryGetValue(owner, out NativeBattleEffectModelBinding? target) || !target.CanActivate || !presentationActivationSequences.Contains(owner))
        {
            throw new InvalidDataException("Native activation lost its bound actor or model sequence.");
        }
        presentationActivations.Add(new(target.ActorId, owner));
        return graphics;
    }

    private NativeBattleGraphicsState AdvanceEffectPresentationActorSequences(NativeBattleGraphicsState graphics)
    {
        foreach (BattleModelOwner owner in presentationActivationSequences)
        {
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, owner);
            NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Hosted appearance requires its original color interpolation.");
            NativeBattleStageColors stage = NativeStage?.ModelColors ?? throw new NotSupportedException("Hosted appearance requires its loaded stage colors.");
            model = NativeBattleGraphicsModels.AdvanceAppearance(model, (graphics.Flags & 1) != 0, colors with { Transition = stage.Color40, Normal = stage.Color44 });
            NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(NativeEntryAdvantage, NativeCamera?.Mask)
            {
                OriginalResource = resource,
                ReadModelExternal = (current, variable) => ReadNativeGraphicsSequenceVariable(graphics with { ActorModels = graphics.ActorModels.Set(owner, current) }, owner, variable),
                WriteExternal = WriteNativeGraphicsSequenceVariable,
                PlaySound = (current, cue) => PublishNativeSequenceSound(graphics, owner, current, presentationNativeTargets![owner].ActorId, cue),
                ApplyActionContacts = current => ApplyNativeActiveHeaderContacts(graphics with { ActorModels = graphics.ActorModels.Set(owner, current) }).ActorModels.Get(owner)
            });
            if (step.Cues.FirstOrDefault(cue => cue.Kind is not (NativeBattleSequenceCueKind.ActorSound or NativeBattleSequenceCueKind.SystemSound or NativeBattleSequenceCueKind.RestoreStanding)) is { } unsupported)
            {
                throw new NotSupportedException($"Hosted activation actor {owner}, sequence {model.Sequence} requires the {unsupported.Kind} cue ({unsupported.First}, {unsupported.Second}).");
            }
            NativeBattleGraphicsModel posed = step.Model;
            if (resource.PoseSource is { } pose)
            {
                posed = posed with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(posed, pose) };
            }
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, posed) };
        }
        return graphics;
    }
}
