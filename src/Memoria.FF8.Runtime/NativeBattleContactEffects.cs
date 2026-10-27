namespace Memoria.FF8.Runtime;

public sealed record NativeBattleContactEffectPlan(bool Loaded, bool StageTargetRefresh, bool DeferPetrify, uint? ImmediateStatusFlags, bool EjectionService7, bool SelectSequence, byte Sequence, ushort ContactFlags, int Amount)
{
    public bool Secondary { get; init; }
}

public static class NativeBattleContactEffects
{
    public static NativeBattleContactEffectPlan Plan(NativeBattleContactHalf half, NativeBattleGraphicsModel model, NativeBattleGraphicsRules rules, uint rawActorBattleStatuses, bool secondary = false)
    {
        if (half.ActorIndex != model.Index)
        {
            throw new InvalidDataException("Native506690 contact does not identify its physical graphics row.");
        }
        bool stageTargetRefresh = !secondary && (model.ModelId4 ?? throw new NotSupportedException("Native506690 requires its retained graphics model BYTE4.")) == 140;
        //506690 only enters the secondary half after the primary loaded guard; it does not repeat that guard.
        bool loaded = secondary || (model.Flags0 & 2) != 0 && (model.Flags0 & 0x10) == 0;
        if (!loaded)
        {
            return new(false, stageTargetRefresh, false, null, false, false, half.Sequence, half.ContactFlags, half.PresentedAmount) { Secondary = secondary };
        }
        ushort saved = half.SavedStatuses4;
        bool defer = !secondary && (saved & 4) != 0 && ((model.Flags8 ?? throw new NotSupportedException("Native506690 requires the reached graphics status DWORD8.")) & 0x1A) == 0 && ((model.ResourceFlags44 ?? throw new NotSupportedException("Native506690 requires the reached animation WORD44.")) & 0x140) == 0;
        if (defer)
        {
            saved &= 0xFFFB;
        }
        //506B50 replaces raw core Defend/Charged before50A070/509BA0 maps battle bits to graphics bits.
        uint raw = secondary ? half.BattleStatuses8 : half.BattleStatuses8 ^ (0x880000u & (half.BattleStatuses8 ^ rawActorBattleStatuses));
        uint immediate = rules.Project(saved, raw);
        bool ejection = !secondary && (half.Flags2 & 0x40) != 0;
        bool selectSequence = secondary || !ejection && (half.BattleStatuses8 & 0x10000) == 0;
        return new(true, stageTargetRefresh, defer, immediate, ejection, selectSequence, half.Sequence, half.ContactFlags, half.PresentedAmount) { Secondary = secondary };
    }

    public static NativeBattleEffectsState ProduceTasks(NativeBattleEffectsState effects, NativeBattleContactHalf half, NativeBattleContactEffectPlan plan)
    {
        if (!plan.Loaded)
        {
            return effects;
        }
        if (plan.DeferPetrify)
        {
            effects = effects.AppendStatusReset(half.ActorIndex);
        }
        return effects.PrependDamageText(half.ActorIndex, plan.ContactFlags, plan.Amount);
    }
}
