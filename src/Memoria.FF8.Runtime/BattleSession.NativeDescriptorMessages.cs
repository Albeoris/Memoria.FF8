namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueueNativeDescriptorMessage(BattleEvent message, byte tag = 86, byte priority = 128)
    {
        if (NativeGraphics is not { } graphics)
        {
            events.Add(message with { Frame = Frame });
            return;
        }
        NativeGraphics = QueueNativeDescriptorMessage(graphics, message, tag, priority);
    }

    private static NativeBattleGraphicsState QueueNativeDescriptorMessage(NativeBattleGraphicsState graphics, BattleEvent message, byte tag, byte priority = 128)
    {
        return graphics with { Descriptors = graphics.Descriptors.EnqueueMessage(message, priority, tag).State };
    }

    private NativeBattleGraphicsState PublishNativeDescriptorMessage(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet)
    {
        BattleEvent message = packet.Message ?? throw new NotSupportedException("Native text descriptor lacks its original logical string binding.");
        //5009B0 passes the duration WORD to506EE0's BYTE parameter. The zero-duration task releases before becoming visible.
        byte duration = unchecked((byte)packet.Index);
        if (duration != 0)
        {
            events.Add(message with { Frame = Frame, Amount = duration, NativeMessageMode = (byte)packet.Value });
        }
        return graphics;
    }

    private NativeBattleGraphicsState QueueNativeEntryAdvantageMessage(NativeBattleGraphicsState graphics)
    {
        int text = NativeEntryAdvantage switch { 1 => 46, 2 => 45, 3 => 44, 4 => 47, _ => -1 };
        if (text < 0)
        {
            return graphics;
        }
        if (text >= definition.NativeGlobalMessages.Length)
        {
            throw new NotSupportedException("Native entry advantage requires its original kernel miscellaneous text bank.");
        }
        int duration = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native entry message requires the saved message speed BYTE.")) + 8;
        string message = definition.NativeGlobalMessages[text] ?? throw new NotSupportedException("Native entry advantage text is absent from its original kernel bank.");
        return QueueNativeDescriptorMessage(graphics, new(Frame, "NativeText", null, null, duration, message) { NativeMessageMode = 3 }, 86);
    }
}
