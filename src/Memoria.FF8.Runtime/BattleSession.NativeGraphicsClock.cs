using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool nativeGraphicsPresentationDriven;

    public void AdvanceNativeGraphicsPresentation(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
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
        graphics = AdvanceNativeGraphicsRequests(graphics);
        flags = graphics.Flags;
        graphics = AdvanceNativeAuxiliary(graphics);
        graphics = AdvanceNativeModuleEffects(graphics);
        graphics = ConsumeNativeGraphicsContacts(graphics);
        models = graphics.Models;
        NativeBattleEffectsStep effects = graphics.Effects.Advance(models);
        foreach (byte index in effects.ResetModelStatuses)
        {
            models = models.SetItem(index, SetNativeGraphicsFlags(models[index], 16));
        }
        graphics = graphics with { Models = models, Effects = effects.State };
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
                model = NativeBattleGraphicsModels.AdvanceCover(model);
                model = NativeBattleGraphicsModels.AdvanceAppearance(model, (flags & 1) != 0);
                if (model.Kind5 is 11 or 12)
                {
                    NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native summon model service requires its authenticated color constants.");
                    model = NativeBattleSummonModels.Advance(model, (flags & 1) != 0, colors);
                }
                byte camera = model.Kind5 == 5 && model.Phase6 > 0 ? NativeCameraTaskMask : (byte)0;
                NativeBattleGraphicsRemoval removal = NativeBattleGraphicsModels.AdvanceRemoval(model, (flags & 1) != 0, graphics.ModelLoadCount != 0, camera);
                model = removal.Model;
                if (removal.ReleaseParticipant)
                {
                    BattleCombatant actor = GraphicsActor(graphics, index);
                    actor.NativeCombatantFlags = (actor.NativeCombatantFlags | 0x40) & ~1u;
                    actor.NativeStatus0 |= 1;
                }
            }
            if ((model.Flags0 & 2) != 0 && (model.Flags0 & 8) == 0 && !((model.Flags8!.Value & 0x18) != 0 && (model.ResourceFlags44!.Value & 0x40) != 0) && (flags & 0x201) == 0)
            {
                NativeBattleGraphicsBinding binding = FindNativeGraphicsBinding(graphics.ActorIds[index]!) ?? throw new InvalidDataException("Native graphics callback lost its immutable resource binding.");
                NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
                NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(NativeEntryAdvantage, NativeCamera?.Mask)
                {
                    OriginalResource = binding.Resource,
                    GlobalRandom = NextNativeGlobalRandom,
                    ReadExternal = variable => ReadNativeGraphicsActionVariable(graphics, variable),
                    ScheduleSoundParticle = (program, soundTwo, bone) => graphics = ScheduleNativeSoundParticle(graphics, index, program, soundTwo, bone),
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
                    else if (cue.Kind is NativeBattleSequenceCueKind.ActorSound or NativeBattleSequenceCueKind.SystemSound)
                    {
                        events.Add(new BattleEvent(Frame, "NativeSequenceSound", actor.Id, null, 0, string.Empty) { NativeSound = new(checked((byte)cue.First), cue.Kind == NativeBattleSequenceCueKind.ActorSound, checked((byte)cue.Second), checked((ushort)cue.Third)) });
                    }
                }
            }
            models = models.SetItem(index, model);
        }
        NativeGraphics = graphics with { Models = models, Callback = checked(graphics.Callback + 1) };
        AdvanceNativeCamera(flags);
        if (NativeCamera is not null)
        {
            NativeCamera = NativeCamera.PublishProjection(flags);
        }
        if (NativeEffectMemory is { } effectMemory)
        {
            NativeEffectMemory = effectMemory.PublishSound();
        }
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
        if (plan.SpecialSound)
        {
            throw new NotSupportedException("Native model140 contact requires its50E510 effect-service owner.");
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
        NativeBattleGraphicsModel model = SetNativeGraphicsFlags(graphics.Models[index], plan.ImmediateStatusFlags!.Value);
        if (plan.EjectionService7)
        {
            NativeBattleModuleResource resource = RequireNativeModuleResource(21);
            model = NativeBattleGraphicsModels.StartService(model, 7);
            NativeBattleVector position = model.Transform?.Position28 ?? throw new NotSupportedException("Native ejection sound requires its current model positionWORD28.");
            NativeBattleModuleAnchor anchor = new(position.X, position.Y, position.Z, 0);
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendModelResource(index, resource, 0x200015), ModuleDraws = graphics.ModuleDraws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 0x0D, SoundFlags = 8 }).Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 0x0E, SoundFlags = 16 }) };
        }
        if (plan.SelectSequence)
        {
            model = SelectNativeContactSequence(model, plan.Sequence);
            if (model.ModelId4 < 16)
            {
                NativeBattleSequenceState state = model.SequenceState!;
                byte standing = NativeStandingSequence(model.Flags8!.Value);
                model = state.Pending3 != 0 ? model with { SequenceState = state with { Pending3 = standing != model.Sequence ? standing : (byte)0 } } : standing != model.Sequence ? QueueNativeModelSequence(model, standing) : model;
            }
        }
        if (!plan.Secondary && (half.Flags2 & 0x30) != 0)
        {
            byte resourceId = (half.Flags2 & 0x30) switch { 0x10 => 39, 0x20 => 40, _ => 38 };
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendModelResource(index, RequireNativeModuleResource(resourceId), 0x100000u | resourceId) };
        }
        if (!plan.Secondary && (graphics.Flags & 0x10) == 0 && (half.HitFlags3 & 8) != 0)
        {
            int sourceIndex = half.HitFlags3 >> 6;
            NativeBattleGraphicsModel source = graphics.Models[sourceIndex];
            NativeBattleVector position = source.Transform?.Position28 ?? throw new NotSupportedException("Native Cover source requires its current positionWORD28/32.");
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Cover recipient requires its current positionWORD28/32.");
            model = NativeBattleGraphicsModels.SelectSequence(model with { Transform = transform with { Position28 = transform.Position28 with { X = position.X, Z = position.Z } } }, 22);
            if (sourceIndex == index)
            {
                model = NativeBattleGraphicsModels.StartService(model, 9);
            }
            else
            {
                graphics = graphics with { Models = graphics.Models.SetItem(sourceIndex, NativeBattleGraphicsModels.StartService(source, 9)) };
            }
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

    private static NativeBattleGraphicsModel SetNativeGraphicsFlags(NativeBattleGraphicsModel model, uint flags)
    {
        uint previous = model.Flags8 ?? throw new InvalidDataException("Native509D10 requires its current graphics status DWORD.");
        uint added = (previous ^ flags) & flags;
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
        return model with { Flags8 = flags, StatusTimers = timers };
    }
}
