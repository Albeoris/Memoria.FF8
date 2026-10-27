using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

internal static partial class BattleActionKernelReader
{
    public static IReadOnlyList<BattleActionDefinition> Read(byte[] bytes, string language) => ReadCatalogue(bytes, language).Actions;

    public static BattleActionCatalog ReadCatalogue(byte[] bytes, string language)
    {
        KernelData kernel = KernelData.ReadFromArray(bytes, NativeTextEncoding.Create(language));
        BattleNativeAction[] native = ReadNativeActions(bytes, kernel, out BattleNativeKernelRecord[] records);
        List<BattleActionDefinition> result = [];
        for (int id = 1; id < kernel.MagicSpells.Count; id++)
        {
            BattleNativeAction action = native.Single(action => action.Command == 2 && action.KernelIndex == id);
            BattleActionEffect effect = action.AttackType switch
            {
                2 when action.Power > 0 => BattleActionEffect.Damage,
                2 => BattleActionEffect.Status,
                3 when action.Power > 0 => BattleActionEffect.Heal,
                3 => BattleActionEffect.RemoveStatus,
                5 or 6 => BattleActionEffect.Revive,
                12 => BattleActionEffect.Scan,
                32 => BattleActionEffect.FullHeal,
                _ => BattleActionEffect.Native
            };
            result.Add(Wrap(BattleAbility.Magic, id, action, effect, kernel.MagicSpells[id].DrawResist));
        }
        for (int id = 1; id < kernel.BattleItems.Count; id++)
        {
            BattleNativeAction action = native.Single(action => action.Command == 4 && action.KernelIndex == id);
            BattleActionEffect effect = action.AttackType switch
            {
                4 when action.Power > 0 => BattleActionEffect.Heal,
                4 => BattleActionEffect.RemoveStatus,
                5 => BattleActionEffect.Revive,
                32 => BattleActionEffect.FullHeal,
                2 when action.Power > 0 => BattleActionEffect.Damage,
                2 => BattleActionEffect.Status,
                _ => BattleActionEffect.Native
            };
            result.Add(Wrap(BattleAbility.Item, id, action, effect));
        }
        if (kernel.Guardians.Count != 16)
        {
            throw new InvalidDataException("Original summon catalog requires all sixteen junctionable Guardian Forces.");
        }
        for (int id = 0; id < kernel.Guardians.Count; id++)
        {
            BattleNativeAction action = native.Single(action => action.KernelSection == 2 && action.KernelIndex == id);
            BattleActionEffect effect = action.AttackType switch
            {
                11 when action.Power == 0 => BattleActionEffect.Status,
                11 or 15 or 20 or 29 => BattleActionEffect.Damage,
                _ => BattleActionEffect.Native
            };
            result.Add(Wrap(BattleAbility.GuardianForce, id, action, effect));
        }
        foreach (BattleNativeAction action in native.Where(action => action.Command == 8))
        {
            string name = string.IsNullOrWhiteSpace(action.Name) ? $"Enemy attack {action.ActionId}" : action.Name;
            result.Add(new BattleActionDefinition(BattleAbility.EnemyAbility, action.ActionId, name, BattleActionEffect.Native, action.Power, false, Statuses: Statuses(action.Statuses0, action.Statuses1), Element: action.Element) { NativeAttackFlags = action.AttackFlags, NativeAction = action });
        }
        return new BattleActionCatalog(result.AsReadOnly(), Array.AsReadOnly(native), Array.AsReadOnly(records));
    }

    private static BattleActionDefinition Wrap(BattleAbility ability, int id, BattleNativeAction native, BattleActionEffect effect, int drawResist = 0)
    {
        byte target = native.TargetFlags ?? throw new InvalidDataException("Original player action lacks its targeting record.");
        string name = native.Name ?? throw new InvalidDataException("Original player action has no installed display name.");
        int levelModifier = ability == BattleAbility.GuardianForce ? native.LevelModifier ?? throw new InvalidDataException("Original GF action lacks its level modifier.") : 0;
        int powerModifier = ability == BattleAbility.GuardianForce ? native.PowerModifier ?? throw new InvalidDataException("Original GF action lacks its power modifier.") : 0;
        // Native483860/483880 preserve the low-bit random-target modifier alongside side/group selection.
        bool allTargets = (target & 0x30) != 0x10 && (target & 2) == 0;
        return new BattleActionDefinition(ability, id, name, effect, native.Power, (target & 0x40) == 0, allTargets, Statuses(native.Statuses0, native.Statuses1), drawResist, native.Element, levelModifier, powerModifier) { NativeAttackFlags = native.AttackFlags, NativeAction = native };
    }

    private static BattleStatusKind[] Statuses(ushort saved, uint battle)
    {
        // This compatibility view lists existing scaffold enums only. NativeAction retains every original status bit.
        List<BattleStatusKind> result = [];
        if ((saved & 2) != 0)
        {
            result.Add(BattleStatusKind.Poison);
        }
        if ((saved & 8) != 0)
        {
            result.Add(BattleStatusKind.Blind);
        }
        if ((saved & 16) != 0)
        {
            result.Add(BattleStatusKind.Silence);
        }
        foreach ((uint mask, BattleStatusKind kind) in new[] { (1u, BattleStatusKind.Sleep), (2u, BattleStatusKind.Haste), (4u, BattleStatusKind.Slow), (16u, BattleStatusKind.Regen), (32u, BattleStatusKind.Protect), (64u, BattleStatusKind.Shell) })
        {
            if ((battle & mask) != 0)
            {
                result.Add(kind);
            }
        }
        return result.ToArray();
    }
}
