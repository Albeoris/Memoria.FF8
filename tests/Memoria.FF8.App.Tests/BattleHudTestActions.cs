using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class BattleHudTestActions
{
    internal static NativeKnownMagic KnownMagic { get; } = new(NativeInputConfiguration.OriginalInitHash, [uint.MaxValue, uint.MaxValue]);

    internal static BattleActionDefinition WithNativeRecord(BattleActionDefinition action)
    {
        (int command, int section, int nativeId) = action.Ability switch
        {
            BattleAbility.Magic => (2, 1, action.Id),
            BattleAbility.Item => (4, 7, action.Id),
            BattleAbility.GuardianForce => (254, 2, action.Id + 64),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        byte attackType = (action.Ability, action.Effect) switch
        {
            (BattleAbility.GuardianForce, BattleActionEffect.Damage) => 11,
            (_, BattleActionEffect.Damage) => 2,
            (_, BattleActionEffect.Heal) => 3,
            (_, BattleActionEffect.Revive) => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        BattleNativeAction native = new((byte)command, (ushort)nativeId, section, action.Id)
        {
            Name = action.Name,
            AttackType = attackType,
            Power = (byte)action.Power,
            MagicId = (ushort)action.Id,
            TargetFlags = (byte)((action.Allies ? 0 : 0x40) | (action.AllTargets ? 0 : 0x10)),
            AttackFlags = 1,
            HitCount = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        return action with { NativeAction = native };
    }
}
