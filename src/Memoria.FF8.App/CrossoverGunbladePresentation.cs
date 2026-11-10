using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGunbladePresentationState(NativeBattleGraphicsModel Model, NativeBattleUiServices Ui, NativeBattleUiRandomState? UiRandom)
{
    public NativeBattleTriggerWidget? Widget { get; init; }
    public NativeBattleTriggerReceipt? PendingTrigger { get; init; }
    public NativeBattleTriggerReceipt? ContactTiming { get; init; }
    public bool ContactPublished { get; init; }
    public bool ReturnedToStanding { get; init; }
    public bool PendingTriggerSound { get; init; }
    public bool Complete => ReturnedToStanding && Widget is null;

    public bool TriggerWindowOpen => Widget is { ClosingPhase: 0, Input: { TriggerTiming: 0, Phase: 4 or 6 } input } widget
        && NativeGunbladeAttack.TriggerSucceeded(checked((short)(input.PoseClock + input.SubframeClock - widget.Block.Timeline.InputThresholds[input.ContactIndex] + 5)));
}

internal sealed record CrossoverGunbladePresentationStep(CrossoverGunbladePresentationState State, NativeBattleTriggerReceipt? Contact, ImmutableArray<NativeBattleSequenceCue> Cues, bool TriggerSound);

/// <summary>Runs the original weapon sequence and trigger callbacks on the host's presentation timeline.</summary>
internal static class CrossoverGunbladePresentation
{
    public static CrossoverGunbladePresentationState Begin(NativeBattleGraphicsModel squall, NativeBattleGraphicsResource weapon, ImmutableArray<byte> commandSequences, NativeBattleUiRandomState? random)
    {
        if (squall is not { Index: >= 0 and < 3, ModelId4: 0 } || (squall.Flags0 & 2) == 0 || commandSequences.Length != 39 || commandSequences[1] == 0 || !weapon.Contains(commandSequences[1]))
        {
            throw new InvalidDataException("Gunblade presentation requires a loaded Squall model and its original Attack sequence binding.");
        }
        return new(NativeBattleGraphicsModels.SelectSequence(squall, commandSequences[1]), NativeBattleUiServices.Initialize(), random);
    }

    public static CrossoverGunbladePresentationStep Advance(CrossoverGunbladePresentationState state, NativeBattleGraphicsResource weapon, NativeBattleSequenceInputs services, ushort savedOptions, uint publicationDuration, bool triggerPressed, Action<NativeBattleTriggerReceipt> prepareContact)
    {
        if (state.Complete)
        {
            throw new InvalidOperationException("The Gunblade presentation has returned to standing.");
        }
        if (triggerPressed && (savedOptions & 1) == 0 && state.Widget is { ClosingPhase: 0, Input.TriggerTiming: 0 } widget)
        {
            state = state with { Widget = widget with { PendingTrigger = true } };
        }
        for (int service = 0; service < 3; service++)
        {
            AdvanceUi();
        }
        if (state.PendingTrigger is { } timing)
        {
            timing.Validate();
            if (state.ContactTiming is not null || timing.ActorIndex != state.Model.Index || timing.ContactIndex != 1 || timing.TotalContacts != 1)
            {
                throw new InvalidDataException("Ordinary Gunblade Attack must recompute exactly one selected contact.");
            }
            prepareContact(timing);
            state = state with { ContactTiming = timing, PendingTrigger = null };
        }

        NativeBattleTriggerReceipt? contact = null;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(state.Model, weapon, services with
        {
            OriginalResource = weapon,
            ApplyActionContacts = current =>
            {
                if (state.ContactPublished || contact is not null || state.ContactTiming is not { } currentTiming)
                {
                    throw new InvalidDataException("Gunblade AA requires one prior ordinary trigger receipt.");
                }
                contact = currentTiming;
                return (services.ApplyActionContacts ?? throw new NotSupportedException("Gunblade AA requires its host contact publisher."))(current);
            }
        });
        NativeBattleGraphicsModel model = step.Model;
        NativeBattlePoseSource pose = weapon.PoseSource ?? throw new InvalidDataException("Gunblade presentation requires the original Squall skeleton.");
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, pose) };
        state = state with { Model = model, ContactPublished = state.ContactPublished || contact is not null };
        foreach (NativeBattleSequenceCue cue in step.Cues)
        {
            if (cue.Kind == NativeBattleSequenceCueKind.TriggerWindow)
            {
                if (state.Widget is not null || state.ContactTiming is not null || cue.TriggerWindow is not { } block || block.Timeline.InputThresholds.Length != 1)
                {
                    throw new InvalidDataException("Ordinary Gunblade Attack requires one original AB trigger block.");
                }
                NativeBattleTriggerWidget registered = NativeBattleTriggerWidget.Register(checked((byte)model.Index), block, savedOptions, state.Ui) with { SourceSequence = checked((byte)cue.First), SourceAbOffset = cue.Second };
                state = state with { Widget = registered };
            }
            if (cue.Kind == NativeBattleSequenceCueKind.RestoreStanding)
            {
                if (!state.ContactPublished)
                {
                    throw new InvalidDataException("Gunblade Attack returned to standing without publishing its physical contact.");
                }
                state = state with { ReturnedToStanding = true };
            }
        }
        bool triggerSound = state.PendingTriggerSound;
        state = state with { Ui = state.Ui.QueuePublication(publicationDuration), PendingTriggerSound = false };
        AdvanceUi();
        return new(state, contact, step.Cues, triggerSound);

        void AdvanceUi()
        {
            NativeBattleUiServices ui = state.Ui.AdvancePublication();
            state = state with { Ui = ui };
            if (state.Widget is not { } active)
            {
                return;
            }
            NativeBattleTriggerWidgetStep next = NativeBattleTriggerWidgetRules.Advance(active, ui.Frame33 == true, (savedOptions & 1) != 0, state.UiRandom);
            state = state with { Widget = next.Unregistered ? null : next.Widget, UiRandom = next.Random, PendingTriggerSound = state.PendingTriggerSound || next.PlayTriggerSound };
            if (next.Contact is { } receipt)
            {
                if (state.PendingTrigger is not null)
                {
                    throw new InvalidDataException("Gunblade Attack has an unconsumed trigger receipt.");
                }
                state = state with { PendingTrigger = new(active.ActorIndex, receipt.Index, receipt.Total, receipt.TriggerTiming, 32) };
            }
        }
    }
}
