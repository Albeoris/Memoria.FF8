using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeRenzokukenTrigger(NativeBattleUiServices Ui, ushort Options, NativeBattleUiRandomState? Random)
{
    public NativeBattleTriggerWidget? Widget { get; init; }
    public NativeBattleTriggerReceipt? Pending { get; init; }
    public int Consumed { get; init; }
    public bool PendingSound { get; init; }
    public bool TriggerWindowOpen => (Options & 1) == 0 && Widget is { ClosingPhase: 0, Input: { TriggerTiming: 0, Phase: 4 or 6 } input } widget
        && NativeGunbladeAttack.TriggerSucceeded(checked((short)(input.PoseClock + input.SubframeClock - widget.Block.Timeline.InputThresholds[input.ContactIndex] + 5)));

    public CrossoverNativeRenzokukenTrigger Register(NativeBattleGraphicsModel source, NativeBattleSequenceCue cue)
    {
        if (Widget is not null || Pending is not null || Consumed != 0 || cue is not { Kind: NativeBattleSequenceCueKind.TriggerWindow, TriggerWindow: { } block } || source.ResourceMagicId is not (141 or 160) || (Options & 1) != 0 && Random is null)
        {
            throw new InvalidDataException("Renzokuken requires one original replacement AB widget and its source options.");
        }
        NativeBattleTriggerWidget registered = NativeBattleTriggerWidget.Register(checked((byte)source.Index), block, Options, Ui) with { SourceSequence = checked((byte)cue.First), SourceAbOffset = cue.Second, SourceMagicId = source.ResourceMagicId };
        NativeBattleUiServices ui = registered.Visible ? Ui.SetFlags(0x1800) with { Menu957 = 6, Menu955 = -2 } : Ui.SetFlags(0, 0x1800);
        return this with { Widget = registered, Ui = ui };
    }

    public CrossoverNativeRenzokukenTrigger Press()
    {
        return (Options & 1) == 0 && Widget is { ClosingPhase: 0, Input.TriggerTiming: 0 } widget ? this with { Widget = widget with { PendingTrigger = true } } : this;
    }

    public (CrossoverNativeRenzokukenTrigger State, NativeBattleTriggerReceipt? Receipt) BeforeGraphics()
    {
        CrossoverNativeRenzokukenTrigger state = this;
        for (int service = 0; service < 3; service++)
        {
            state = state.AdvanceUi();
        }
        if (state.Pending is not { } timing)
        {
            return (state, null);
        }
        timing.Validate();
        if (timing.ContactIndex != state.Consumed + 1)
        {
            throw new InvalidDataException("Renzokuken received a duplicate or unordered original trigger receipt.");
        }
        return (state with { Pending = null, Consumed = state.Consumed + 1 }, timing);
    }

    public (CrossoverNativeRenzokukenTrigger State, bool Sound) AfterGraphics(uint duration)
    {
        return ((this with { Ui = Ui.QueuePublication(duration), PendingSound = false }).AdvanceUi(), PendingSound);
    }

    private CrossoverNativeRenzokukenTrigger AdvanceUi()
    {
        NativeBattleUiServices ui = Ui.AdvancePublication();
        if (Widget is not { } active)
        {
            return this with { Ui = ui };
        }
        NativeBattleTriggerWidgetStep step = NativeBattleTriggerWidgetRules.Advance(active, ui.Frame33 == true, (Options & 1) != 0, Random);
        NativeBattleTriggerReceipt? pending = Pending;
        if (step.Contact is { } contact)
        {
            if (pending is not null)
            {
                throw new InvalidDataException("Renzokuken overwrote an unconsumed original trigger receipt.");
            }
            pending = new(active.ActorIndex, contact.Index, contact.Total, contact.TriggerTiming, 32);
        }
        if (step.Unregistered && active.Visible)
        {
            ui = ui.SetFlags(0, 0x1800) with { Menu957 = active.PreviousMenu957, Menu955 = active.PreviousMenu955 };
        }
        return this with { Ui = ui, Widget = step.Unregistered ? null : step.Widget, Random = step.Random, Pending = pending, PendingSound = PendingSound || step.PlayTriggerSound };
    }
}
