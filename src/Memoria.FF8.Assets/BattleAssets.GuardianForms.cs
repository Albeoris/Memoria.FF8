using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public BattleGuardianFormResources ReadIfritGuardianFormResources()
    {
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(2) ?? throw new NotSupportedException("Ifrit's Guardian Force form profile is unavailable.");
        NativeBattleAiDefinition source = ReadEnemyAi(profile.ModelId, 1);
        NativeBattleAiAbility ability = source.Abilities[0];
        NativeBattleAiAbility spell = source.Abilities[1];
        BattleGuardianFormActionBinding fire = profile.Binding(BattleGuardianFormAction.Fire);
        if (spell.Command != 2 || spell.Id != fire.ResourceActionId || spell.Sequence != fire.Sequence)
        {
            throw new NotSupportedException("Ifrit's original Fire action does not match its supported form binding.");
        }
        BattleNativeAction physical = NativeActions.Single(action => action.Command == ability.Command && action.ActionId == ability.Id);
        if (ability.Command != 8 || ability.Id != 79 || ability.Sequence != 12 || physical.AttackType != 1 || physical.Power != 14 || physical.Power != ability.Power || physical.MagicId != 0 || physical.HitCount != 1 || physical.Element != 0 || physical.Statuses0 != 0 || physical.Statuses1 != 0)
        {
            throw new NotSupportedException("Ifrit's original physical action does not match its supported form binding.");
        }
        return new(profile.GuardianForceId, physical, ability.Sequence, ReadEnemyNativeGraphicsResource(profile.ModelId))
        {
            ElementDefenses = ReadEnemyNativeStats(profile.ModelId).ElementDefenses.Select(value => (int)value).ToImmutableArray(),
            StatusResistances = ReadEnemyNativeStatusData(profile.ModelId).Resistances
        };
    }
}
