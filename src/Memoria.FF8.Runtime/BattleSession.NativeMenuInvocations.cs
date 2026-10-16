namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static void ValidateNativeMenuInvocation(BattleCombatant actor, BattleAbility ability, int actionId, BattleNativeAction? native, BattleNativeDuelInvocation? duel, BattleNativeSlotsRoll? slots)
    {
        if (duel is not null && (slots is not null || actor.NativeLimit?.Command != 11 || ability != BattleAbility.LimitBreak || actionId != 11 << 16 || duel.CrisisLevel is < 1 or > 4 || native is not null && (native.Command != 11 || native.ActionId != 0 || native.CrisisLevel != duel.CrisisLevel)))
        {
            throw new InvalidDataException("Native Duel invocation contradicts its selected character, crisis or control header.");
        }
        if (slots is not null && (actor.NativeLimit?.Command != 16 || native is not { Command: 16 } || native.ActionId != slots.MagicId || actionId != slots.MagicId || slots.Repetitions is < 1 or > 3))
        {
            throw new InvalidDataException("Native Slots group contradicts its accepted roll or physical target WORDs.");
        }
    }
}
