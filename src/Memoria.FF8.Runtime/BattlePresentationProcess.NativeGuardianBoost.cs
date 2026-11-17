namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGuardianBoost? nativeGuardianBoost;

    private bool PressGuardianBoostWidget()
    {
        if (nativeGuardianBoost is not { Enabled: true, Phase: 4, PendingPress: false } boost)
        {
            return false;
        }
        nativeGuardianBoost = boost with { PendingPress = true };
        return boost.Window != 0;
    }

    private NativeBattleGraphicsState RegisterNativeGuardianBoost(NativeBattleGraphicsState graphics, ushort actionId)
    {
        NativeBattleGuardianBoost widget = CreateNativeGuardianBoost(actionId);
        if (HasBattleEntry)
        {
            BindHostedGuardianBoostWidget(actionId);
        }
        nativeGuardianBoost = widget;
        //4B9AD0 overwrites the single callback6 registration, including an earlier trigger callback.
        return graphics with { TriggerWidget = null };
    }

    private NativeBattleGuardianBoost CreateNativeGuardianBoost(ushort actionId)
    {
        if (actionId is < 64 or >= 80)
        {
            throw new InvalidDataException("Native56DCE0 requires its original GF action64..79.");
        }
        if (!resources.GuardianBoost.TryGetValue(actionId, out var profile))
        {
            throw new NotSupportedException("Native56DCE0 requires its authenticated GF kernel and learned Boost state.");
        }
        if (nativeBattleUiRandom is null)
        {
            throw new NotSupportedException("Native GF Boost requires its actual4A7080 battle UI random owner.");
        }
        return NativeBattleGuardianBoost.Register(profile.First, profile.Second, profile.Enabled) with { ActionId = actionId };
    }

    private void AdvanceNativeGuardianBoost()
    {
        NativeBattleGuardianBoost? widget = HasBattleEntry ? HostedGuardianBoost : nativeGuardianBoost;
        if (widget is null || NativeGraphics?.UiServices is not { } ui)
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
        if (HasBattleEntry && step.RecomputePercent is byte hostedPercent)
        {
            PublishHostedGuardianBoostRequest(hostedPercent);
        }
        nativeBattleUiRandom = step.Random;
        nativeGuardianBoost = step.Unregister ? null : step.Widget with { PendingPress = false };
    }
}
