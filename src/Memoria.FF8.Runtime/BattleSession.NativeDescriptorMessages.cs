namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueueNativeDescriptorMessage(BattleEvent message, byte tag = 86, byte priority = 128)
    {
        if (preparingExpandedHostedAction)
        {
            StageExpandedHostedControl(new HostedDispatchControl.Message(message, tag, priority));
            return;
        }
        StopTurnAdvance();
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

    private NativeBattleGraphicsState QueueNativeEffectActorText4876F0(NativeBattleGraphicsState graphics, BattleModelOwner owner, int text, int duration)
    {
        string actorId;
        NativeBattleGraphicsResource resource;
        BattleTextIdentity? identity = null;
        NativeBattleModelLifecycleServices? services = null;
        if (hostedBattlePresentation)
        {
            services = hostedFrameServices ?? throw new InvalidOperationException("Hosted effect text requires its current frame facts.");
            NativeBattleModelActorFacts facts = RequireHostedContactActorFacts(graphics, services, owner);
            actorId = facts.ActorId;
            resource = ReadOriginalPresentationModelResource(owner) ?? throw new NotSupportedException("Native effect text requires its original actor resource.");
            if (!facts.IsParty)
            {
                identity = BattleTextIdentity.Enemy(facts.OriginalModelId, text);
            }
        }
        else
        {
            actorId = GraphicsActor(graphics, owner).Id;
            resource = FindNativeOriginalGraphicsBinding(actorId)?.Resource ?? throw new NotSupportedException("Native effect text requires its original actor resource.");
        }

        BattleEvent message = new(Frame, "NativeText", actorId, null, duration, resource.ReadText91(text)) { NativeMessageMode = 3, NativeTextIdentity = identity };
        if (services is null)
        {
            StopTurnAdvance();
        }
        NativeBattleGraphicsState queued = QueueNativeDescriptorMessage(graphics, message, 0, 240);
        services?.Emit(new NativeBattleModelTurnAdvanceStopRequested(owner, actorId));
        return queued;
    }

    private NativeBattleGraphicsState PublishNativeDescriptorMessage(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet)
    {
        BattleEvent message = packet.Message ?? throw new NotSupportedException("Native text descriptor lacks its original logical string binding.");
        //5009B0 passes the duration WORD to506EE0's BYTE parameter. The zero-duration task releases before becoming visible.
        byte duration = unchecked((byte)packet.ScalarWord8);
        if (duration != 0)
        {
            BattleEvent published = message with { Frame = Frame, Amount = duration, NativeMessageMode = (byte)packet.Value };
            if (hostedBattlePresentation)
            {
                (hostedFrameServices ?? throw new InvalidOperationException("Hosted native text requires the active frame receipt owner.")).EmitDescriptorText(published);
            }
            else
            {
                events.Add(published);
            }
        }
        return graphics;
    }

    private NativeBattleGraphicsState QueueNativeEntryAdvantageMessage(NativeBattleGraphicsState graphics)
    {
        return CreateNativeEntryAdvantageMessage() is { } message ? QueueNativeDescriptorMessage(graphics, message, 86) : graphics;
    }

    private BattleEvent? CreateNativeEntryAdvantageMessage()
    {
        int text = NativeEntryAdvantage switch { 1 => 46, 2 => 45, 3 => 44, 4 => 47, _ => -1 };
        if (text < 0)
        {
            return null;
        }
        if (text >= definition.NativeGlobalMessages.Length)
        {
            throw new NotSupportedException("Native entry advantage requires its original kernel miscellaneous text bank.");
        }
        int duration = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native entry message requires the saved message speed BYTE.")) + 8;
        string message = definition.NativeGlobalMessages[text] ?? throw new NotSupportedException("Native entry advantage text is absent from its original kernel bank.");
        return new(Frame, "NativeText", null, null, duration, message) { NativeMessageMode = 3, NativeTextIdentity = BattleTextIdentity.Global(text) };
    }
}
