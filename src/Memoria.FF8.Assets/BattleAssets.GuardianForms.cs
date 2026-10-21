using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public BattleGuardianFormResources ReadIfritGuardianFormResources()
    {
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(2) ?? throw new NotSupportedException("Ifrit's Guardian Force form profile is unavailable.");
        NativeBattleAiAbility ability = ReadEnemyAi(profile.ModelId, 1).Abilities[0];
        BattleNativeAction physical = NativeActions.Single(action => action.Command == ability.Command && action.ActionId == ability.Id);
        if (ability.Command != 8 || ability.Id != 79 || ability.Sequence != 12 || physical.AttackType != 1 || physical.Power != 14 || physical.Power != ability.Power || physical.MagicId != 0 || physical.HitCount != 1 || physical.Element != 0 || physical.Statuses0 != 0 || physical.Statuses1 != 0)
        {
            throw new NotSupportedException("Ifrit's original physical action does not match its supported form binding.");
        }
        return new(profile.GuardianForceId, physical, ability.Sequence, ReadEnemyNativeGraphicsResource(profile.ModelId));
    }
}
