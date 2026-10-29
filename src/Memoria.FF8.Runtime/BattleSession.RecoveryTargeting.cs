namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>Reads removable negative statuses for target selection without resolving an action or consuming random state.</summary>
    public BattleStatusPresentation ReadRemovableRecoveryStatuses(string targetId, BattleActionDefinition action)
    {
        BattleCombatant target = GetActor(targetId);
        ushort saved = NativeSavedStatusBits(target);
        uint battle = NativeBattleStatusBits(target);
        if (action.NativeAction is not { } native)
        {
            if (action.Effect == BattleActionEffect.Revive)
            {
                return new(target.IsAlive ? (ushort)0 : (ushort)1, 0);
            }
            ushort savedMask = 0;
            uint battleMask = 0;
            if (action.Effect == BattleActionEffect.RemoveStatus)
            {
                foreach (BattleStatusKind status in action.Statuses ?? [])
                {
                    if (!target.Effects.Any(effect => effect.Definition.Kind == status))
                    {
                        continue;
                    }
                    savedMask |= status switch { BattleStatusKind.Poison => (ushort)2, BattleStatusKind.Blind => (ushort)8, BattleStatusKind.Silence => (ushort)16, _ => (ushort)0 };
                    battleMask |= status switch { BattleStatusKind.Sleep => 1u, BattleStatusKind.Slow => 4u, _ => 0u };
                }
            }
            return new((ushort)(saved & savedMask), battle & battleMask);
        }
        if (native.AttackType is 5 or 6)
        {
            bool revival = !target.IsAlive && (saved & 0x40) == 0 && definition.NativeFieldBattleRules is byte rules && (rules & 0x40) == 0;
            return new(revival ? (ushort)1 : (ushort)0, 0);
        }
        if (native.AttackType is not (3 or 4 or 21 or 25 or 32) || (native.Power > 0 || native.AttackType == 25) && (saved & 0x40) != 0)
        {
            return default;
        }
        bool bypass = (native.Statuses1 & 0x4000000) != 0;
        bool earthImmune = (native.Element & 8) != 0 && (battle & 0x2000) != 0;
        if (native.AttackType is 3 or 21 && !bypass && ((saved & 1) != 0 || (battle & 0x200000) != 0 || earthImmune))
        {
            return default;
        }
        if (native.AttackType is 4 or 25 or 32 && ((native.Hit ?? 255) <= 1 || native.StatusAttack <= 1))
        {
            return default;
        }
        ushort removableSaved = (ushort)(native.Statuses0 & 0x7F);
        uint removableBattle = native.Statuses1 & ~0x4008000u;
        if (!target.IsPlayer && target.NativeTemplate?.NativeStatusAllocation is { } allocation)
        {
            removableSaved &= (ushort)~(allocation.Saved & 0x40);
            removableBattle &= ~(allocation.Battle & 0x2000);
        }
        for (int bit = 0; bit < 14; bit++)
        {
            if ((target.NativeStatus1 & (1u << bit)) != 0 && target.NativeStatusTimers[bit] == -1111)
            {
                removableBattle &= ~(1u << bit);
            }
        }
        return new((ushort)(saved & removableSaved), battle & removableBattle & 0x40560Du);
    }
}
