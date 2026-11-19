namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool CanRetainNativeSupportActor(BattleCombatant actor, BattleNativePlayerReaction? reaction)
    {
        return actor.IsAlive || actor.IsActive && IsNativeSupportInvocation(reaction);
    }

    private static bool CanNativeSupportCommandAct(BattleCombatant actor, BattleNativePlayerReaction? reaction)
    {
        return CanNativeBattleActorAct(actor) || actor.IsActive && IsNativeSupportInvocation(reaction);
    }

    private static bool IsNativeSupportInvocation(BattleNativePlayerReaction? reaction)
    {
        return reaction is BattleNativePlayerReaction.AngeloReverse or BattleNativePlayerReaction.AngeloPeriodic or BattleNativePlayerReaction.AutomaticGuardian or BattleNativePlayerReaction.PhoenixRevival;
    }

    private bool NativeAngeloDisabled()
    {
        if ((definition.NativeSavedBattleFlagsKnownMask & 0x10) == 0)
        {
            throw new NotSupportedException("Native Angelo support requires the actual saved1CFE97A bit0x10.");
        }
        return (CurrentNativeSavedBattleFlags() & 0x10) != 0;
    }

    private void RunNativePlayerDamageSupport(BattleCombatant actor)
    {
        if (!HasNativeLimitFamily(actor.Id, BattleLimitFamily.Combine) || actor.LastAttackerId is not { } attackerId || GetActor(attackerId) is not { IsPlayer: false } attacker)
        {
            return;
        }
        byte healing = actor.LastNativeHealing ?? throw new NotSupportedException("Native Angelo damage support requires the actual last healing byte.");
        if (healing != 0 || NativeAngeloDisabled())
        {
            return;
        }
        NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Native Angelo support requires the saved learned flags at1CFE772.");
        if ((progress.AngeloFlags & 2) != 0 && (NativeSavedStatusBits(actor) & 0x100) != 0 && NextRandomByte() <= 16)
        {
            QueueNativePlayerReaction(actor, BattleNativePlayerReaction.AngeloRecover, 240, 12, SelectBattleTarget(actor));
        }
        else if ((progress.AngeloFlags & 1) != 0 && NextRandomByte() <= 16)
        {
            QueueNativePlayerReaction(actor, BattleNativePlayerReaction.AngeloRush, 240, 11, SelectBattleTarget(attacker, replacement: BattleTargetReplacement.UseCurrentBattlePolicy));
        }
    }

    private bool ShouldRunNativePlayerDeathSupport(BattleCombatant actor, BattleCombatant? attacker)
    {
        // NG-005: Original482F10 excludes encounter317.
        if (!HasNativeLimitFamily(actor.Id, BattleLimitFamily.Combine) || (attacker is not { IsPlayer: false }) || definition.EncounterId == 317 || (NativeBattleStatusBits(actor) & 0x10000) != 0)
        {
            return false;
        }
            return (ExpandedAngeloLearned(actor) & 4) != 0 && NextRandomByte() <= 32;
    }

    private void RunNativePlayerDeathSupport(BattleCombatant actor)
    {
        //494410 already admitted482F10;4877F0 stage3 retains the original actor and includes KO targets.
        QueueNativePlayerReaction(actor, BattleNativePlayerReaction.AngeloReverse, 240, 13, SelectBattleTarget(actor, allowsKnockedOutAllies: true));
        SetNativeReactionActionGate(false);
    }
}
