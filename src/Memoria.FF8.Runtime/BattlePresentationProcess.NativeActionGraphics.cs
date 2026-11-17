namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState BeginNativeActionGraphics(NativeBattleGraphicsState graphics, BattleActionHeaderReference reference)
    {
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(nativeActionPackets, reference, graphics.ActionGraphics);
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(reference);
        NativeBattleActionServiceSelection selection = NativeBattleActionGraphics.SelectService(header);
        if (services?.MatchesVisualEntry(header) == true && selection.Callback is not (0x50A9A0 or 0x50B2A0))
        {
            throw new NotSupportedException("Visual presentation gating requires the native casting or GF service entry boundary.");
        }
        nativeActionPackets = nativeActionPackets.ActivateHeader(reference);
        if (selection.Sequence is byte sequence)
        {
            nativeActionPackets = nativeActionPackets.WriteSequence(reference, sequence);
        }
        return graphics with { ActionGraphics = context, Flags = graphics.Flags | selection.SetGraphicsFlags };
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeActionGraphics(NativeBattleGraphicsState graphics, byte phase)
    {
        NativeBattleActionGraphicsState context = graphics.ActionGraphics ?? throw new InvalidDataException("Native104 callback lost its50BF90 action globals.");
        if (context.Callback is 0x50BEE0 or 0x50BDC0)
        {
            return PollNativeDirectGraphics(graphics, context, phase);
        }
        if (context.Callback == 0x50BC20)
        {
            return PollNativeUnsequencedGraphics(graphics, context, phase);
        }
        if (context.Callback == 0x50B830)
        {
            return PollNativeIndependentSummonGraphics(graphics, context, phase);
        }
        if (context.Callback == 0x50BB00)
        {
            return PollNativeOrdinaryGraphics(graphics, context, phase);
        }
        if (context.Callback is 0x50BD00 or 0x50BD80)
        {
            return PollNativeControlGraphics(graphics, context, phase);
        }
        if (context.Callback == 0x50B0C0)
        {
            return PollNativeReflectionGraphics(graphics, context, phase);
        }
        if (context.Callback == 0x50A9A0)
        {
            return PollNativeCastingGraphics(graphics, context, phase);
        }
        if (context.Callback == 0x50B2A0)
        {
            return PollNativeGuardianGraphics(graphics, context, phase);
        }
        if (context.Callback != 0x50B190)
        {
            throw new NotSupportedException($"Native104 selected callback{context.Callback:X}; its original graphical service is not implemented.");
        }
        return PollNativeOriginalGraphics(graphics, context, phase);
    }
}
