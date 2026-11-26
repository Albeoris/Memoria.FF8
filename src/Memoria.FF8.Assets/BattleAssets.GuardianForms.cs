using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public BattleGuardianFormResources ReadGuardianFormResources(BattleGuardianFormDefinition profile, ImmutableArray<BattleGuardianFormActionBinding> actions)
    {
        NativeBattleGraphicsResource visual = ReadEnemyNativeGraphicsResource(profile.ModelId);
        if (!visual.Contains(profile.SupportSequence))
        {
            throw new InvalidDataException($"GF form '{profile.VariantId}' requires model {profile.ModelId} to provide support sequence {profile.SupportSequence}.");
        }
        NativeBattleAiDefinition? prototype = null;
        foreach (BattleGuardianFormActionBinding binding in actions)
        {
            BattleNativeAction native = binding.ResolveNativeAction(NativeActions);
            if (binding.Terminal && !NativeBattleGuardianFormPresentation.SupportsBody(native.MagicId, profile.ModelId))
            {
                throw new NotSupportedException($"GF form '{profile.VariantId}' has no supported presentation for native effect {native.MagicId} with model {profile.ModelId}.");
            }
            if (binding.Target == BattleGuardianFormTarget.Native && native.TargetFlags is null && binding.Source != BattleGuardianFormActionSource.Physical)
            {
                throw new InvalidDataException($"GF form '{profile.VariantId}' action '{binding.Name}' requires an explicit target selector.");
            }
            if (!binding.Terminal && !visual.Contains(binding.Sequence))
            {
                throw new InvalidDataException($"GF form '{profile.VariantId}' action '{binding.Name}' requires model {profile.ModelId} to provide sequence {binding.Sequence}.");
            }
            if (binding.RequirePrototypeBinding)
            {
                prototype ??= ReadEnemyAi(profile.ModelId, 1);
                if (!prototype.Abilities.Any(ability => ability.Command == binding.Command && ability.Id == binding.ResourceActionId && ability.Sequence == binding.Sequence))
                {
                    throw new InvalidDataException($"GF form '{profile.VariantId}' action '{binding.Name}' differs from the native model's action binding.");
                }
            }
        }
        BattleGuardianFormAction basic = profile.TierAtLevel(1).CompelledPhysical ?? throw new InvalidDataException($"GF form '{profile.VariantId}' requires a base physical action.");
        BattleGuardianFormActionBinding attack = actions.Single(binding => binding.Action == basic);
        BattleNativeAction physical = attack.ResolveNativeAction(NativeActions);
        return new(profile.GuardianForceId, physical, attack.Sequence, visual)
        {
            VariantId = profile.VariantId,
            Profile = profile,
            DisplayName = profile.UseInstalledModelName ? ReadEnemyName(profile.ModelId) : profile.UseGuardianForceName ? null : profile.Name,
            InnateBindings = actions,
            CommandBindings = ReadGuardianFormCommandBindings(),
            ElementDefenses = ReadEnemyNativeStats(profile.ModelId).ElementDefenses.Select(value => (int)value).ToImmutableArray(),
            StatusResistances = ReadEnemyNativeStatusData(profile.ModelId).Resistances
        };
    }

    public BattleGuardianFormResources ReadIfritGuardianFormResources()
    {
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(2) ?? throw new NotSupportedException("Ifrit's Guardian Force form profile is unavailable.");
        ImmutableArray<BattleGuardianFormActionBinding> actions = ReadIfritGuardianFormInnateBindings();
        BattleGuardianFormActionBinding attack = actions.Single(binding => binding.Action == BattleGuardianFormAction.Attack);
        BattleNativeAction physical = NativeActions.Single(action => action.Command == 8 && action.ActionId == attack.ResourceActionId);
        return new(profile.GuardianForceId, physical, attack.Sequence, ReadEnemyNativeGraphicsResource(profile.ModelId))
        {
            InnateBindings = actions,
            CommandBindings = ReadGuardianFormCommandBindings(),
            ElementDefenses = ReadEnemyNativeStats(profile.ModelId).ElementDefenses.Select(value => (int)value).ToImmutableArray(),
            StatusResistances = ReadEnemyNativeStatusData(profile.ModelId).Resistances
        };
    }

    public ImmutableArray<BattleNativeCommandBinding> ReadGuardianFormCommandBindings()
    {
        return battleKernel.CommandAbilities.Select(ability => ability.Index).Where(command => command is not (0 or 2 or 3 or 4 or 6)).Distinct().Select(ReadNativeCommand).ToImmutableArray();
    }

    private ImmutableArray<BattleGuardianFormActionBinding> ReadIfritGuardianFormInnateBindings()
    {
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(2) ?? throw new NotSupportedException("Ifrit's Guardian Force form profile is unavailable.");
        NativeBattleAiDefinition source = ReadEnemyAi(profile.ModelId, 1);
        NativeBattleAiAbility fire = source.Abilities[1];
        if (fire.Command != 2 || fire.Id != 1)
        {
            throw new NotSupportedException("Ifrit's original spell slot does not contain his native Fire action.");
        }
        // Slot 2 is self-targeted. Slot 6 is the prototype's second offensive physical action.
        return BattleGuardianFormProfiles.BindIfritInnateActions(
            ReadIfritPhysicalBinding(source.Abilities[0], BattleGuardianFormAction.Attack, "Attack"),
            new(BattleGuardianFormAction.Fire, "Fire", BattleGuardianFormActionSource.Magic, fire.Id, fire.Sequence, BattleGuardianFormActionCost.Innate),
            ReadIfritPhysicalBinding(source.Abilities[6], BattleGuardianFormAction.HeavyStrike, "Heavy Strike"));
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
