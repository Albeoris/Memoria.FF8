using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

internal static class BattleActionKernelReader
{
    public static IReadOnlyList<BattleActionDefinition> Read(byte[] bytes)
    {
        KernelData kernel = KernelData.ReadFromArray(bytes, FF8Encoding.CreateEuropean());
        List<BattleActionDefinition> result = [];
        for (int id = 1; id < kernel.MagicSpells.Count; id++)
        {
            var spell = kernel.MagicSpells[id];
            uint statuses = (uint)(spell.StatusMagic1 | spell.StatusMagic2 << 8 | spell.StatusMagic3 << 16);
            ushort persistent = (ushort)(spell.StatusMagic5 | spell.StatusMagic4 << 8);
            BattleActionEffect? effect = spell.AttackType switch
            {
                2 when spell.SpellPower > 0 => BattleActionEffect.Damage,
                2 => BattleActionEffect.Status,
                3 when spell.SpellPower > 0 => BattleActionEffect.Heal,
                3 => BattleActionEffect.RemoveStatus,
                5 or 6 => BattleActionEffect.Revive,
                12 => BattleActionEffect.Scan,
                32 => BattleActionEffect.FullHeal,
                _ => null
            };
            BattleStatusKind[]? kinds = Statuses(persistent, statuses, effect == BattleActionEffect.RemoveStatus || effect == BattleActionEffect.FullHeal);
            if (effect is null || kinds is null || spell.HitCount != 1 || effect == BattleActionEffect.Status && kinds.Length == 0)
            {
                continue;
            }
            result.Add(new BattleActionDefinition(BattleAbility.Magic, id, spell.Name ?? throw new InvalidDataException("Original spell has no name."), effect.Value, spell.AttackType == 6 ? 1 : spell.SpellPower, (spell.DefaultTarget & 64) == 0, (spell.DefaultTarget & 16) == 0, kinds, spell.DrawResist, (int)spell.Element));
        }
        for (int id = 1; id < kernel.BattleItems.Count; id++)
        {
            var item = kernel.BattleItems[id];
            BattleActionEffect? effect = item.AttackType switch
            {
                4 when item.AttackPower > 0 => BattleActionEffect.Heal,
                4 => BattleActionEffect.RemoveStatus,
                5 => BattleActionEffect.Revive,
                32 => BattleActionEffect.FullHeal,
                2 when item.AttackPower > 0 => BattleActionEffect.Damage,
                2 => BattleActionEffect.Status,
                _ => null
            };
            BattleStatusKind[]? kinds = Statuses(item.Statuses0, item.Statuses1, effect is BattleActionEffect.RemoveStatus or BattleActionEffect.FullHeal);
            if (effect is null || kinds is null || effect == BattleActionEffect.Status && kinds.Length == 0)
            {
                continue;
            }
            result.Add(new BattleActionDefinition(BattleAbility.Item, id, item.Name ?? throw new InvalidDataException("Original battle item has no name."), effect.Value, item.AttackPower, (item.Target & 64) == 0, (item.Target & 16) == 0, kinds, Element: (int)item.Element));
        }
        for (int id = 0; id < 3; id++)
        {
            var guardian = kernel.Guardians[id];
            if (guardian.AttackType != 11 || guardian.Statuses0 != 0 || guardian.Statuses1 != 0)
            {
                throw new NotSupportedException("The original early GF attack contract differs from the supported magic damage effect.");
            }
            result.Add(new BattleActionDefinition(BattleAbility.GuardianForce, id, guardian.AttackName ?? throw new InvalidDataException("Original GF attack has no name."), BattleActionEffect.Damage, guardian.Power, false, true, Element: (int)guardian.SecondaryElement, LevelPowerCoefficient: guardian.LevelModifier, ConstantPower: guardian.PowerModifier));
        }
        return result.AsReadOnly();
    }

    private static BattleStatusKind[]? Statuses(ushort saved, uint battle, bool remove)
    {
        // Native status WORD/DWORD masks; unknown additions fail closed. Removal can safely intersect implemented states.
        if (!remove && ((saved & ~0x1B) != 0 || (battle & ~0x77u) != 0))
        {
            return null;
        }
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
