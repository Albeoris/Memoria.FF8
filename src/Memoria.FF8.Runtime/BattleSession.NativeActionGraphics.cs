namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState BeginNativeActionGraphics(NativeBattleGraphicsState graphics, uint address)
    {
        uint offset = unchecked(address - 0x1D280C4u);
        if (offset % 20 != 0 || offset / 20 >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new InvalidDataException("Native104 requires its actual physical20-byte action header pointer.");
        }
        int index = (int)(offset / 20);
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(nativeActionPackets, index, graphics.ActionGraphics);
        NativeBattleActionServiceSelection selection = NativeBattleActionGraphics.SelectService(nativeActionPackets.Headers[index]);
        nativeActionPackets = nativeActionPackets.ActivateHeader(index);
        if (selection.Sequence is byte sequence)
        {
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[index];
            nativeActionPackets = nativeActionPackets with { Headers = nativeActionPackets.Headers.SetItem(index, header with { Payload = header.Payload.SetItem(2, sequence), UnknownByteMask = header.UnknownByteMask & ~4u }) };
        }
        return graphics with { ActionGraphics = context, Flags = graphics.Flags | selection.SetGraphicsFlags };
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeActionGraphics(NativeBattleGraphicsState graphics, byte phase)
    {
        NativeBattleActionGraphicsState context = graphics.ActionGraphics ?? throw new InvalidDataException("Native104 callback lost its50BF90 action globals.");
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
        if (context.Callback != 0x50B190)
        {
            throw new NotSupportedException($"Native104 selected callback{context.Callback:X}; its original graphical service is not implemented.");
        }
        return PollNativeOriginalGraphics(graphics, context, phase);
    }
}
