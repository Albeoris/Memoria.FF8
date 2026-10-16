using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>4BA560's single registered widget6; contact timing and post-contact close states remain distinct.</summary>
public sealed record NativeBattleTriggerWidget([property: JsonRequired] byte ActorIndex, [property: JsonRequired] BattleNativeRenzokukenBlock Block, [property: JsonRequired] BattleNativeRenzokukenInputState Input, [property: JsonRequired] bool Visible, [property: JsonRequired] bool PendingTrigger, [property: JsonRequired] byte ClosingPhase, [property: JsonRequired] short ClosingRemaining, [property: JsonRequired] sbyte PreviousMenu957, [property: JsonRequired] sbyte? PreviousMenu955)
{
    public byte? SourceSequence { get; init; }
    public int? SourceAbOffset { get; init; }
    public ushort? SourceMagicId { get; init; }

    public void Validate()
    {
        if (ActorIndex >= 7 || Block is null || Block.Timeline is null || Block.Segments.IsDefaultOrEmpty || Input is null || Block.Timeline.InputThresholds.IsDefaultOrEmpty || Input.ContactIndex > Block.Timeline.InputThresholds.Length || Input.SubframeClock is < 0 or > 3 || Input.PoseClock < 0 || Input.PoseClock % 4 != 0 || Input.Phase is not (0 or 1 or 2 or 3 or 4 or 6 or 8) || ClosingPhase is not (0 or 8 or 10 or 12 or 13) || ClosingRemaining is < 0 or > 23 || (ClosingPhase == 0) == (Input.Phase == 8) || (SourceSequence is null) != (SourceAbOffset is null) || SourceAbOffset < 0)
        {
            throw new InvalidDataException("Native trigger widget lacks its parsed AB timeline or retained callback state.");
        }
        BattleNativeRenzokukenTimeline timeline = NativeBattleRenzokukenRules.Timeline(Block.Segments);
        if (timeline.TotalPoseFrames != Block.Timeline.TotalPoseFrames || !timeline.InputThresholds.SequenceEqual(Block.Timeline.InputThresholds))
        {
            throw new InvalidDataException("Native trigger timeline differs from its original AB segment bytes.");
        }
    }

    public static NativeBattleTriggerWidget Register(byte actor, BattleNativeRenzokukenBlock block, ushort options, NativeBattleUiServices ui)
    {
        bool visible = block.ShowBar != 0 && (options & 0x8001) == 0;
        //4BA6C0 case0 only registers phase1; it does not read the retained widgetBYTE33.
        BattleNativeRenzokukenInputStep initial = NativeBattleRenzokukenRules.AdvanceInput(block.Timeline, new(), false, false, (options & 1) != 0, null);
        return new(actor, block, initial.State, visible, false, 0, 0, ui.Menu957, ui.Menu955);
    }
}

public sealed record NativeBattleTriggerWidgetStep(NativeBattleTriggerWidget Widget, BattleNativeRenzokukenContact? Contact, NativeBattleUiRandomState? Random, bool BeginClose, bool Unregistered);
public sealed record NativeBattleTriggerReceipt([property: JsonRequired] byte ActorIndex, [property: JsonRequired] short ContactIndex, [property: JsonRequired] short TotalContacts, [property: JsonRequired] short Timing, [property: JsonRequired] short WordD96)
{
    public void Validate()
    {
        if (ActorIndex >= 7 || ContactIndex < 1 || ContactIndex > TotalContacts || TotalContacts > 16 || Timing != 0 && Timing != 1 && Timing < 20 || WordD96 != 32)
        {
            throw new InvalidDataException("Saved485130 receipt differs from4BA6C0's signed WORD inputs and separate argument32.");
        }
    }
}

public static class NativeBattleTriggerWidgetRules
{
    /// <summary>One actual4B9C80 service invocation, with BYTE33 supplied by4A8E30 rather than an elapsed-time adapter.</summary>
    public static NativeBattleTriggerWidgetStep Advance(NativeBattleTriggerWidget widget, bool frame33, bool automatic, NativeBattleUiRandomState? random, bool assistedPress = false)
    {
        widget.Validate();
        BattleNativeRenzokukenContact? contact = null;
        bool beginClose = false;
        bool unregistered = false;
        if (widget.ClosingPhase == 0)
        {
            BattleNativeRenzokukenInputStep input = NativeBattleRenzokukenRules.AdvanceInput(widget.Block.Timeline, widget.Input, frame33, widget.PendingTrigger || assistedPress, automatic, random);
            random = input.Random;
            contact = input.Contact;
            widget = widget with { Input = input.State, PendingTrigger = false, ClosingPhase = input.State.Phase == 8 ? (byte)8 : (byte)0 };
        }
        else if (widget.ClosingPhase == 8)
        {
            BattleNativeRenzokukenInputState input = widget.Input;
            input = frame33 ? input with { PoseClock = unchecked((short)(input.PoseClock + 4)), SubframeClock = 0 } : input with { SubframeClock = (short)Math.Min(3, input.SubframeClock + 1) };
            widget = widget with { Input = input };
        }
        if (widget.ClosingPhase == 8 && widget.Input.PoseClock >= 4 * widget.Block.Timeline.TotalPoseFrames)
        {
            //4BA6C0 case9 enters case10's first decrement in the same callback.
            widget = widget with { ClosingPhase = 10, ClosingRemaining = 23 };
        }
        else if (widget.ClosingPhase == 10)
        {
            short remaining = (short)(widget.ClosingRemaining - 1);
            if (remaining <= 0)
            {
                widget = widget with { ClosingPhase = 12, ClosingRemaining = 0 };
                beginClose = true;
            }
            else
            {
                widget = widget with { ClosingRemaining = remaining };
            }
        }
        else if (widget.ClosingPhase == 12 && frame33)
        {
            widget = widget with { ClosingPhase = 13 };
            unregistered = true;
        }
        return new(widget, contact, random, beginClose, unregistered);
    }
}
