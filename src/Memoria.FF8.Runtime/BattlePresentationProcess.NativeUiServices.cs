namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

    private void BeginNativeHostPause()
    {
        if (NativeGraphics?.UiServices is { } initial)
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
    }

    private void CompleteNativeHostPause()
    {
        if (NativeGraphics?.UiServices is { } final)
        {
            bool retained = NativeEffectMemory?.PrimitiveProcess?.DisableTableReset ?? throw new NotSupportedException("Native47CF60 pause completion requires its actual1CFF834 owner.");
            NativeGraphics = NativeGraphics with { UiServices = final with { PauseDE9 = retained } };
        }
    }

    private void AdvanceNativeUiWindowCallbacks(NativeBattleUiServices services, Action publishVitals)
    {
        NativeBattleUiWindows windows = services.Windows ?? throw new NotSupportedException("Native UI callbacks require their retained physical window bank; migrate the named snapshot explicitly.");
        ObserveNativeMegaSpark279UiStack(windows, nativePreCoreUiStack);
        publishVitals();
        services = services with { Windows = windows.AdvanceVitals4B0450().CommitDraw4B9DB0(services.Frame33 == true, services.Flags942) };
        NativeGraphics = NativeGraphics! with { UiServices = services };
        AdvanceNativeTriggerWidget();
        AdvanceNativeGuardianBoost();
        ObserveNativeUiWindowStack(windows, NativeGraphics.UiServices!.Windows!);
    }

    internal void SetNativeActionMenu(bool open)
    {
        if (NativeGraphics?.UiServices is { } ui && NativeGraphics.TriggerWidget is null)
        {
            // TODO(TD-009): The current HUD publishes the list transition without native pixel-opening phases.
            NativeGraphics = NativeGraphics with
            {
                UiServices = open ? ui.SetFlags(0x10) with { Menu957 = 2, Menu955 = -1 } : ui.SetFlags(0, 0x10) with { Menu957 = 4 }
            };
        }
    }

    private NativeBattleGraphicsState PrepareNativeTriggerWidget(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleSequenceCue cue)
    {
        BattleNativeRenzokukenBlock block = cue.TriggerWindow ?? throw new InvalidDataException("Native AB cue lacks its parsed original timing block.");
        NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native AB requires its initialized4A94D0 UI service owner.");
        ushort options = this.resources.TriggerOptions ?? throw new NotSupportedException("Native AB requires its actual saved options WORD1CFE978.");
        NativeBattleTriggerWidget widget = NativeBattleTriggerWidget.Register(owner, block, options, ui, graphics.ActorModels.Roster) with { SourceSequence = checked((byte)cue.First), SourceAbOffset = cue.Second, SourceMagicId = graphics.ActorModels.Get(owner).ResourceMagicId };
        ui = widget.Visible ? ui.SetFlags(0x1800) with { Menu957 = 6, Menu955 = -2 } : ui.SetFlags(0, 0x1800);
        return graphics with { UiServices = ui, TriggerWidget = widget, PendingTriggerSoundActor = null };
    }

    private void AdvanceNativeTriggerWidget()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.TriggerWidget is not { } widget)
        {
            return;
        }
        NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidDataException("Registered widget6 lost its UI callback owner.");
        ushort options = this.resources.TriggerOptions ?? throw new NotSupportedException("Native widget6 requires its saved automatic trigger option.");
        //Host Automatic supplies one actual manual press edge; the original automatic option retains its separate4A7080 draw.
        bool assistance = hostedRenzokukenUiFrame is null && GunbladeMode == BattleGunbladeMode.Automatic && (options & 1) == 0 && widget.Input.TriggerTiming == 0 && widget.Input.Phase is 4 or 6 && widget.Input.PoseClock + widget.Input.SubframeClock - widget.Block.Timeline.InputThresholds[widget.Input.ContactIndex] == 20;
        NativeBattleTriggerWidgetStep step = NativeBattleTriggerWidgetRules.Advance(widget, ui.Frame33 ?? throw new InvalidDataException("Native widget6 was invoked before its4A8E30 BYTE33 producer."), (options & 1) != 0, nativeBattleUiRandom, graphics.ActorModels.Roster, assistedPress: assistance);
        nativeBattleUiRandom = step.Random;
        NativeBattleTriggerReceipt? pending = graphics.PendingTrigger;
        if (step.Contact is { } contact)
        {
            //485130 overwrites the four timing WORDs;485160 consumes that single receipt in the next admitted core step.
            pending = new(widget.Actor, contact.Index, contact.Total, contact.TriggerTiming, 32);
        }
        if (step.Unregistered && widget.Visible)
        {
            ui = ui.SetFlags(0, 0x1800) with { Menu957 = widget.PreviousMenu957, Menu955 = widget.PreviousMenu955 };
        }
        NativeGraphics = graphics with { UiServices = ui, TriggerWidget = step.Unregistered ? null : step.Widget, PendingTrigger = pending, PendingTriggerSoundActor = step.PlayTriggerSound ? widget.Actor : graphics.PendingTriggerSoundActor };
        if (widget.SourceMagicId is not null && hostedRenzokukenUiFrame is not null)
        {
            RecordHostedRenzokukenUi(step, step.Contact is null ? null : pending);
        }
    }
}
