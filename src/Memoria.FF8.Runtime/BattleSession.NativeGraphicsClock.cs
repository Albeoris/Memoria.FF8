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
            AdvanceNativeGraphicsFrame();
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
        NativeGraphics = graphics with { Requests = graphics.Requests.Add(new(106, checked((byte)index))) };
        actor.NativeStatus0 |= 1;
        actor.NativeCombatantFlags &= ~0xCu;
        actor.Atb = 0;
    }

    private void QueueNativeGraphicsBarrier()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeGraphics = graphics with { Requests = graphics.Requests.Add(new(112, null)) };
    }

    private void QueueNativeGraphicsRemoval(BattleCombatant actor)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        int index = graphics.ActorIds.IndexOf(actor.Id);
        if (index < 0)
        {
            throw new InvalidDataException("Native105 removal descriptor has no bound graphical allocation.");
        }
        NativeGraphics = graphics with { Requests = graphics.Requests.Add(new(105, checked((byte)index))) };
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
        byte entryPhase = graphics.EntryGatePhase;
        if (entryPhase == 0)
        {
            flags |= 0x80;
            entryPhase = 1;
        }
        else if (entryPhase == 1 && !ModelsBusy(models))
        {
            flags &= ~0x80u;
            entryPhase = 2;
        }
        graphics = graphics with { Flags = flags, EntryGatePhase = entryPhase };
        //500900: descriptor/callback listAA4, then published effect listAA8, then model listA94, then camera5033E0.
        graphics = AdvanceNativeGraphicsRequests(graphics);
        graphics = AdvanceNativeAuxiliary(graphics);
        graphics = ConsumeNativeGraphicsContacts(graphics);
        models = graphics.Models;
        NativeBattleEffectsStep effects = graphics.Effects.Advance(models);
        foreach (byte index in effects.ResetModelStatuses)
        {
            models = models.SetItem(index, SetNativeGraphicsFlags(models[index], 16));
        }
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
                model = NativeBattleGraphicsModels.AdvanceAppearance(model, (flags & 1) != 0);
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
                NativeBattleGraphicsResource resource = model.ResourceMagicId is ushort magic ? definition.NativeRenzokukenResources.Single(resource => resource.MagicId == magic).GraphicsResource : binding.Resource;
                NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(NativeEntryAdvantage, NativeCamera?.Mask) { OriginalResource = binding.Resource, GlobalRandom = NextNativeGlobalRandom });
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
                        if (graphics.ActionActorId != actor.Id)
                        {
                            throw new InvalidDataException("Native AA lacks its current command callback owner.");
                        }
                        graphics = graphics with { ActionCallbackReady = true };
                    }
                    else if (cue.Kind == NativeBattleSequenceCueKind.TriggerWindow)
                    {
                        throw new NotSupportedException("Native AB callback requires its event-owned trigger-window registration.");
                    }
                }
            }
            models = models.SetItem(index, model);
        }
        NativeGraphics = graphics with { Models = models, Effects = effects.State, Callback = checked(graphics.Callback + 1) };
        AdvanceNativeCamera(flags);
    }

    private NativeBattleGraphicsState AdvanceNativeGraphicsRequests(NativeBattleGraphicsState graphics)
    {
        while (!graphics.Requests.IsEmpty)
        {
            NativeBattleGraphicsRequest request = graphics.Requests[0];
            if (request.State == 255)
            {
                graphics = graphics with { Requests = graphics.Requests.RemoveAt(0), EntryReady = graphics.EntryReady || request.Entry };
                continue;
            }
            if (request.Command == 106)
            {
                int index = request.ModelIndex!.Value;
                NativeBattleGraphicsModel model = SetNativeGraphicsFlags(graphics.Models[index], 0);
                graphics = graphics with { Models = graphics.Models.SetItem(index, NativeBattleGraphicsModels.SelectSequence(model, 3)), Requests = graphics.Requests.RemoveAt(0) };
                continue;
            }
            if (request.Command == 105)
            {
                int index = request.ModelIndex!.Value;
                NativeBattleGraphicsModel model = graphics.Models[index];
                if (request.State == 0 && (model.Flags0 & 2) == 0)
                {
                    graphics = graphics with { Requests = graphics.Requests.RemoveAt(0) };
                    continue;
                }
                if (request.State == 0)
                {
                    model = model with { Flags0 = (ushort)(model.Flags0 | 0x10) };
                    if (model.Kind5 == 0)
                    {
                        model = NativeBattleGraphicsModels.StartService(model, 5);
                        request = request with { State = 8 };
                    }
                }
                else if (model.Kind5 == 0)
                {
                    request = request with { State = 255 };
                }
                return graphics with { Models = graphics.Models.SetItem(index, model), Requests = graphics.Requests.SetItem(0, request) };
            }
            byte completion = 0;
            if (graphics.Effects.PublishedCount == 0 && !ModelsBusy(graphics.Models))
            {
                _ = NativeBattleGraphicsCompletion.PollTask112(graphics.Effects.PublishedCount, graphics.Models.Select(model => model.CompletionInputs()).ToArray(), NativeCameraTaskMask, ref completion);
            }
            return graphics with { Requests = graphics.Requests.SetItem(0, request with { State = completion == 255 ? (byte)255 : (byte)8 }), EntryBarrierCompletion = request.Entry ? completion : graphics.EntryBarrierCompletion };
        }
        return graphics;
    }

    private static bool ModelsBusy(IReadOnlyList<NativeBattleGraphicsModel> models)
    {
        return models.Any(model => (model.Flags0 & 2) != 0 && (model.Flags0 & 8) == 0 && ((model.Flags8 ?? throw new InvalidDataException("Native508540 lacks graphics statusDWORD8.")) & 0x18) == 0 && (model.Kind5 != 0 || ((model.ResourceFlags44 ?? throw new InvalidDataException("Native508540 lacks resourceWORD44.")) & 0x40) == 0));
    }

    private BattleCombatant GraphicsActor(NativeBattleGraphicsState graphics, int index) => GetActor(graphics.ActorIds[index] ?? throw new InvalidDataException("Native graphical row has no bound allocation identity."));

    private NativeBattleGraphicsState ConsumeNativeGraphicsContacts(NativeBattleGraphicsState graphics)
    {
        foreach (NativeBattleContactPacket packet in graphics.PendingContacts)
        {
            packet.Validate();
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
        if (plan.EjectionService7 || (half.HitFlags3 & 0xA) != 0 || (half.Flags2 & 0x30) != 0)
        {
            throw new NotSupportedException("Native contact requires its ejection/critical/Cover/defensive graphical effect module.");
        }
        int index = half.ActorIndex;
        NativeBattleGraphicsModel model = SetNativeGraphicsFlags(graphics.Models[index], plan.ImmediateStatusFlags!.Value);
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
        NativeBattleEffectsState effects = NativeBattleContactEffects.ProduceTasks(graphics.Effects, half, plan);
        return graphics with { Models = graphics.Models.SetItem(index, model), Effects = effects };
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
