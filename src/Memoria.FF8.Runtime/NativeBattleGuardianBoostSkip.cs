namespace Memoria.FF8.Runtime;

public static class NativeBattleGuardianBoostSkip
{
    /// <summary>Settles an admitted press before freezing earned Boost and requesting the existing contact recomputation.</summary>
    public static NativeBattleGuardianBoostStep Complete(NativeBattleGuardianBoost widget, bool inputEnabled, NativeBattleUiRandomState? random)
    {
        widget.Validate();
        if (widget.Phase == 6)
        {
            return new(widget with { PendingPress = false }, random, null, false);
        }
        if (widget.PendingPress)
        {
            NativeBattleGuardianBoostStep settled = NativeBattleGuardianBoostRules.Advance(widget, false, inputEnabled, true, random);
            widget = settled.Widget;
            random = settled.Random;
            if (settled.RecomputePercent is not null)
            {
                return settled with { Widget = widget with { PendingPress = false } };
            }
        }
        NativeBattleGuardianBoostStep completed = NativeBattleGuardianBoostRules.Advance(widget with { Phase = 5, PendingPress = false }, false, false, false, random);
        return completed with { Widget = completed.Widget with { PendingPress = false } };
    }
}
