using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeLoadedModels(NativeBattleGraphicsState graphics, uint flags, NativeBattleModelLifecycleServices? services = null)
    {
        services?.Validate(nativeActionPackets.Roster, graphics);
        BattleActorModels models = graphics.ActorModels;
        foreach (BattleModelOwner owner in models.Owners)
        {
            NativeBattleModelActorFacts? actorFacts = services?.Facts[owner];
            NativeBattleGraphicsModel model = models.Get(owner);
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
                NativeBattleModelColorStep colorStep = NativeBattleModelColors.Advance50A0A0(owner, model, flags, colorRules with { Transition = modelColors.Color40, Normal = modelColors.Color44 }, colorMemory.ModelColorOverrides, models, graphics.NativeBindings);
                model = colorStep.Model;
                NativeEffectMemory = colorMemory with { ModelColorOverrides = colorStep.Overrides };
                if (colorStep.CountdownRequired)
                {
                    AdvanceNativeStatusCountdowns(graphics, owner, model, actorFacts);
                }
                (graphics, model) = AdvanceNativeStatusIcons(graphics, owner, model);
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
                        BattleCombatant actor = GraphicsActor(graphics, owner);
                        actor.NativeCombatantFlags = (actor.NativeCombatantFlags | 0x40) & ~1u;
                        actor.NativeStatus0 |= 1;
                    }
                    else
                    {
                        services!.Emit(new NativeBattleModelParticipantReleased(owner, actorFacts.ActorId));
                    }
                }
            }
            bool encounterArrivalActive = ApplyEncounterArrivalBeforeDraw(graphics with { ActorModels = models.Set(owner, model) }, owner, ref model);
            if (definition.NativeGraphics?.PrimitiveTables is not null && (model.Flags0 & 2) != 0)
            {
                NativeBattleGraphicsResource drawResource = NativeGraphicsPoseResource(graphics with { ActorModels = models.Set(owner, model) }, owner);
                if ((model.Flags0 & 6) == 2 && (model.Flags2!.Value & 0x84) == 0)
                {
                    model = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model, drawResource.PoseSource ?? throw new NotSupportedException("Native5086F0 requires its actual posed body skeleton."));
                }
                graphics = SubmitNativeModelPackets502D40(graphics with { ActorModels = models.Set(owner, model) }, owner, model, drawResource);
                models = graphics.ActorModels;
                model = models.Get(owner);
            }
            if (!encounterArrivalActive && (model.Flags0 & 2) != 0 && (model.Flags0 & 8) == 0 && !((model.Flags8!.Value & 0x18) != 0 && (model.ResourceFlags44!.Value & 0x40) != 0) && (flags & 0x201) == 0)
            {
                NativeBattleGraphicsResource originalResource = ReadHostedPresentationModelResource(owner) ?? throw new InvalidDataException("Native graphics callback lost its immutable resource binding.");
                NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, owner);
                NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(owner, model, resource, new(NativeEntryAdvantage, NativeCamera?.Mask)
                {
                    OriginalResource = model.ModelId4 >= 16 ? model.OriginalResourceRow104 == model.ActiveResourceRow100 && model.ResourceMagicId is null ? resource : ResolveNativeEnemyResource(model.OriginalResourceRow104 ?? throw new NotSupportedException("Native enemy original resource row remains unknown."), originalResource) : originalResource,
                    GlobalRandom = NextNativeGlobalRandom,
                    BindModelRing90 = (current, selector) =>
                    {
                        models = NativeBattleModelRings.Bind90(models.Set(owner, current), owner, selector, graphics.NativeBindings);
                        graphics = graphics with { ActorModels = models };
                        return models.Get(owner);
                    },
                    ReadModelExternal = (current, variable) => ReadNativeGraphicsSequenceVariable(graphics with { ActorModels = models.Set(owner, current) }, owner, variable),
                    WriteExternal = WriteNativeGraphicsSequenceVariable,
                    ScheduleContactEffect = (current, program) =>
                    {
                        graphics = graphics with { ActorModels = models.Set(owner, current) };
                        (graphics, int offset) = ScheduleNativeSequenceParticle(graphics, owner, program, true);
                        models = graphics.ActorModels;
                        return (models.Get(owner), offset);
                    },
                    ScheduleModelParticle = (current, program) =>
                    {
                        graphics = graphics with { ActorModels = models.Set(owner, current) };
                        (graphics, int offset) = ScheduleNativeSequenceParticle(graphics, owner, program, false);
                        models = graphics.ActorModels;
                        return (models.Get(owner), offset);
                    },
                    WriteModelExternal = (current, variable, value) =>
                    {
                        BattleModelOwner target = (graphics.ActionContext ?? throw new NotSupportedException("Native sequence target write requires its50D300 target identity.")).Target ?? throw new NotSupportedException("Native sequence target write requires its physical target identity.");
                        models = models.Set(owner, current);
                        models = models.Set(target, NativeBattleActionGraphics.WriteSequenceTarget5048E0(models.Get(target), variable, value));
                        graphics = graphics with { ActorModels = models };
                        return models.Get(owner);
                    },
                    ScheduleSoundParticle = (program, soundTwo, bone) => graphics = ScheduleNativeSoundParticle(graphics, owner, program, soundTwo, bone),
                    ScheduleCameraShake = (start, end, duration) => graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendCameraShake50F680(start, end, duration) },
                    ScheduleModelGroupFade = group => graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendModelGroupFade50F0B0(owner, group, graphics.NativeBindings.CaptureAliases(graphics.ActorModels)) },
                    ScheduleModelDeformation = (current, group, velocity, angle) =>
                    {
                        graphics = ScheduleNativeModelDeformation50F250(graphics with { ActorModels = models.Set(owner, current) }, owner, group, velocity, angle);
                        models = graphics.ActorModels;
                        return models.Get(owner);
                    },
                    ScheduleStageFade = duration =>
                    {
                        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native501F50 requires its four retained stage rows.");
                        NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorFlags = group.ColorFlags & 0xFF000000u }).ToImmutableArray() };
                        graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendStageFade501F50(duration) };
                    },
                    StageControl = command =>
                    {
                        graphics = DispatchNativeStageControl(graphics with { ActorModels = models }, command);
                        models = graphics.ActorModels;
                    },
                    PlaySound = (current, cue) =>
                    {
                        NativeBattleGraphicsState sounding = graphics with { ActorModels = models.Set(owner, current) };
                        if (actorFacts is null)
                        {
                            PublishNativeSequenceSound(sounding, owner, current, GraphicsActor(graphics, owner), cue);
                        }
                        else
                        {
                            PublishNativeSequenceSound(sounding, owner, current, actorFacts.ActorId, cue);
                        }
                    },
                    ScheduleTextureAnimation = (program, sequence) => graphics = ScheduleNativeTextureAnimation(graphics, owner, program, sequence),
                    ScheduleModelEffect = effect => graphics = ScheduleNativeModelEffect(graphics, owner, effect),
                    ApplyTexture = (selector, variant) => ApplyNativeTextureMotion(graphics, owner, selector, variant),
                    AdvanceTexture = (current, selector) => AdvanceNativeTextureMotion(owner, current, selector),
                    RebindActionGeometry = (current, selector) =>
                    {
                        graphics = RebindNativeSequenceGeometry92(graphics with { ActorModels = models.Set(owner, current) }, owner, selector);
                        models = graphics.ActorModels;
                        return models.Get(owner);
                    },
                    ScheduleModelMotion = (current, opcode, first, second, fraction, duration) =>
                    {
                        graphics = graphics with { ActorModels = models.Set(owner, current) };
                        BattleModelOwner target = opcode == 0x9E ? graphics.NativeBindings.DecodeLiteral(first, models) : (graphics.ActionContext?.Target ?? throw new NotSupportedException("Native AD/AE requires its retained50D300 target identity."));
                        (NativeBattleAuxiliaryState auxiliary, bool allocated) = graphics.Auxiliary.AppendModelMotion(opcode, owner, target, first, second, fraction, duration, graphics.NativeBindings.CaptureAliases(graphics.ActorModels));
                        graphics = graphics with { Auxiliary = auxiliary };
                        if (allocated && opcode != 0x9E)
                        {
                            NativeBattleGraphicsModel targetModel = graphics.ActorModels.Get(target);
                            ushort targetFlags = targetModel.Flags2 ?? throw new NotSupportedException("Native AD/AE requires its target flagsWORD2.");
                            graphics = graphics with { ActorModels = graphics.ActorModels.Set(target, targetModel with { Flags2 = (ushort)(targetFlags | 0x20) }) };
                        }
                        models = graphics.ActorModels;
                        return models.Get(owner);
                    },
                    ApplyCurrentContact = current =>
                    {
                        graphics = ApplyNativeCurrentSequenceContact(graphics with { ActorModels = models.Set(owner, current) });
                        models = graphics.ActorModels;
                        return models.Get(owner);
                    },
                    SelectModelSequence = (current, opcode, selector, selected) =>
                    {
                        graphics = SelectNativeSequenceModels(graphics with { ActorModels = models.Set(owner, current) }, owner, opcode, selector, selected);
                        models = graphics.ActorModels;
                        return models.Get(owner);
                    },
                    SchedulePendingSequence = (current, selected) =>
                    {
                        graphics = ScheduleNativeRingSequence505C00(graphics with { ActorModels = models.Set(owner, current) }, owner, selected);
                        models = graphics.ActorModels;
                        return models.Get(owner);
                    },
                    ApplyNextContact = current =>
                    {
                        graphics = ApplyNativeNextSequenceContact(graphics with { ActorModels = models.Set(owner, current) });
                        models = graphics.ActorModels;
                        return models.Get(owner);
                    },
                    ApplyActionContacts = current =>
                    {
                        models = models.Set(owner, current);
                        graphics = ApplyNativeActiveHeaderContacts(graphics with { ActorModels = models });
                        models = graphics.ActorModels;
                        //TODO(TD-009): the component presentation phase observes the original AA publication while104 owns native completion.
                        graphics = graphics with { ActionCallbackReady = true };
                        return models.Get(owner);
                    }
                });
                model = step.Model with { Flags2 = (ushort)(step.Model.Flags2!.Value & ~4) };
                foreach (NativeBattleSequenceCue cue in step.Cues)
                {
                    if (cue.Kind == NativeBattleSequenceCueKind.CommandTitle)
                    {
                        if (graphics.ActionGraphics is { } titleContext)
                        {
                            graphics = PublishNativeOriginalEffectTitle(graphics, titleContext);
                        }
                        continue;
                    }
                    if (actorFacts is not null)
                    {
                        graphics = PublishHostedModelLifecycleCue(graphics, owner, model, actorFacts, cue, services!);
                        continue;
                    }
                    BattleCombatant actor = GraphicsActor(graphics, owner);
                    if (cue.Kind == NativeBattleSequenceCueKind.Text)
                    {
                        string text = (FindNativeOriginalGraphicsBinding(actor.Id) ?? throw new InvalidDataException("Native91 lost its original actor resource binding.")).Resource.ReadText91(cue.First);
                        events.Add(new BattleEvent(Frame, "NativeText", actor.Id, null, cue.Second, text) { NativeMessageMode = 3, NativeTextIdentity = actor.IsPlayer ? null : BattleTextIdentity.Enemy(actor.ModelId, cue.First) });
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
                        NativeGraphics = graphics with { ActorModels = models.Set(owner, model) };
                        RegisterNativeTriggerWidget(NativeGraphics, owner, cue);
                        graphics = NativeGraphics;
                    }
                }
                graphics = PublishNativeTriggerSound(graphics with { ActorModels = models.Set(owner, model) });
            }
            else if ((model.Flags0 & 2) != 0 && model.ModelId4 != 0x8F)
            {
                NativeBattleGraphicsResource refreshResource = NativeGraphicsPoseResource(graphics with { ActorModels = models.Set(owner, model) }, owner);
                model = refreshResource.PoseSource is { } bodySource
                    ? NativeBattleBodyBoneMatrices.Publish508F60(model, bodySource, refreshResource.LinkedPoseSource)
                    : model with { CurrentBodyBoneMatrices = null };
            }
            if (definition.NativeGraphics?.PrimitiveTables is not null && (model.Flags0 & 2) != 0)
            {
                NativeBattleGraphicsResource matrixResource = NativeGraphicsPoseResource(graphics with { ActorModels = models.Set(owner, model) }, owner);
                model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, matrixResource.PoseSource ?? throw new NotSupportedException("Native502AB0 requires its actual body skeleton source."), ReadNativeModelScaleVector502AB0(graphics, model)) };
                model = NativeBattleModelRings.BindWorldMatrix502AB0(models, owner, model);
            }
            models = models.Set(owner, model);
        }
        return graphics with { ActorModels = models };
    }
}
