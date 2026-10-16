namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private HashSet<string>? nativeUiBecameReady;
    private bool nativeUiForceGaugeAdvance;

    private bool WaitingForTurnBasedDecision => Mode == BattleAtbMode.TurnBased && !HasPendingTurn && combatants.Any(actor => actor.IsPlayer && actor.IsReady) && !combatants.Any(actor => !actor.IsPlayer && actor.IsReady);

    private HashSet<string> PumpNativeHostFrame(bool forceGauge = false)
    {
        if (NativeGraphics is null || nativeUiBecameReady is not null)
        {
            throw new InvalidOperationException("Native47CF60 requires one nonnested graphical host-frame owner.");
        }
        HashSet<string> ready = [];
        nativeUiBecameReady = ready;
        nativeUiForceGaugeAdvance = forceGauge;
        try
        {
            if (NativeGraphics.UiServices is { } initial)
            {
                bool requestPause = NativeGraphics.EntryReady && (initial.Flags942 & 2) != 0;
                NativeGraphics = NativeGraphics with { UiServices = initial with { PauseDE9 = requestPause && initial.PauseDE9 } };
            }
            for (int pre = 0; pre < 3; pre++)
            {
                DispatchNativeUiService();
            }
            if (NativeGraphics.UiServices?.PauseDE9 != true)
            {
                if (Outcome == BattleOutcome.Active)
                {
                    ready.UnionWith(DispatchFrame(advanceGraphics: false));
                }
                AdvanceNativeGraphicsFrame();
                if (NativeGraphics.UiServices is { } services && (NativeGraphics.Flags & 4) != 0)
                {
                    uint duration = definition.NativeGraphics!.Rules.UiPublicationDuration ?? throw new NotSupportedException("Native500900 publication requires its original B8A3E4 duration DWORD.");
                    NativeGraphics = NativeGraphics with { UiServices = services.QueuePublication(duration) };
                }
            }
            DispatchNativeUiService();
            if (NativeGraphics.UiServices is { } final)
            {
                NativeGraphics = NativeGraphics with { UiServices = final with { PauseDE9 = NativeGraphics.EntryReady && (final.Flags942 & 2) != 0 } };
            }
            return ready;
        }
        finally
        {
            nativeUiBecameReady = null;
            nativeUiForceGaugeAdvance = false;
        }
    }

    private void DispatchNativeUiService()
    {
        if (NativeGraphics is not { UiServices: { } services } graphics)
        {
            return;
        }
        services = services.AdvancePublication();
        NativeGraphics = graphics with { UiServices = services };
        if (services.PauseDE9)
        {
            return;
        }
        (NativeBattleUiCommands commands, uint readyMask, bool selectMenu) = services.Commands.Dispatch(services.ReadyMask);
        if (selectMenu)
        {
            //4BB840 resets the command widget;4ADDB0's mismatched-owner path calls4A7160(0).
            services = services.SetFlags(0, 0x10);
        }
        services = services with { Commands = commands, ReadyMask = readyMask, Menu957 = selectMenu ? (sbyte)4 : services.Menu957 };
        NativeGraphics = NativeGraphics with { UiServices = services };
        if ((services.Flags942 & 0x40) != 0)
        {
            AdvanceNativeTriggerWidget();
            AdvanceNativeGuardianBoost();
        }
        AdvanceNativeUiAtbService();
    }

    private void PublishNativeUiReady(BattleCombatant actor)
    {
        if (NativeGraphics?.UiServices is not { } ui || actor.NativeIndex is < 0 or >= 3 || actor.NativeAtb is not { } gauge || !NativeBattleAtbState.CanAdvance(actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor)) || gauge.Current < gauge.Maximum || (actor.NativeCombatantFlags & 0x1C) != 0 || (NativeSavedStatusBits(actor) & 0x24) != 0 || (NativeBattleStatusBits(actor) & 0x2004009) != 0)
        {
            return;
        }
        //4842B0 queues17 once at the clamp, even if the gauge was already full on entry.
        NativeGraphics = NativeGraphics with { UiServices = ui with { Commands = ui.Commands.Enqueue(checked((byte)actor.NativeIndex), 0x11) } };
        actor.NativeCombatantFlags |= 8;
    }

    private void RemoveNativeUiReady(BattleCombatant actor)
    {
        if (NativeGraphics?.UiServices is { } ui && actor.NativeIndex is >= 0 and < 3)
        {
            //4BB9E0/4ADDB0 submit18 after accepting the menu command;4AD400 consumes it next service.
            NativeGraphics = NativeGraphics with { UiServices = ui with { Commands = ui.Commands.Enqueue(checked((byte)actor.NativeIndex), 0x12) } };
        }
    }

    public void OpenNativeActionMenu()
    {
        if (NativeGraphics?.UiServices is { } ui && NativeGraphics.TriggerWidget is null)
        {
            // TODO(TD-009): The current HUD publishes the opened-list transition without native pixel-opening phases
            //4ADD10/4FDD90 select menu2;4ADDB0/4FDD90's opened-list path calls4A7160(1).
            NativeGraphics = NativeGraphics with { UiServices = ui.SetFlags(0x10) with { Menu957 = 2, Menu955 = -1 } };
        }
    }

    public void CloseNativeActionMenu()
    {
        if (NativeGraphics?.UiServices is { } ui && NativeGraphics.TriggerWidget is null)
        {
            //4BB710/4BB7A0 restore command menu4 and call4A7160(0).
            NativeGraphics = NativeGraphics with { UiServices = ui.SetFlags(0, 0x10) with { Menu957 = 4 } };
        }
    }

    private void RegisterNativeTriggerWidget(NativeBattleGraphicsState graphics, BattleCombatant actor, NativeBattleSequenceCue cue)
    {
        BattleNativeRenzokukenBlock block = cue.TriggerWindow ?? throw new InvalidDataException("Native AB cue lacks its parsed original timing block.");
        NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native AB requires its initialized4A94D0 UI service owner.");
        ushort options = definition.LimitProgress?.Options978 ?? throw new NotSupportedException("Native AB requires its actual saved options WORD1CFE978.");
        NativeBattleTriggerWidget widget = NativeBattleTriggerWidget.Register(checked((byte)actor.NativeIndex), block, options, ui) with { SourceSequence = checked((byte)cue.First), SourceAbOffset = cue.Second, SourceMagicId = graphics.Models[actor.NativeIndex].ResourceMagicId };
        nativeGuardianBoost = null;
        ui = widget.Visible ? ui.SetFlags(0x1800) with { Menu957 = 6, Menu955 = -2 } : ui.SetFlags(0, 0x1800);
        NativeGraphics = graphics with { UiServices = ui, TriggerWidget = widget };
    }

    private void AdvanceNativeTriggerWidget()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.TriggerWidget is not { } widget)
        {
            return;
        }
        NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidDataException("Registered widget6 lost its UI callback owner.");
        ushort options = definition.LimitProgress?.Options978 ?? throw new NotSupportedException("Native widget6 requires its saved automatic trigger option.");
        //Host Automatic supplies one actual manual press edge; the original automatic option retains its separate4A7080 draw.
        bool assistance = nativeRenzokukenProcesses.Count == 0 && GunbladeMode == BattleGunbladeMode.Automatic && (options & 1) == 0 && widget.Input.TriggerTiming == 0 && widget.Input.Phase is 4 or 6 && widget.Input.PoseClock + widget.Input.SubframeClock - widget.Block.Timeline.InputThresholds[widget.Input.ContactIndex] == 20;
        NativeBattleTriggerWidgetStep step = NativeBattleTriggerWidgetRules.Advance(widget, ui.Frame33 ?? throw new InvalidDataException("Native widget6 was invoked before its4A8E30 BYTE33 producer."), (options & 1) != 0, nativeBattleUiRandom, assistedPress: assistance);
        nativeBattleUiRandom = step.Random;
        NativeBattleTriggerReceipt? pending = graphics.PendingTrigger;
        if (step.Contact is { } contact)
        {
            //485130 overwrites the four timing WORDs;485160 consumes that single receipt in the next admitted core step.
            pending = new(widget.ActorIndex, contact.Index, contact.Total, contact.TriggerTiming, 32);
        }
        if (step.Unregistered && widget.Visible)
        {
            ui = ui.SetFlags(0, 0x1800) with { Menu957 = widget.PreviousMenu957, Menu955 = widget.PreviousMenu955 };
        }
        NativeGraphics = graphics with { UiServices = ui, TriggerWidget = step.Unregistered ? null : step.Widget, PendingTrigger = pending };
    }

    private void DispatchNativeTriggerReceipt()
    {
        if (NativeGraphics?.PendingTrigger is not { } receipt)
        {
            return;
        }
        //47CCB0 case4 calls485160 after485460 and before periodic/script callbacks, even while ATB is held.
        RecomputeNativeTriggerContact(receipt);
        NativeGraphics = RequireNativeGraphics() with { PendingTrigger = null };
    }

    private void ValidateNativeTriggerWidget(NativeBattleGraphicsState graphics)
    {
        if (graphics.TriggerWidget is { } widget)
        {
            if (widget.SourceSequence is not byte sequence || widget.SourceAbOffset is not int offset || widget.ClosingPhase == 13 || graphics.ActorIds[widget.ActorIndex] is not string actorId)
            {
                throw new InvalidDataException("Saved native widget lacks its actual registered AB resource binding.");
            }
            NativeBattleGraphicsBinding binding = FindNativeGraphicsBinding(actorId) ?? throw new InvalidDataException("Saved native widget lost its original graphics resource.");
            NativeBattleGraphicsResource resource = widget.SourceMagicId is ushort magic ? definition.NativeRenzokukenResources.Single(entry => entry.MagicId == magic).BindGraphicsResource(binding.Resource.PoseSource ?? throw new InvalidDataException("Saved native widget lost its original skeleton."), binding.Resource.LinkedPoseSource) : binding.Resource;
            int start = resource.SequenceStart(sequence);
            BattleNativeRenzokukenBlock original = NativeBattleRenzokukenRules.ReadBlock(resource.SequenceSection.AsSpan().Slice(start, resource.SequenceEnd(sequence) - start), offset, resource.AnimationFrames);
            if (original.ShowBar != widget.Block.ShowBar || original.EndOffset != widget.Block.EndOffset || !original.Segments.SequenceEqual(widget.Block.Segments))
            {
                throw new InvalidDataException("Saved native widget differs from its reached original AB timing bytes.");
            }
        }
        graphics.PendingTrigger?.Validate();
    }
}
