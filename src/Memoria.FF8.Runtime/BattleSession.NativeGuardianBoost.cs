namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGuardianBoost? nativeGuardianBoost;
    public NativeBattleGuardianBoost? NativeGuardianBoost => nativeGuardianBoost;

    public bool PressNativeGuardianBoost()
    {
        if (Outcome != BattleOutcome.Active || nativeGuardianBoost is not { Enabled: true, Phase: 4, PendingPress: false } boost)
        {
            return false;
        }
        nativeGuardianBoost = boost with { PendingPress = true };
        return boost.Window != 0;
    }

    private NativeBattleGraphicsState RegisterNativeGuardianBoost(NativeBattleGraphicsState graphics, ushort actionId)
    {
        nativeGuardianBoost = CreateNativeGuardianBoost(actionId);
        //4B9AD0 overwrites the single callback6 registration, including an earlier trigger callback.
        return graphics with { TriggerWidget = null };
    }

    private NativeBattleGuardianBoost CreateNativeGuardianBoost(ushort actionId)
    {
        if (actionId is < 64 or >= 80)
        {
            throw new InvalidDataException("Native56DCE0 requires its original GF action64..79.");
        }
        BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, actionId - 64);
        BattleNativeAction native = action.NativeAction ?? throw new NotSupportedException("Native56DCE0 requires its authenticated GF kernel row.");
        if (native.KernelSection != 2 || native.KernelIndex != actionId - 64 || native.RawRecord.Length != 132)
        {
            throw new InvalidDataException("Native56DCE0 timer bytes128/129 do not belong to the selected GF.");
        }
        bool enabled = action.NativeGuardianBoostAvailable ?? throw new NotSupportedException("Native56DCE0 requires the selected GF's actual learned Boost flag.");
        if (nativeBattleUiRandom is null)
        {
            throw new NotSupportedException("Native GF Boost requires its actual4A7080 battle UI random owner.");
        }
        return NativeBattleGuardianBoost.Register(native.RawRecord[128], native.RawRecord[129], enabled) with { ActionId = actionId };
    }

    private void AdvanceNativeGuardianBoost()
    {
        if (nativeGuardianBoost is null || NativeGraphics?.UiServices is not { } ui)
        {
            return;
        }
        bool frame = ui.Frame33 ?? throw new InvalidDataException("Native56DD70 was called before its4A8E30 publication producer.");
        AdvanceNativeGuardianBoostWidget(frame, (ui.Flags942 & 1) != 0);
    }

    private void AdvanceNativeGuardianBoostWidget(bool frame, bool inputEnabled)
    {
        NativeBattleGuardianBoost widget = nativeGuardianBoost ?? throw new InvalidOperationException("Native Boost callback has no registered widget owner.");
        NativeBattleGuardianBoostStep step = NativeBattleGuardianBoostRules.Advance(widget, frame, inputEnabled, widget.PendingPress, nativeBattleUiRandom);
        nativeBattleUiRandom = step.Random;
        nativeGuardianBoost = step.Unregister ? null : step.Widget with { PendingPress = false };
        if (step.RecomputePercent is byte percent)
        {
            RecomputeNativeGuardianContacts(percent);
        }
    }

    private void BeginComponentGuardianBoost(BattleActionPresentation action)
    {
        if (NativeGraphics is not null || action.NativeQueuedCommand != 254 || action.NativeHeaderIndex is null)
        {
            return;
        }
        (ushort actionId, BattleActionDefinition selected) = NativeGuardianBoostBinding(action);
        bool enabled = selected.NativeGuardianBoostAvailable ?? throw new NotSupportedException("Native GF presentation requires its actual learned Boost flag; migrate the unknown owner explicitly.");
        if (!enabled)
        {
            return;
        }
        // TODO(TD-009): The component GF windup admits the native Boost widget without the original3D effect-service entry.
        if (nativeGuardianBoost is not null)
        {
            throw new InvalidOperationException("A native Boost callback already owns the battle input widget.");
        }
        nativeGuardianBoost = CreateNativeGuardianBoost(actionId);
    }

    private (ushort ActionId, BattleActionDefinition Action) NativeGuardianBoostBinding(BattleActionPresentation action)
    {
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[0];
        if (action.NativeQueuedCommand != 254 || action.NativeHeaderIndex != 0 || header.ReadByte(1) != 254 || header.ReadByte(0) != GetActor(action.ActorId).NativeIndex)
        {
            throw new InvalidDataException("Native GF Boost requires its admitted actor and original254 header0.");
        }
        ushort id = NativeBattleActionGraphics.Word(header, 4);
        if (id is < 64 or >= 80)
        {
            throw new InvalidDataException("Native GF Boost header does not identify GF64..79.");
        }
        BattleActionDefinition selected = FindAction(BattleAbility.GuardianForce, id - 64);
        if (selected.NativeAction is not { Command: 254 } native || native.ActionId != id)
        {
            throw new InvalidDataException("Native GF Boost header differs from its immutable GF binding.");
        }
        return (id, selected);
    }

    public void AdvanceComponentGuardianBoostPresentation(TimeSpan elapsed, ushort heldButtons = 0)
    {
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(1000))
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        if (NativeGraphics is not null || nativeGuardianBoost is not { } widget)
        {
            return;
        }
        // TODO(TD-009): Map admitted battle presentation callbacks to30Hz; raw widgets continue using actual Frame33/Flags942.
        long ticks = widget.ComponentSubframeTicks + elapsed.Ticks * FramesPerSecond;
        int callbacks = checked((int)(ticks / TimeSpan.TicksPerSecond));
        nativeGuardianBoost = widget with { ComponentSubframeTicks = ticks % TimeSpan.TicksPerSecond };
        for (int callback = 0; callback < callbacks && nativeGuardianBoost is not null; callback++)
        {
            AdvanceNativeGuardianBoostWidget(true, (heldButtons & 0x100) != 0);
        }
    }

    private void RestoreNativeGuardianBoost(NativeBattleGuardianBoost? widget)
    {
        widget?.Validate();
        if (widget is not null)
        {
            if (widget.ActionId is not ushort actionId || actionId is < 64 or >= 80 || nativeBattleUiRandom is null || NativeGraphics?.TriggerWidget is not null || NativeGraphics is not null && widget.ComponentSubframeTicks != 0)
            {
                throw new InvalidDataException("Saved Boost widget differs from its admitted GF action, singleton input or UI random owner.");
            }
            NativeBattleGuardianBoost initial = CreateNativeGuardianBoost(actionId);
            ushort admittedId;
            if (NativeGraphics is { } graphics)
            {
                NativeBattleActionGraphicsState context = graphics.ActionGraphics ?? throw new InvalidDataException("Saved raw Boost widget lacks its original action graphics owner.");
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[context.HeaderIndex];
                admittedId = NativeBattleActionGraphics.Word(header, 4);
                if (context.Callback is not (0x50B2A0 or 0x50A9A0) || context.ActorIndex != header.ReadByte(0) || header.ReadByte(1) != 254 || admittedId is < 64 or >= 80)
                {
                    throw new InvalidDataException("Saved raw Boost widget differs from its physical GF header and action graphics service.");
                }
                BattleNativeAction? binding = FindAction(BattleAbility.GuardianForce, admittedId - 64).NativeAction;
                if (binding is not { Command: 254 } || binding.ActionId != admittedId)
                {
                    throw new InvalidDataException("Saved raw Boost widget differs from its immutable GF binding.");
                }
            }
            else
            {
                BattleActionPresentation action = ActiveAction is { NativeQueuedCommand: 254, NativeHeaderIndex: 0 } active ? active : throw new InvalidDataException("Saved component Boost widget lacks its admitted GF action.");
                (admittedId, _) = NativeGuardianBoostBinding(action);
            }
            bool computed = nativeActionScratch.GuardianFormulaE1E ?? throw new InvalidDataException("Saved Boost widget lacks its actual E1E formula gate.");
            if (widget.Enabled != (widget.Phase == 6 ? false : initial.Enabled) || computed != (widget.Phase == 6) || admittedId != actionId)
            {
                throw new InvalidDataException("Saved Boost widget contradicts its learned flag, phase/E1E gate or physical GF header0.");
            }
        }
        nativeGuardianBoost = widget;
    }
}
