using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeLoadedModels(NativeBattleGraphicsState graphics, uint flags, NativeBattleModelLifecycleServices? services = null)
    {
        BattleModelRoster? roster = graphics.PresentationRoster;
        if (roster is not null)
        {
            (services ?? throw new InvalidDataException("Hosted model lifecycle requires its actor fact and receipt services.")).Validate(roster, graphics);
        }
        else if (services is not null)
        {
            throw new InvalidDataException("Hosted model lifecycle services require an explicit graphics roster.");
        }
        ImmutableArray<NativeBattleGraphicsModel> models = graphics.Models;
        for (int index = 0; index < (roster?.ActorCount ?? NativeBattleSlots.Count); index++)
        {
            NativeBattleModelActorFacts? actorFacts = services?.Facts[index];
            if (services is not null && actorFacts is null)
            {
                continue;
            }
            NativeBattleGraphicsModel model = models[index];
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            model = NativeBattleGraphicsModels.PublishResourceOnlyReadiness502AB0(model);
            ushort modelFlags = model.Flags2 ?? throw new InvalidDataException("Native loaded graphics model lacks Flags2.");
            if ((modelFlags & 4) == 0)
            {
                NativeBattleEffectMemory colorMemory = NativeEffectMemory ?? throw new NotSupportedException("Native50A0A0 requires the retained process model-color owner.");
                NativeBattleSummonColors colorRules = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native50A0A0 requires its authenticated interpolation constants.");
                NativeBattleStageColors modelColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Native50A0A0 requires the actual stage color DWORDs.");
                NativeBattleModelColorStep colorStep = NativeBattleModelColors.Advance50A0A0(model, flags, colorRules with { Transition = modelColors.Color40, Normal = modelColors.Color44 }, colorMemory.ModelColorOverrides);
                model = colorStep.Model;
                NativeEffectMemory = colorMemory with { ModelColorOverrides = colorStep.Overrides };
                if (colorStep.CountdownRequired)
                {
                    AdvanceNativeStatusCountdowns(graphics, model, actorFacts);
                }
                (graphics, model) = AdvanceNativeStatusIcons(graphics, model);
                byte? dispatchedKind = model.Kind5;
                NativeBattleSummonColors lifecycleColors = colorRules with { Transition = modelColors.Color40, Normal = modelColors.Color44 };
                model = NativeBattleGraphicsModels.AdvanceCover(model);
                model = NativeBattleGraphicsModels.AdvanceAppearance(model, (flags & 1) != 0, lifecycleColors);
                if (model.Kind5 == 3)
                {
                    NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native50C410 requires its authenticated interpolation constants.");
                    NativeBattleStageColors stageColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Native50C410 requires its retained stage color DWORDs.");
                    model = NativeBattleModelFade.Advance50C410(model, (flags & 1) != 0, colors with { Transition = stageColors.Color40, Normal = stageColors.Color44 });
                }
                if (model.Kind5 == 4)
                {
                    model = NativeBattleModelFade.Advance50C560(model, (flags & 1) != 0, lifecycleColors);
                }
                if (model.Kind5 == 8)
                {
                    model = NativeBattleModelFade.AdvanceFlash50C670(model, lifecycleColors, NextNativeGlobalRandom);
                }
                if (model.Kind5 is 10 or 11 or 12 or 13)
                {
                    NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native summon model service requires its authenticated color constants.");
                    NativeBattleStageColors stageColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Native50C4D0/50C330 requires its retained stage color DWORD producers.");
                    colors = colors with { Transition = stageColors.Color40, Normal = stageColors.Color44 };
                    model = NativeBattleSummonModels.Advance(model, (flags & 1) != 0, colors);
                }
                byte camera = dispatchedKind == 5 && model.Phase6 > 0 ? NativeCameraTaskMask : (byte)0;
                NativeBattleGraphicsModel beforeRemoval = model;
                bool soundBusy = dispatchedKind == 5 && model.Phase6 > 0 && NativeOriginalSoundBusy();
                NativeBattleGraphicsRemoval removal = dispatchedKind is 5 or 6 or 7 ? NativeBattleGraphicsModels.AdvanceRemoval(model, (flags & 1) != 0, soundBusy, camera, lifecycleColors) : new(model, false);
                model = removal.Model;
                if ((model.Flags0 & 2) == 0 && beforeRemoval.ModelId4 != 143)
                {
                    NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native5080D0 requires its original resource-row texture owner.");
                    int releasedRow = beforeRemoval.TextureResourceIndex ?? throw new NotSupportedException("Native5080D0 requires the original model DWORD100 resource-row binding.");
                    if (cache.Rows[releasedRow].Kind == 3)
                    {
                        (NativeModelCpuResources, NativeFileStaging) = NativeBattleModelEnemyHeap.Release(NativeModelCpuResources ?? throw new NotSupportedException("Native5080D0 requires its actual retained heap rows."), NativeFileStaging ?? throw new NotSupportedException("Native5080D0 requires its actual shared heap bytes."), cache, releasedRow);
                    }
                    NativeModelTextureCache = cache.Release5080D0(releasedRow);
                }
                if (removal.ReleaseParticipant)
                {
                    if (actorFacts is null)
                    {
                        BattleCombatant actor = GraphicsActor(graphics, index);
                        actor.NativeCombatantFlags = (actor.NativeCombatantFlags | 0x40) & ~1u;
                        actor.NativeStatus0 |= 1;
                    }
                    else
                    {
                        services!.Emit(new NativeBattleModelParticipantReleased(index, actorFacts.ActorId));
                    }
                }
            }
            bool encounterArrivalActive = ApplyEncounterArrivalBeforeDraw(graphics with { Models = models.SetItem(index, model) }, ref model);
            if (definition.NativeGraphics?.PrimitiveTables is not null && (model.Flags0 & 2) != 0)
            {
                NativeBattleGraphicsResource drawResource = NativeGraphicsPoseResource(graphics with { Models = models.SetItem(index, model) }, index);
                if ((model.Flags0 & 6) == 2 && (model.Flags2!.Value & 0x84) == 0)
                {
                    model = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model, drawResource.PoseSource ?? throw new NotSupportedException("Native5086F0 requires its actual posed body skeleton."));
                }
                graphics = SubmitNativeModelPackets502D40(graphics with { Models = models.SetItem(index, model) }, model, drawResource);
                models = graphics.Models;
                model = models[index];
            }
            if (!encounterArrivalActive && (model.Flags0 & 2) != 0 && (model.Flags0 & 8) == 0 && !((model.Flags8!.Value & 0x18) != 0 && (model.ResourceFlags44!.Value & 0x40) != 0) && (flags & 0x201) == 0)
            {
                NativeBattleGraphicsResource originalResource = actorFacts is null
                    ? (FindNativeGraphicsBinding(graphics.ActorIds[index]!) ?? throw new InvalidDataException("Native graphics callback lost its immutable resource binding.")).Resource
                    : ReadHostedPresentationModelResource(index) ?? throw new InvalidDataException("Hosted graphics callback lost its immutable resource binding.");
                NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
                NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(NativeEntryAdvantage, NativeCamera?.Mask)
                {
                    OriginalResource = model.ModelId4 >= 16 ? model.OriginalResourceRow104 == model.ActiveResourceRow100 && model.ResourceMagicId is null ? resource : ResolveNativeEnemyResource(model.OriginalResourceRow104 ?? throw new NotSupportedException("Native enemy original resource row remains unknown."), originalResource) : originalResource,
                    GlobalRandom = NextNativeGlobalRandom,
                    BindModelRing90 = (current, selector) =>
                    {
                        models = NativeBattleModelRings.Bind90(models.SetItem(index, current), index, selector);
                        graphics = graphics with { Models = models };
                        return models[index];
                    },
                    ReadModelExternal = (current, variable) => ReadNativeGraphicsSequenceVariable(graphics with { Models = models.SetItem(index, current) }, index, variable),
                    WriteExternal = WriteNativeGraphicsSequenceVariable,
                    ScheduleContactEffect = (current, program) =>
                    {
                        graphics = graphics with { Models = models.SetItem(index, current) };
                        (graphics, int offset) = ScheduleNativeSequenceParticle(graphics, index, program, true);
                        models = graphics.Models;
                        return (models[index], offset);
                    },
                    ScheduleModelParticle = (current, program) =>
                    {
                        graphics = graphics with { Models = models.SetItem(index, current) };
                        (graphics, int offset) = ScheduleNativeSequenceParticle(graphics, index, program, false);
                        models = graphics.Models;
                        return (models[index], offset);
                    },
                    WriteModelExternal = (current, variable, value) =>
                    {
                        int target = (graphics.ActionContext ?? throw new NotSupportedException("Native sequence target write requires its50D300 target identity.")).TargetIndex ?? throw new NotSupportedException("Native sequence target write requires its physical target identity.");
                        models = models.SetItem(index, current);
                        models = models.SetItem(target, NativeBattleActionGraphics.WriteSequenceTarget5048E0(models[target], variable, value));
                        graphics = graphics with { Models = models };
                        return models[index];
                    },
                    ScheduleSoundParticle = (program, soundTwo, bone) => graphics = ScheduleNativeSoundParticle(graphics, index, program, soundTwo, bone),
                    ScheduleCameraShake = (start, end, duration) => graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendCameraShake50F680(start, end, duration) },
                    ScheduleModelGroupFade = group => graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendModelGroupFade50F0B0(index, group, graphics.PresentationRoster) },
                    ScheduleModelDeformation = (current, group, velocity, angle) =>
                    {
                        graphics = ScheduleNativeModelDeformation50F250(graphics with { Models = models.SetItem(index, current) }, index, group, velocity, angle);
                        models = graphics.Models;
                        return models[index];
                    },
                    ScheduleStageFade = duration =>
                    {
                        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native501F50 requires its four retained stage rows.");
                        NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorFlags = group.ColorFlags & 0xFF000000u }).ToImmutableArray() };
                        graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendStageFade501F50(duration) };
                    },
                    StageControl = command =>
                    {
                        graphics = DispatchNativeStageControl(graphics with { Models = models }, command);
                        models = graphics.Models;
                    },
                    PlaySound = (current, cue) =>
                    {
                        NativeBattleGraphicsState sounding = graphics with { Models = models.SetItem(index, current) };
                        if (actorFacts is null)
                        {
                            PublishNativeSequenceSound(sounding, current, GraphicsActor(graphics, index), cue);
                        }
                        else
                        {
                            PublishNativeSequenceSound(sounding, current, actorFacts.ActorId, cue);
                        }
                    },
                    ScheduleTextureAnimation = (program, sequence) => graphics = ScheduleNativeTextureAnimation(graphics, index, program, sequence),
                    ScheduleModelEffect = effect => graphics = ScheduleNativeModelEffect(graphics, index, effect),
                    ApplyTexture = (selector, variant) => ApplyNativeTextureMotion(graphics, index, selector, variant),
                    AdvanceTexture = AdvanceNativeTextureMotion,
                    RebindActionGeometry = (current, selector) =>
                    {
                        graphics = RebindNativeSequenceGeometry92(graphics with { Models = models.SetItem(index, current) }, index, selector);
                        models = graphics.Models;
                        return models[index];
                    },
                    ScheduleModelMotion = (current, opcode, first, second, fraction, duration) =>
                    {
                        graphics = graphics with { Models = models.SetItem(index, current) };
                        int target = opcode == 0x9E ? first : (graphics.ActionContext?.TargetIndex ?? throw new NotSupportedException("Native AD/AE requires its retained50D300 target identity."));
                        (NativeBattleAuxiliaryState auxiliary, bool allocated) = graphics.Auxiliary.AppendModelMotion(opcode, index, target, first, second, fraction, duration, graphics.PresentationRoster);
                        graphics = graphics with { Auxiliary = auxiliary };
                        if (allocated && opcode != 0x9E)
                        {
                            NativeBattleGraphicsModel targetModel = graphics.Models[target];
                            ushort targetFlags = targetModel.Flags2 ?? throw new NotSupportedException("Native AD/AE requires its target flagsWORD2.");
                            graphics = graphics with { Models = graphics.Models.SetItem(target, targetModel with { Flags2 = (ushort)(targetFlags | 0x20) }) };
                        }
                        models = graphics.Models;
                        return models[index];
                    },
                    ApplyCurrentContact = current =>
                    {
                        graphics = ApplyNativeCurrentSequenceContact(graphics with { Models = models.SetItem(index, current) });
                        models = graphics.Models;
                        return models[index];
                    },
                    SelectModelSequence = (current, opcode, selector, selected) =>
                    {
                        graphics = SelectNativeSequenceModels(graphics with { Models = models.SetItem(index, current) }, index, opcode, selector, selected);
                        models = graphics.Models;
                        return models[index];
                    },
                    SchedulePendingSequence = (current, selected) =>
                    {
                        graphics = ScheduleNativeRingSequence505C00(graphics with { Models = models.SetItem(index, current) }, checked((byte)index), selected);
                        models = graphics.Models;
                        return models[index];
                    },
                    ApplyNextContact = current =>
                    {
                        graphics = ApplyNativeNextSequenceContact(graphics with { Models = models.SetItem(index, current) });
                        models = graphics.Models;
                        return models[index];
                    },
                    ApplyActionContacts = current =>
                    {
                        models = models.SetItem(index, current);
                        graphics = ApplyNativeActiveHeaderContacts(graphics with { Models = models });
                        models = graphics.Models;
                        //TODO(TD-009): the component presentation phase observes the original AA publication while104 owns native completion.
                        graphics = graphics with { ActionCallbackReady = true };
                        return models[index];
                    }
                });
                model = step.Model with { Flags2 = (ushort)(step.Model.Flags2!.Value & ~4) };
                foreach (NativeBattleSequenceCue cue in step.Cues)
                {
                    if (actorFacts is not null)
                    {
                        graphics = PublishHostedModelLifecycleCue(graphics, model, actorFacts, cue, services!);
                        continue;
                    }
                    BattleCombatant actor = GraphicsActor(graphics, index);
                    if (cue.Kind == NativeBattleSequenceCueKind.Text)
                    {
                        string text = (FindNativeOriginalGraphicsBinding(actor.Id) ?? throw new InvalidDataException("Native91 lost its original actor resource binding.")).Resource.ReadText91(cue.First);
                        events.Add(new BattleEvent(Frame, "NativeText", actor.Id, null, cue.Second, text) { NativeMessageMode = 3, NativeTextIdentity = actor.IsPlayer ? null : BattleTextIdentity.Enemy(actor.ModelId, cue.First) });
                    }
                    else if (cue.Kind == NativeBattleSequenceCueKind.CommandTitle && graphics.ActionGraphics is { } titleContext)
                    {
                        graphics = PublishNativeOriginalEffectTitle(graphics, titleContext);
                    }
                    else if (cue.Kind == NativeBattleSequenceCueKind.SuspendParticipant)
                    {
                        actor.NativeCombatantFlags |= 0x40;
                        actor.NativeStatus0 |= 1;
                        ObserveEncounterNativeTerminal(actor, checked((byte)cue.First), graphics);
                    }
                    else if (cue.Kind == NativeBattleSequenceCueKind.ActionCallback)
                    {
                        throw new InvalidDataException("Native AA must publish its active header contacts inline.");
                    }
                    else if (cue.Kind == NativeBattleSequenceCueKind.TriggerWindow)
                    {
                        NativeGraphics = graphics with { Models = models.SetItem(index, model) };
                        RegisterNativeTriggerWidget(NativeGraphics, actor, cue);
                        graphics = NativeGraphics;
                    }
                }
                graphics = PublishNativeTriggerSound(graphics with { Models = models.SetItem(index, model) });
            }
            else if ((model.Flags0 & 2) != 0 && model.ModelId4 != 0x8F)
            {
                NativeBattleGraphicsResource refreshResource = NativeGraphicsPoseResource(graphics with { Models = models.SetItem(index, model) }, index);
                model = refreshResource.PoseSource is { } bodySource
                    ? NativeBattleBodyBoneMatrices.Publish508F60(model, bodySource, refreshResource.LinkedPoseSource)
                    : model with { CurrentBodyBoneMatrices = null };
            }
            if (definition.NativeGraphics?.PrimitiveTables is not null && (model.Flags0 & 2) != 0)
            {
                NativeBattleGraphicsResource matrixResource = NativeGraphicsPoseResource(graphics with { Models = models.SetItem(index, model) }, index);
                model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, matrixResource.PoseSource ?? throw new NotSupportedException("Native502AB0 requires its actual body skeleton source."), ReadNativeModelScaleVector502AB0(graphics, model)) };
                model = NativeBattleModelRings.BindWorldMatrix502AB0(models, model);
            }
            models = models.SetItem(index, model);
        }
        return graphics with { Models = models };
    }
}
