namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool CanPresentExpandedEnemyEffect(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction action, int recipientCount)
    {
        return command.Ability == BattleAbility.EnemyAbility && command.NativeSequence == 11 && actor is { IsPlayer: false, ModelId: 99 } &&
            action is { Command: 8, ActionId: 182, MagicId: NativeBattleSonic139Profile.Effect, HitCount: 1 } &&
            recipientCount is >= 1 and <= NativeBattleSonic139Profile.TargetCapacity;
    }
}
