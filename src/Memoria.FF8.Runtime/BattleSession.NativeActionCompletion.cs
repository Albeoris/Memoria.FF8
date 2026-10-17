using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void TryCompleteNativeComponentCommand()
    {
        if (NativeGraphics is null && nativeTimedAction is null && ActiveAction?.NativeHeaderIndex is null && !actions.Any(command => command.NativeHeaderIndex is not null) && nativeActionScratch.ComponentCompletion is uint callback)
        {
            nativeActionScratch = nativeActionScratch with { ComponentCompletion = null };
            CompleteNativeActionEvent(callback);
        }
    }

    private bool CompleteNativeActionEvent(uint callback)
    {
        if (callback == 0x47DE70 && ProduceNativePostActionEffects())
        {
            return false;
        }
        if (nativeBattleClock is not null)
        {
            SetNativeBattleActionPause(false);
        }
        FlushNativeReactions();
        nativeActionPackets = nativeActionPackets.ResetCounts();
        foreach (BattleCombatant player in combatants.Where(actor => actor.NativeIndex is >= 0 and < 3 && actor.IsActive))
        {
            ReprojectNativePlayerStatistics(player);
        }
        if (callback is 0x47DE70 or 0x47E120 && nativeActionScratch.EventActorB0F is byte index)
        {
            BattleCombatant actor = FindNativeActor(index);
            actor.Atb = 0;
            actor.NativeCombatantFlags &= ~0xCu;
        }
        RefreshNativeKnownMagicStocks();
        RefreshNativeEnemyDrawSlots();
        return true;
    }

    private bool ProduceNativePostActionEffects()
    {
        byte index = nativeActionScratch.EventActorB0F ?? throw new NotSupportedException("Native484180 requires its original event actor BYTE B0F.");
        BattleCombatant actor = FindNativeActor(index);
        NativeBattleActionProductionContext? previous = nativeProductionContext;
        nativeProductionContext = new(0, nativeActionScratch.EventLane ?? throw new NotSupportedException("Native484180 lacks its original dispatch lane."), false, 0x47E120);
        bool produced = false;
        try
        {
            if ((NativeSavedStatusBits(actor) & 1) == 0 && nativeActionScratch.Command is 28 or 243)
            {
                QueueNativeCommand(actor, 0, 6, 0, checked((ushort)(1 << index)));
                produced = true;
            }
            if ((NativeSavedStatusBits(actor) & 3) == 2)
            {
                QueueNativeCommand(actor, 0, 2, 0, checked((ushort)(1 << index)));
                produced = true;
            }
            if (produced)
            {
                QueueNativeActionClockCompletion(0x47E120);
            }
            return produced;
        }
        finally
        {
            nativeProductionContext = previous;
        }
    }

    private static BattleNativeAction NativePostActionEffect(ushort id)
    {
        return new(0, id, -1, -1) { Name = id == 2 ? "Poison" : "Darkside cost", MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = id == 2 ? (byte)5 : (byte)0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }

    private bool ResolveNativePostActionHit(BattleCombatant actor, BattleCombatant recipient, BattleNativeAction native)
    {
        if (native.Command != 0 || native.ActionId is not (2 or 6))
        {
            return false;
        }
        int damage = native.ActionId == 2 ? (NextRandomByte() % 5 + 5) * recipient.MaximumHp / 100 : recipient.MaximumHp / 10;
        SetNativeContactDamage(damage);
        FinalizeNativeContactPresentation();
        ApplyDamage(actor, recipient, damage, native.Name!, counterEligible: false);
        return true;
    }
}
