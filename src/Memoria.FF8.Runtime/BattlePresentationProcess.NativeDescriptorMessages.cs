namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{


    private static NativeBattleGraphicsState QueueNativeDescriptorMessage(NativeBattleGraphicsState graphics, BattleEvent message, byte tag, byte priority = 128)
    {
        return graphics with { Descriptors = graphics.Descriptors.EnqueueMessage(message, priority, tag).State };
    }

    private NativeBattleGraphicsState PublishNativeDescriptorMessage(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet)
    {
        BattleEvent message = packet.Message ?? throw new NotSupportedException("Native text descriptor lacks its original logical string binding.");
        //5009B0 passes the duration WORD to506EE0's BYTE parameter. The zero-duration task releases before becoming visible.
        byte duration = unchecked((byte)packet.ScalarWord8);
        if (duration != 0)
        {
            BattleEvent published = message with { Frame = Frame, Amount = duration, NativeMessageMode = (byte)packet.Value };
            hostedFrameServices?.EmitDescriptorText(published);
        }
        return graphics;
    }

    private BattleEvent? CreateNativeEntryAdvantageMessage()
    {
        int text = NativeEntryAdvantage switch { 1 => 46, 2 => 45, 3 => 44, 4 => 47, _ => -1 };
        if (text < 0)
        {
            return null;
        }
        if (text >= this.resources.NativeGlobalMessages.Length)
        {
            throw new NotSupportedException("Native entry advantage requires its original kernel miscellaneous text bank.");
        }
        int duration = 8 * (this.resources.NativeMessageSpeed ?? throw new NotSupportedException("Native entry message requires the saved message speed BYTE.")) + 8;
        string message = this.resources.NativeGlobalMessages[text] ?? throw new NotSupportedException("Native entry advantage text is absent from its original kernel bank.");
        return new(Frame, "NativeText", null, null, duration, message) { NativeMessageMode = 3, NativeTextIdentity = BattleTextIdentity.Global(text) };
    }

    private NativeBattleGraphicsState QueueNativeEffectActorText4876F0(NativeBattleGraphicsState graphics, BattleModelOwner owner, int index, int duration)
    {
        NativeBattleGraphicsResource resource = ReadOriginalPresentationModelResource(owner)
            ?? throw new NotSupportedException("Effect text requires its original model text resource.");
        string actorId = ReadGraphicsPresentationId(owner);
        string text = resource.ReadText91(index);
        BattleEvent message = new(Frame, "NativeText", actorId, null, duration, text) { NativeMessageMode = 3 };
        return QueueNativeDescriptorMessage(graphics, message, 0, 240);
    }
}
