using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool nativeGraphicsPresentationDriven;

    public void AdvanceNativeGraphicsPresentation(TimeSpan elapsed)
    {
        AdvanceNativeGraphicsPresentation(elapsed, null);
    }

    public void AdvanceNativeGraphicsPresentation(TimeSpan elapsed, TimeSpan? idleWorkBudget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        if (idleWorkBudget is { } budget)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(budget.Ticks);
        }
        if (NativeGraphics is not { } graphics)
        {
            return;
        }
        long subticks = checked(graphics.PresentationSubticks + elapsed.Ticks * FramesPerSecond);
        nativeGraphicsPresentationDriven = true;
        NativeGraphics = graphics with { PresentationSubticks = subticks % TimeSpan.TicksPerSecond };
        for (long count = subticks / TimeSpan.TicksPerSecond; count > 0; count--)
        {
            DispatchTurnBasedFrame();
            AdvanceSkippedGuardianFormPresentation();
        }
    }

    private void QueueNativeGraphicsDeath(BattleCombatant actor)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        int index = graphics.ActorIds.IndexOf(actor.Id);
        if (index < 0)
        {
            throw new InvalidDataException("Native106 death descriptor has no bound graphical allocation.");
        }
        NativeGraphics = QueueNativeDescriptor(graphics, 106, 0, checked((ushort)index));
        actor.NativeStatus0 |= 1;
        actor.NativeCombatantFlags &= ~0xCu;
        actor.Atb = 0;
    }

    private void QueueNativeGraphicsBarrier()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeGraphics = QueueNativeDescriptor(graphics, 112, 0);
    }

    private void QueueNativeGraphicsRemoval(BattleCombatant actor)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        int index = graphics.ActorIds.IndexOf(actor.Id);
        if (index < 0)
        {
            throw new InvalidDataException("Native105 removal descriptor has no bound graphical allocation.");
        }
        NativeGraphics = QueueNativeDescriptor(graphics, 105, 0, checked((ushort)index));
    }

    private NativeBattleGraphicsState RequireNativeGraphics() => NativeGraphics ?? throw new NotSupportedException("Native graphical descriptors require their explicitly initialized seven-row resource bank.");

    private void AdvanceNativeGraphicsFrame()
    {
        if (NativeGraphics is not { } graphics)
        {
            return;
        }
        graphics.Validate();
        ImmutableArray<NativeBattleGraphicsModel> models = graphics.Models;
        uint flags = graphics.Flags;
        if (graphics.EntryLifecycle == 1)
        {
            graphics = QueueNativeEntryModels(graphics);
        }
        //500900: descriptor/callback listAA4, then published effect listAA8, then model listA94, then camera5033E0.
        try
        {
            graphics = AdvanceNativeGraphicsRequests(graphics);
            flags = graphics.Flags;
            graphics = AdvanceNativeAuxiliary(graphics);
            graphics = AdvanceNativeModuleEffects(graphics);
            graphics = AdvanceNativeOriginalEffect(graphics);
            graphics = AdvanceGuardianFormPresentation(graphics);
        }
        catch when (GuardianFormOwnsCurrentPresentation())
        {
            FailOwnedGuardianFormPresentation();
            throw;
        }
        flags = graphics.Flags;
        graphics = SubmitNativeModulePackets(graphics);
        graphics = ConsumeNativeGraphicsContacts(graphics);
        models = graphics.Models;
        NativeBattleEffectsStep effects = graphics.Effects.Advance(models, index => ProjectNativeDamageText(graphics, index));
        foreach (byte index in effects.ResetModelStatuses)
        {
            graphics = ApplyNativeGraphicsStatus509CD0(graphics with { Models = models }, index, 16);
            models = graphics.Models;
        }
        graphics = graphics with { Models = models, Effects = effects.State };
        graphics = FrameGuardianFormIdleCamera(graphics);
        for (int index = 0; index < 7; index++)
        {
            NativeBattleGraphicsModel model = models[index];
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            ushort modelFlags = model.Flags2 ?? throw new InvalidDataException("Native loaded graphics model lacks Flags2.");
            if ((modelFlags & 4) == 0)
            {
                NativeBattleEffectMemory colorMemory = NativeEffectMemory ?? throw new NotSupportedException("Native50A0A0 requires the retained process model-color owner.");
                NativeBattleSummonColors colorRules = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native50A0A0 requires its authenticated interpolation constants.");
                NativeBattleStageColors modelColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Native50A0A0 requires the actual stage color DWORDs.");
                NativeBattleModelColorStep colorStep = NativeBattleModelColors.Advance50A0A0(model, flags, colorRules with { Transition = modelColors.Color40, Normal = modelColors.Color44 }, colorMemory.ModelColorOverrides);
                model = colorStep.Model;
                NativeEffectMemory = colorMemory with { ModelColorOverrides = colorStep.Overrides };
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
                    BattleCombatant actor = GraphicsActor(graphics, index);
                    actor.NativeCombatantFlags = (actor.NativeCombatantFlags | 0x40) & ~1u;
                    actor.NativeStatus0 |= 1;
                }
            }
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
            if ((model.Flags0 & 2) != 0 && (model.Flags0 & 8) == 0 && !((model.Flags8!.Value & 0x18) != 0 && (model.ResourceFlags44!.Value & 0x40) != 0) && (flags & 0x201) == 0)
            {
                NativeBattleGraphicsBinding binding = FindNativeGraphicsBinding(graphics.ActorIds[index]!) ?? throw new InvalidDataException("Native graphics callback lost its immutable resource binding.");
                NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
                NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(NativeEntryAdvantage, NativeCamera?.Mask)
                {
                    OriginalResource = model.ModelId4 >= 16 ? model.OriginalResourceRow104 == model.ActiveResourceRow100 ? resource : ResolveNativeEnemyResource(model.OriginalResourceRow104 ?? throw new NotSupportedException("Native enemy original resource row remains unknown."), binding.Resource) : binding.Resource,
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
                    ScheduleModelGroupFade = group => graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendModelGroupFade50F0B0(index, group) },
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
                    PlaySound = (current, cue) => PublishNativeSequenceSound(graphics with { Models = models.SetItem(index, current) }, current, GraphicsActor(graphics, index), cue),
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
                        (NativeBattleAuxiliaryState auxiliary, bool allocated) = graphics.Auxiliary.AppendModelMotion(opcode, index, target, first, second, fraction, duration);
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
                    BattleCombatant actor = GraphicsActor(graphics, index);
                    if (cue.Kind == NativeBattleSequenceCueKind.Text)
                    {
                        NativeBattleAiDefinition ai = actor.NativeAi ?? throw new NotSupportedException("Native91 requires its actor-owned text bank.");
                        if (cue.First >= ai.Texts.Length)
                        {
                            throw new InvalidDataException("Native91 exceeds the actor text bank.");
                        }
                        events.Add(new BattleEvent(Frame, "NativeText", actor.Id, null, cue.Second, ai.Texts[cue.First]) { NativeMessageMode = 3 });
                    }
                    else if (cue.Kind == NativeBattleSequenceCueKind.SuspendParticipant)
                    {
                        actor.NativeCombatantFlags |= 0x40;
                        actor.NativeStatus0 |= 1;
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
            }
            models = models.SetItem(index, model);
        }
        graphics = AdvanceNativeStage506C30(graphics with { Models = models });
        NativeGraphics = graphics with { Callback = checked(graphics.Callback + 1) };
        AdvanceNativeCamera(flags);
        if (NativeCamera is not null)
        {
            NativeCamera = NativeCamera.PublishProjection(flags);
        }
        //The original loc_5005A0 tail calls501B60 at50065E regardless of flags4/8, before501230 and505D20.
        DrainNativeMusic501B60();
        if (NativeEffectMemory is { } effectMemory)
        {
            effectMemory = effectMemory.PublishSound();
            if ((graphics.Flags & 8) != 0 && effectMemory.Textures is { } textures)
            {
                effectMemory = DrainNativeTextureUploads(effectMemory, textures);
            }
            NativeEffectMemory = effectMemory;
        }
        NativeGraphics = FinishNativePrimitiveFrame500900(NativeGraphics!);
        NativeGraphics = PublishNativePcRenderTail47CF60(NativeGraphics);
    }

    private NativeBattleEffectMemory DrainNativeTextureUploads(NativeBattleEffectMemory memory, NativeBattleEffectTextureState textures)
    {
        NativeBattleEffectTextureDrain drain = textures.Drain505D20(memory.Arena, NativeFileStaging, address => NativeBattleProcessTim.Resolve(memory, address), memory.ScriptedEffects);
        NativeBattlePrimitiveProcess? process = memory.PrimitiveProcess;
        if (process?.TextureWindowCache is { } cache)
        {
            foreach (NativeBattleEffectVramUpload upload in drain.Uploads)
            {
                cache = cache.Invalidate464850(upload.X, upload.Y, upload.X + upload.Width - 1, upload.Y + upload.Height - 1, textures);
            }
            process = process with { TextureWindowCache = cache };
        }
        return memory with { Textures = drain.State, PrimitiveProcess = process, Arena = drain.Arena ?? memory.Arena };
    }

    private NativeBattleGraphicsState AdvanceNativeGraphicsRequests(NativeBattleGraphicsState graphics)
    {
        return AdvanceNativeDescriptors(graphics);
    }

    private static bool ModelsBusy(IReadOnlyList<NativeBattleGraphicsModel> models)
    {
        return models.Any(model => (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40));
    }

    private BattleCombatant GraphicsActor(NativeBattleGraphicsState graphics, int index) => GetActor(graphics.ActorIds[index] ?? throw new InvalidDataException("Native graphical row has no bound allocation identity."));

    private NativeBattleGraphicsState ConsumeNativeGraphicsContacts(NativeBattleGraphicsState graphics)
    {
        foreach (NativeBattleContactPacket packet in graphics.PendingContacts)
        {
            packet.Validate();
            //506690 invokes493D80 before checking whether the primary model is loaded.
            graphics = PublishNativeReadyContact(graphics, packet.Primary);
            if (packet.Secondary.ActorIndex != 255)
            {
                graphics = PublishNativeReadyContact(graphics, packet.Secondary);
            }
            NativeBattleGraphicsModel primary = graphics.Models[packet.Primary.ActorIndex];
            BattleCombatant actor = GraphicsActor(graphics, packet.Primary.ActorIndex);
            NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(packet.Primary, primary, definition.NativeGraphics!.Rules, NativeBattleStatusBits(actor));
            graphics = ApplyNativeGraphicsContact(graphics, packet.Primary, plan);
            if (plan.Loaded && packet.Secondary.ActorIndex != 255)
            {
                NativeBattleContactHalf half = packet.Secondary;
                BattleCombatant secondaryActor = GraphicsActor(graphics, half.ActorIndex);
                NativeBattleContactEffectPlan secondary = NativeBattleContactEffects.Plan(half, graphics.Models[half.ActorIndex], definition.NativeGraphics.Rules, NativeBattleStatusBits(secondaryActor), secondary: true);
                graphics = ApplyNativeGraphicsContact(graphics, half, secondary);
            }
        }
        return graphics with { PendingContacts = [] };
    }

    private NativeBattleGraphicsState ApplyNativeGraphicsContact(NativeBattleGraphicsState graphics, NativeBattleContactHalf half, NativeBattleContactEffectPlan plan)
    {
        if (plan.StageTargetRefresh)
        {
            if (definition.NativeStageSource?.Callback?.Address == NativeBattleStage37.CallbackAddress)
            {
                UpdateNativeStage37Target3(graphics);
            }
            else if (definition.NativeStageSource?.Callback?.Address == NativeBattleFinalStage142.CallbackAddress)
            {
                UpdateNativeFinalStage142Target3(graphics);
            }
        }
        if (!plan.Loaded)
        {
            return graphics;
        }
        if (!plan.Secondary && (graphics.Flags & 0x10000000) != 0)
        {
            graphics = graphics with { Flags = graphics.Flags & ~0x10000000u };
        }
        else if (!plan.Secondary && (half.HitFlags3 & 2) != 0)
        {
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 0, 255) };
        }
        int index = half.ActorIndex;
        graphics = ApplyNativeGraphicsStatus509CD0(graphics, index, plan.ImmediateStatusFlags!.Value);
        NativeBattleGraphicsModel model = graphics.Models[index];
        if (plan.EjectionService7)
        {
            NativeBattleModuleResource resource = RequireNativeModuleResource(21);
            model = NativeBattleGraphicsModels.StartService(model, 7);
            NativeBattleVector position = model.Transform?.Position28 ?? throw new NotSupportedException("Native ejection sound requires its current model positionWORD28.");
            NativeBattleModuleAnchor anchor = new(position.X, position.Y, position.Z, 0);
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendModelResource(index, resource, 0x200015) };
            PublishNativeModuleSound(0x0D, 8, anchor, graphics.ActorIds[index]);
            PublishNativeModuleSound(0x0E, 16, anchor, graphics.ActorIds[index]);
        }
        if (plan.SelectSequence)
        {
            if (model.ReadLinkedModelIndex140() is not null)
            {
                graphics = SelectNativeRingContactSequence(graphics with { Models = graphics.Models.SetItem(index, model) }, index, plan.Sequence);
                model = graphics.Models[index];
            }
            else
            {
                model = SelectNativeContactSequence(model, plan.Sequence);
            }
            if (model.ModelId4 < 16)
            {
                graphics = RefreshNativeGuardianStandingSequence(graphics with { Models = graphics.Models.SetItem(index, model) }, checked((byte)index));
                model = graphics.Models[index];
            }
        }
        if (!plan.Secondary && (half.Flags2 & 0x30) != 0)
        {
            byte resourceId = (half.Flags2 & 0x30) switch { 0x10 => 39, 0x20 => 40, _ => 38 };
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendModelResource(index, RequireNativeModuleResource(resourceId), 0x100000u | resourceId) };
        }
        if (!plan.Secondary && (graphics.Flags & 0x10) == 0 && (half.HitFlags3 & 8) != 0)
        {
            graphics = ApplyNativeSequenceCover5065B0(graphics with { Models = graphics.Models.SetItem(index, model) }, half);
            model = graphics.Models[index];
        }
        NativeBattleEffectsState effects = NativeBattleContactEffects.ProduceTasks(graphics.Effects, half, plan);
        return graphics with { Models = graphics.Models.SetItem(index, model), Effects = effects };
    }

    private NativeBattleModuleResource RequireNativeModuleResource(byte id)
    {
        return definition.NativeModuleResources.SingleOrDefault(resource => resource.Id == id) ?? throw new NotSupportedException($"Native contact effect requires its installed module resource{id}.");
    }

    private static NativeBattleGraphicsModel QueueNativeModelSequence(NativeBattleGraphicsModel model, byte sequence)
    {
        return (model.ResourceFlags44!.Value & 0x40) != 0 ? NativeBattleGraphicsModels.SelectSequence(model, sequence) : model with { SequenceState = model.SequenceState! with { Pending3 = sequence } };
    }

    private static NativeBattleGraphicsModel SelectNativeContactSequence(NativeBattleGraphicsModel model, byte sequence)
    {
        ushort flags = model.ResourceFlags44!.Value;
        if (model.ModelId4 >= 16)
        {
            return sequence != 0 && (flags & 0x140) != 0 ? NativeBattleGraphicsModels.SelectSequence(model, sequence) : QueueNativeModelSequence(model, sequence);
        }
        if ((model.Flags8!.Value & 0x300000) == 0 || sequence == 3)
        {
            if ((flags & 0x140) != 0)
            {
                return sequence == 0 ? model : NativeBattleGraphicsModels.SelectSequence(model with { SequenceState = model.SequenceState! with { Default2 = 1 } }, sequence);
            }
            return model with { SequenceState = model.SequenceState! with { Default2 = 1 } };
        }
        return model;
    }

    private NativeBattleGraphicsModel SetNativeGraphicsFlags(NativeBattleGraphicsModel model, uint flags, NativeBattleGraphicsState graphics)
    {
        uint previous = model.Flags8 ?? throw new InvalidDataException("Native509D10 requires its current graphics status DWORD.");
        uint added = (previous ^ flags) & flags;
        uint removed = (previous ^ flags) & ~flags;
        ImmutableArray<byte?> timers = model.StatusTimers;
        for (int bit = 0; bit < 32; bit++)
        {
            if ((added & (1u << bit)) != 0)
            {
                timers = timers.SetItem(bit, 0);
            }
        }
        if ((added & 0xA) != 0)
        {
            flags &= ~0x800000u;
        }
        if ((added & 0x400000) != 0)
        {
            if (model.ModelId4 >= 16)
            {
                model = NativeBattleGraphicsModels.StartService(model with { Flags0 = (ushort)(model.Flags0 | 0x10) }, 5);
                flags &= ~0x400000u;
            }
            else
            {
                model = model with { Flags0 = (ushort)(model.Flags0 & ~2) };
            }
        }
        if ((model.ModelId4 ?? throw new InvalidDataException("Native509D10 requires the model BYTE4.")) < 16)
        {
            if (((added | removed) & 0x410) != 0)
            {
                UploadNativeStatusPalette509E00(checked((byte)model.Index), flags);
            }
            if ((removed & 2) != 0)
            {
                ApplyNativeTextureMotion(graphics with { Models = graphics.Models.SetItem(model.Index, model) }, model.Index, 0, 0);
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x400) };
            }
            if ((added & 0x1000) != 0)
            {
                ApplyNativeTextureMotion(graphics with { Models = graphics.Models.SetItem(model.Index, model) }, model.Index, 0, 1);
            }
            else if ((removed & 0x1000) != 0)
            {
                ApplyNativeTextureMotion(graphics with { Models = graphics.Models.SetItem(model.Index, model) }, model.Index, 0, 0);
            }
        }
        return model with { Flags8 = flags, StatusTimers = timers };
    }
}
