namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void DispatchNativeStatusTimers()
    {
        // TODO(TD-009): The declared scaffold battle frame remains the scheduling input; native47CCB0 pause/presentation gates require the native battle scheduler.
        foreach (BattleCombatant actor in combatants.Where(candidate => candidate.IsActive && candidate.NativeIndex is >= 0 and < 7))
        {
            if ((NativeSavedStatusBits(actor) & 5) != 0)
            {
                continue;
            }
            for (int bit = 0; bit < actor.NativeStatusTimers.Length; bit++)
            {
                short timer = actor.NativeStatusTimers[bit];
                if (timer == -1111)
                {
                    continue;
                }
                uint mask = 1u << bit;
                if (timer > 0)
                {
                    uint statuses = NativeBattleStatusBits(actor);
                    int decrement = (statuses & 2) != 0 ? 3 : 2;
                    if ((statuses & 4) != 0)
                    {
                        decrement = 1;
                    }
                    if ((statuses & 8) != 0 ? bit != 3 : (statuses & 1) != 0 && bit != 0)
                    {
                        decrement = 0;
                    }
                    if (bit == 4 && decrement != 0 && timer / decrement % (60 / decrement) == 0)
                    {
                        int amount = actor.MaximumHp / 20;
                        if ((NativeSavedStatusBits(actor) & 0x40) != 0)
                        {
                            if (amount >= actor.CurrentHp)
                            {
                                StoreNativeActionMemory(actor, actor, BattleAbility.EnemyAbility, 0, 4, 0, 3);
                            }
                            ApplyDamage(null, actor, amount, "Regen");
                        }
                        else
                        {
                            ApplyHealing(actor, amount, "Regen");
                        }
                    }
                    actor.NativeStatusTimers[bit] = unchecked((short)(timer - decrement));
                    continue;
                }
                actor.NativeStatusTimers[bit] = -1111;
                actor.NativeStatus1 &= ~mask;
                if (bit == 10)
                {
                    //483470 queues4877F0 callback5 -> command246;48FE20 sets the death bit without a resistance roll.
                    StoreNativeActionMemory(actor, actor, BattleAbility.EnemyAbility, 246, 43, 0, 3);
                    ApplyDamage(null, actor, actor.CurrentHp, "Doom");
                }
                else if (bit == 12)
                {
                    actor.NativeStatus0 |= 4;
                    RemoveNativeStatuses(actor, 0x7A, 0x30E77FF);
                    actor.Atb = 0;
                    Log("Status", actor.Id, actor.Id, 2, $"{actor.Name} is petrified.");
                }
                else
                {
                    Log("NativeStatusExpired", actor.Id, actor.Id, bit + 16, $"Native status {bit + 16} expired.");
                }
                RefreshNativePresentationStatus(actor);
            }
        }
    }

    private void CompleteNativeActionStatuses(BattleCombatant actor)
    {
        //484180 runs after the actor's command, before47DE70 publishes pending counter callbacks.
        if (actor.CurrentHp == 0 || (actor.NativeStatus0 & 3) != 2)
        {
            return;
        }
        int damage = (NextRandomByte() % 5 + 5) * actor.MaximumHp / 100;
        if (damage >= actor.CurrentHp)
        {
            StoreNativeActionMemory(actor, actor, BattleAbility.EnemyAbility, 0, 2, 0, 3);
        }
        ApplyDamage(null, actor, damage, "Poison");
    }

    private static bool IsNativeBattleCapable(BattleCombatant actor)
    {
        return actor.IsActive && (NativeSavedStatusBits(actor) & 5) == 0 && (NativeBattleStatusBits(actor) & 0x10000) == 0;
    }

    internal static bool CanNativeBattleActorAct(BattleCombatant actor)
    {
        return IsNativeBattleCapable(actor) && !actor.Suspended && (NativeBattleStatusBits(actor) & 9) == 0;
    }
}
