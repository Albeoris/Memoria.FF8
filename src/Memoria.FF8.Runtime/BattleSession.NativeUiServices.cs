namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private HashSet<string>? nativeUiBecameReady;

    private bool WaitingForTurnBasedDecision => Mode == BattleAtbMode.TurnBased && combatants.Any(actor => actor.IsPlayer && actor.IsReady);

    private HashSet<string> PumpNativeHostFrame(bool advanceFrame = true)
    {
        if (NativeGraphics is null || nativeUiBecameReady is not null)
        {
            throw new InvalidOperationException("Native47CF60 requires one nonnested graphical host-frame owner.");
        }
        HashSet<string> ready = [];
        nativeUiBecameReady = ready;
        try
        {
            AdvanceNativeHostTime();
            BeginNativeFrameStack();
            if (NativeGraphics.UiServices is { } initial)
            {
                bool requestPause = (initial.Flags942 & 2) != 0 && (NativeEffectMemory?.ActionPhaseDEA ?? throw new NotSupportedException("Native47CF60 pause admission requires its actual retained process BYTE DEA.")) != 0;
                NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native47CF60 pause transition requires its retained primitive process.");
                NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native47CF60 pause transition requires its retained OT reset gate.");
                bool wasPaused = process.DisableTableReset ?? throw new NotSupportedException("Native47CF60 pause transition requires its actual prior1CFF834.");
                if (!requestPause && wasPaused)
                {
                    byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native5003A0 requires its retained OT bank selector.");
                    NativeBattlePrimitiveFrame frame = NativeGraphics.PrimitiveFrame ?? throw new NotSupportedException("Native5003A0 requires its retained physical OT roots.");
                    NativeGraphics = NativeGraphics with { PrimitiveFrame = frame with { Pending = frame.Pending.SelectTable(buffer).ClearRoots45D550() } };
                    process = process with { TablePerspective = process.TablePerspective.SetItem(buffer, true) };
                }
                NativeEffectMemory = memory with { PrimitiveProcess = process with { DisableTableReset = requestPause } };
                NativeGraphics = NativeGraphics with { UiServices = initial with { PauseDE9 = requestPause && initial.PauseDE9 } };
            }
            for (int pre = 0; pre < 3; pre++)
            {
                DispatchNativeUiService();
            }
            nativePreCoreUiStack = false;
            if (NativeGraphics.UiServices?.PauseDE9 != true)
            {
                if (Outcome == BattleOutcome.Active)
                {
                    ready.UnionWith(DispatchFrame(advanceGraphics: false, advanceFrame: advanceFrame));
                }
                if ((Outcome is BattleOutcome.Victory or BattleOutcome.Defeat) && DebugOutcome is null && NativeGraphics.NativeOutcome?.FadeDurationB0C == 0)
                {
                    return ready;
                }
                //47CCB0 calls48D0C0 once after outer action/AI producers and immediately before500900.
                PumpNativeFileIo482590();
                AdvanceNativeGraphicsFrame();
                AdvanceNativeOutcomeCountdown47CCB0();
                if (NativeGraphics.UiServices is { } services && (NativeGraphics.Flags & 4) != 0)
                {
                    uint duration = definition.NativeGraphics!.Rules.UiPublicationDuration ?? throw new NotSupportedException("Native500900 publication requires its original B8A3E4 duration DWORD.");
                    NativeGraphics = NativeGraphics with { UiServices = services.QueuePublication(duration) };
                }
            }
            DispatchNativeUiService();
            AdvanceNativeScanText537A30();
            //534560's538200 input publication follows the ordinary4A84E0 service polls.
            PublishNativeScanInputFrame();
            if (NativeGraphics.UiServices is { } final)
            {
                bool retained = NativeEffectMemory?.PrimitiveProcess?.DisableTableReset ?? throw new NotSupportedException("Native47CF60 pause completion requires its actual1CFF834 owner.");
                NativeGraphics = NativeGraphics with { UiServices = final with { PauseDE9 = retained } };
            }
            return ready;
        }
        finally
        {
            EndNativeFrameStack();
            nativeUiBecameReady = null;
        }
    }

    private void DispatchNativeUiService()
    {
        if (NativeGraphics is not { UiServices: { } services } graphics)
        {
            return;
        }
        services = services.AdvancePublication();
        services = services.ApplySelect4A8C50();
        //4A84E0 calls4A8C50 before49E9C0 replaces its mapped input WORD16.
        PublishNativeScanInputFrame();
        if (NativeEffectMemory?.Scan is { } scan)
        {
            uint options = definition.NativeSavedOptions ?? throw new NotSupportedException("Native4A84E0 input requires its saved mapping options.");
            ushort held = NativeInputConfiguration.Project(scan.Input.Input.Read49ED30(0, 0), options, definition.NativeInputButtonMap);
            services = services with { HeldButtons16 = held };
        }
        NativeGraphics = graphics with { UiServices = services };
        if (services.PauseDE9)
        {
            return;
        }
        (NativeBattleUiCommands commands, uint readyMask, bool selectMenu) = services.Commands.Dispatch(services.ReadyMask, mask => services = services.SetFlags(0, mask), command => services = services.ApplyWindowCommand4AD400(command));
        services = ApplyNativeReadyMenuSelection(services, selectMenu, pendingSlotsDeal is not null) with { Commands = commands, ReadyMask = readyMask };
        NativeGraphics = NativeGraphics with { UiServices = services };
        if (selectMenu && SelectedPlayerCommandActorId is { } selectedActor)
        {
            SelectPlayerCommandActor(selectedActor);
        }
        if ((services.Flags942 & 0x40) != 0)
        {
            NativeBattleUiWindows windows = services.Windows ?? throw new NotSupportedException("Native UI callbacks require their retained physical window bank; migrate the named snapshot explicitly.");
            ObserveNativeMegaSpark279UiStack(windows, nativePreCoreUiStack);
            AdvanceNativePresentedVitals4B0500();
            services = services with { Windows = windows.AdvanceVitals4B0450().CommitDraw4B9DB0(services.Frame33 == true, services.Flags942) };
            NativeGraphics = NativeGraphics with { UiServices = services };
            AdvanceNativeTriggerWidget();
            AdvanceNativeGuardianBoost();
            ObserveNativeUiWindowStack(windows, NativeGraphics.UiServices!.Windows!);
        }
        if ((NativeGraphics.UiServices!.Flags942 & 0x400) != 0)
        {
            ObserveNativeUnknownStackCallback(0x4B19A0, "UI flag400 callback");
        }
        AdvanceNativeUiAtbService();
        services = NativeGraphics.UiServices!;
        if ((services.Flags942 & 8) != 0)
        {
            ObserveNativeUnknownStackCallback(0x4AD620, "UI window-open command");
            services = services with { Commands = services.Commands.Enqueue(0, 80) };
            NativeGraphics = NativeGraphics with { UiServices = services.SetFlags(0, 8) };
        }
    }

    internal static NativeBattleUiServices ApplyNativeReadyMenuSelection(NativeBattleUiServices services, bool selectMenu, bool slotsPending)
    {
        if (selectMenu && !slotsPending)
        {
            //4BB840 resets the command widget;4ADDB0's mismatched-owner path calls4A7160(0).
            return services.SetFlags(0, 0x10) with { Menu957 = 4 };
        }
        return services;
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
            // TODO(TD-009): The current HUD publishes the opened-list transition without native pixel-opening phases.
            //4ADD10/4FDD90 select menu2;4ADDB0/4FDD90's opened-list path calls4A7160(1).
            NativeGraphics = NativeGraphics with { UiServices = ui.SetFlags(0x10) with { Menu957 = 2, Menu955 = -1 } };
        }
    }

    public void CloseNativeActionMenu()
    {
        RequireNoPendingSlotsDeal();
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
        NativeGraphics = graphics with { UiServices = ui, TriggerWidget = widget, PendingTriggerSoundActorIndex = null };
        MirrorNativeRenzokukenWidget(widget);
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
        NativeGraphics = graphics with { UiServices = ui, TriggerWidget = step.Unregistered ? null : step.Widget, PendingTrigger = pending, PendingTriggerSoundActorIndex = step.PlayTriggerSound ? widget.ActorIndex : graphics.PendingTriggerSoundActorIndex };
        MirrorNativeRenzokukenWidget(step.Widget);
    }

    private void DispatchNativeTriggerReceipt()
    {
        if (NativeGraphics?.PendingTrigger is not { } receipt)
        {
            return;
        }
        ObserveNativeUnknownStackCallback(0x485160, "trigger receipt");
        //47CCB0 case4 calls485160 after485460 and before periodic/script callbacks, even while ATB is held.
        if (nativeTimedAction?.Command.NativeRenzokuken is not null && nativeRenzokukenProcesses.Count != 0)
        {
            CommitNativeRawRenzokukenTrigger(receipt);
        }
        else
        {
            RecomputeNativeTriggerContact(receipt);
        }
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
