using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public BattleGuardianFormResources ReadIfritGuardianFormResources()
    {
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(2) ?? throw new NotSupportedException("Ifrit's Guardian Force form profile is unavailable.");
        BattleGuardianFormAbilityBands bands = ReadIfritGuardianFormAbilityBands();
        BattleGuardianFormActionBinding attack = bands.Bands[0][0];
        BattleNativeAction physical = NativeActions.Single(action => action.Command == 8 && action.ActionId == attack.ResourceActionId);
        return new(profile.GuardianForceId, physical, attack.Sequence, ReadEnemyNativeGraphicsResource(profile.ModelId))
        {
            AbilityBands = bands,
            ElementDefenses = ReadEnemyNativeStats(profile.ModelId).ElementDefenses.Select(value => (int)value).ToImmutableArray(),
            StatusResistances = ReadEnemyNativeStatusData(profile.ModelId).Resistances
        };
    }

    public BattleGuardianFormAbilityBands ReadIfritGuardianFormAbilityBands()
    {
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(2) ?? throw new NotSupportedException("Ifrit's Guardian Force form profile is unavailable.");
        byte[] bytes = Read($"c0m{profile.ModelId:000}.dat");
        int information = EnemyDrawInformation(bytes);
        NativeBattleAiDefinition source = ReadEnemyAi(profile.ModelId, 1);
        var bands = ImmutableArray.CreateBuilder<ImmutableArray<BattleGuardianFormActionBinding>>(3);
        for (int band = 0; band < 3; band++)
        {
            int start = band * 16;
            NativeBattleAiAbility spell = source.Abilities[start + 1];
            if (spell.Command != 2 || spell.Id == 0)
            {
                throw new NotSupportedException("Ifrit's original spell slot does not contain a native magic action.");
            }
            var actions = ImmutableArray.CreateBuilder<BattleGuardianFormActionBinding>(4);
            actions.Add(ReadIfritPhysicalBinding(source.Abilities[start], BattleGuardianFormAction.Attack, "Attack"));
            actions.Add(new(BattleGuardianFormAction.Fire, "Fire", BattleGuardianFormActionSource.Magic, spell.Id, spell.Sequence, BattleGuardianFormActionCost.Innate));
            // Slot 2 is a self-targeted animation. The second offensive physical action is slot 6.
            NativeBattleAiAbility heavy = source.Abilities[start + 6];
            if (heavy is not { Command: 0, Id: 0, Sequence: 0 })
            {
                actions.Add(ReadIfritPhysicalBinding(heavy, BattleGuardianFormAction.HeavyStrike, "Heavy Strike"));
            }
            actions.Add(profile.Finisher);
            bands.Add(actions.ToImmutable());
        }
        return new(bytes[information + 244], bytes[information + 245], bands.MoveToImmutable());
    }

    private BattleGuardianFormActionBinding ReadIfritPhysicalBinding(NativeBattleAiAbility ability, BattleGuardianFormAction action, string name)
    {
        BattleNativeAction? physical = NativeActions.SingleOrDefault(candidate => candidate.Command == 8 && candidate.ActionId == ability.Id);
        if (ability.Command != 8 || physical is not { AttackType: 1, MagicId: 0, HitCount: 1, Element: 0, Statuses0: 0, Statuses1: 0 } || physical.Power != ability.Power)
        {
            throw new NotSupportedException("Ifrit's original physical slot does not contain a supported native attack.");
        }
        return new(action, name, BattleGuardianFormActionSource.Physical, ability.Id, ability.Sequence);
    }
}
