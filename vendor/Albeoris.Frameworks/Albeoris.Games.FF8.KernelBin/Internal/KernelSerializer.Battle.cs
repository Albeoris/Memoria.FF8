using Albeoris.Games.FF8.KernelBin.Abstractions;
using Albeoris.Games.FF8.KernelBin.Internal.Raw;

namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Maps between the public model types and the raw binary sections that describe battle
/// commands, magic, Guardian Forces, enemy attacks, weapons, battle/field items and their
/// related sections.
/// </summary>
internal static partial class KernelSerializer
{
    public static List<BattleCommand> ReadBattleCommands(BattleCommandRaw[] raw, KernelTextBlobReader text)
    {
        List<BattleCommand> result = new List<BattleCommand>(raw.Length);
        foreach (BattleCommandRaw item in raw)
        {
            BattleCommand command = new();
            command.Name = text.ReadString(item.OffsetName);
            command.Description = text.ReadString(item.OffsetDescription);
            command.AbilityId = item.AbilityId;
            command.Target = item.Target;
            command.Unknown1 = item.Unknown1;
            command.Unknown2 = item.Unknown2;
            result.Add(command);
        }

        return result;
    }

    public static BattleCommandRaw[] WriteBattleCommands(List<BattleCommand> commands, KernelTextBlobWriter text)
    {
        BattleCommandRaw[] result = new BattleCommandRaw[commands.Count];
        for (Int32 i = 0; i < commands.Count; i++)
        {
            BattleCommand command = commands[i];
            BattleCommandRaw raw = new();
            raw.OffsetName = text.Write(command.Name);
            raw.OffsetDescription = text.Write(command.Description);
            raw.AbilityId = command.AbilityId;
            raw.Target = command.Target;
            raw.Unknown1 = command.Unknown1;
            raw.Unknown2 = command.Unknown2;
            result[i] = raw;
        }

        return result;
    }

    public static List<MagicSpell> ReadMagicSpells(MagicRaw[] raw, KernelTextBlobReader text)
    {
        List<MagicSpell> result = new List<MagicSpell>(raw.Length);
        foreach (MagicRaw item in raw)
        {
            MagicSpell spell = new();
            spell.Name = text.ReadString(item.OffsetName);
            spell.Description = text.ReadString(item.OffsetDescription);
            spell.MagicId = item.MagicId;
            spell.Unknown1 = item.Unknown1;
            spell.AttackType = item.AttackType;
            spell.SpellPower = item.SpellPower;
            spell.Unknown2 = item.Unknown2;
            spell.DefaultTarget = item.DefaultTarget;
            spell.Flags = item.Flags;
            spell.DrawResist = item.DrawResist;
            spell.HitCount = item.HitCount;
            spell.Element = item.Element;
            spell.Unknown4 = item.Unknown4;
            spell.StatusMagic1 = item.StatusMagic1;
            spell.StatusMagic2 = item.StatusMagic2;
            spell.StatusMagic3 = item.StatusMagic3;
            spell.StatusMagic4 = item.StatusMagic4;
            spell.StatusMagic5 = item.StatusMagic5;
            spell.Unknown5 = item.Unknown5;
            spell.StatusAttack = item.StatusAttack;
            spell.HP = item.HP;
            spell.STR = item.STR;
            spell.VIT = item.VIT;
            spell.MAG = item.MAG;
            spell.SPR = item.SPR;
            spell.SPD = item.SPD;
            spell.EVA = item.EVA;
            spell.HIT = item.HIT;
            spell.LUCK = item.LUCK;
            spell.ElemAttackEnabled = item.ElemAttackEnabled;
            spell.ElemAttackValue = item.ElemAttackValue;
            spell.ElemDefenseEnabled = item.ElemDefenseEnabled;
            spell.ElemDefenseValue = item.ElemDefenseValue;
            spell.StatusAttackValue = item.StatusAttackValue;
            spell.StatusDefenseValue = item.StatusDefenseValue;
            spell.StatusAttackEnabled = item.StatusAttackEnabled;
            spell.StatusDefenseEnabled = item.StatusDefenseEnabled;
            spell.QuezacoltCompatibility = item.QuezacoltCompatibility;
            spell.ShivaCompatibility = item.ShivaCompatibility;
            spell.IfritCompatibility = item.IfritCompatibility;
            spell.SirenCompatibility = item.SirenCompatibility;
            spell.BrothersCompatibility = item.BrothersCompatibility;
            spell.DiablosCompatibility = item.DiablosCompatibility;
            spell.CarbuncleCompatibility = item.CarbuncleCompatibility;
            spell.LeviathanCompatibility = item.LeviathanCompatibility;
            spell.PandemonaCompatibility = item.PandemonaCompatibility;
            spell.CerberusCompatibility = item.CerberusCompatibility;
            spell.AlexanderCompatibility = item.AlexanderCompatibility;
            spell.DoomtrainCompatibility = item.DoomtrainCompatibility;
            spell.BahamutCompatibility = item.BahamutCompatibility;
            spell.CactuarCompatibility = item.CactuarCompatibility;
            spell.TonberryCompatibility = item.TonberryCompatibility;
            spell.EdenCompatibility = item.EdenCompatibility;
            spell.Unknown6 = item.Unknown6;
            result.Add(spell);
        }

        return result;
    }

    public static MagicRaw[] WriteMagicSpells(List<MagicSpell> spells, KernelTextBlobWriter text)
    {
        MagicRaw[] result = new MagicRaw[spells.Count];
        for (Int32 i = 0; i < spells.Count; i++)
        {
            MagicSpell spell = spells[i];
            MagicRaw raw = new();
            raw.OffsetName = text.Write(spell.Name);
            raw.OffsetDescription = text.Write(spell.Description);
            raw.MagicId = spell.MagicId;
            raw.Unknown1 = spell.Unknown1;
            raw.AttackType = spell.AttackType;
            raw.SpellPower = spell.SpellPower;
            raw.Unknown2 = spell.Unknown2;
            raw.DefaultTarget = spell.DefaultTarget;
            raw.Flags = spell.Flags;
            raw.DrawResist = spell.DrawResist;
            raw.HitCount = spell.HitCount;
            raw.Element = spell.Element;
            raw.Unknown4 = spell.Unknown4;
            raw.StatusMagic1 = spell.StatusMagic1;
            raw.StatusMagic2 = spell.StatusMagic2;
            raw.StatusMagic3 = spell.StatusMagic3;
            raw.StatusMagic4 = spell.StatusMagic4;
            raw.StatusMagic5 = spell.StatusMagic5;
            raw.Unknown5 = spell.Unknown5;
            raw.StatusAttack = spell.StatusAttack;
            raw.HP = spell.HP;
            raw.STR = spell.STR;
            raw.VIT = spell.VIT;
            raw.MAG = spell.MAG;
            raw.SPR = spell.SPR;
            raw.SPD = spell.SPD;
            raw.EVA = spell.EVA;
            raw.HIT = spell.HIT;
            raw.LUCK = spell.LUCK;
            raw.ElemAttackEnabled = spell.ElemAttackEnabled;
            raw.ElemAttackValue = spell.ElemAttackValue;
            raw.ElemDefenseEnabled = spell.ElemDefenseEnabled;
            raw.ElemDefenseValue = spell.ElemDefenseValue;
            raw.StatusAttackValue = spell.StatusAttackValue;
            raw.StatusDefenseValue = spell.StatusDefenseValue;
            raw.StatusAttackEnabled = spell.StatusAttackEnabled;
            raw.StatusDefenseEnabled = spell.StatusDefenseEnabled;
            raw.QuezacoltCompatibility = spell.QuezacoltCompatibility;
            raw.ShivaCompatibility = spell.ShivaCompatibility;
            raw.IfritCompatibility = spell.IfritCompatibility;
            raw.SirenCompatibility = spell.SirenCompatibility;
            raw.BrothersCompatibility = spell.BrothersCompatibility;
            raw.DiablosCompatibility = spell.DiablosCompatibility;
            raw.CarbuncleCompatibility = spell.CarbuncleCompatibility;
            raw.LeviathanCompatibility = spell.LeviathanCompatibility;
            raw.PandemonaCompatibility = spell.PandemonaCompatibility;
            raw.CerberusCompatibility = spell.CerberusCompatibility;
            raw.AlexanderCompatibility = spell.AlexanderCompatibility;
            raw.DoomtrainCompatibility = spell.DoomtrainCompatibility;
            raw.BahamutCompatibility = spell.BahamutCompatibility;
            raw.CactuarCompatibility = spell.CactuarCompatibility;
            raw.TonberryCompatibility = spell.TonberryCompatibility;
            raw.EdenCompatibility = spell.EdenCompatibility;
            raw.Unknown6 = spell.Unknown6;
            result[i] = raw;
        }

        return result;
    }

    public static List<Guardian> ReadGuardians(GuardianRaw[] raw, KernelTextBlobReader text)
    {
        List<Guardian> result = new List<Guardian>(raw.Length);
        foreach (GuardianRaw item in raw)
        {
            Guardian guardian = new();
            guardian.AttackName = text.ReadString(unchecked((UInt16)item.OffsetAttackName));
            guardian.AttackDescription = text.ReadString(unchecked((UInt16)item.OffsetAttackDescription));
            guardian.MagicId = item.MagicId;
            guardian.AttackType = item.AttackType;
            guardian.Power = item.Power;
            guardian.Flags = item.Flags;
            guardian.AttackElement = item.AttackElement;
            guardian.AttackFlags = item.AttackFlags;
            guardian.Unknown2 = item.Unknown2;
            guardian.Unknown3 = item.Unknown3;
            guardian.SecondaryElement = item.SecondaryElement;
            guardian.Statuses0 = item.Statuses0;
            guardian.Statuses1 = item.Statuses1;
            guardian.HpModifier = item.HpModifier;
            guardian.Unknown4 = item.Unknown4;
            guardian.Unknown5 = item.Unknown5;
            guardian.Unknown6 = item.Unknown6;
            guardian.ExpPerLevel = item.ExpPerLevel;
            guardian.Unknown7 = item.Unknown7;
            guardian.Unknown8 = item.Unknown8;
            guardian.StatusAttack = item.StatusAttack;

            GuardianRaw item2 = item;
            Int32[] abilities = new Int32[21];
            Byte[] magicCompatibility = new Byte[16];
            unsafe
            {
                for (Int32 i = 0; i < abilities.Length; i++)
                    abilities[i] = item2.Abilities[i];
                for (Int32 i = 0; i < magicCompatibility.Length; i++)
                    magicCompatibility[i] = item2.MagicCompatibility[i];
            }

            guardian.Abilities = abilities;
            guardian.MagicCompatibility = magicCompatibility;

            guardian.Unknown9 = item.Unknown9;
            guardian.Unknown10 = item.Unknown10;
            guardian.PowerModifier = item.PowerModifier;
            guardian.LevelModifier = item.LevelModifier;
            result.Add(guardian);
        }

        return result;
    }

    public static GuardianRaw[] WriteGuardians(List<Guardian> guardians, KernelTextBlobWriter text)
    {
        GuardianRaw[] result = new GuardianRaw[guardians.Count];
        for (Int32 i = 0; i < guardians.Count; i++)
        {
            Guardian guardian = guardians[i];
            GuardianRaw raw = new();
            raw.OffsetAttackName = unchecked((Int16)text.Write(guardian.AttackName));
            raw.OffsetAttackDescription = unchecked((Int16)text.Write(guardian.AttackDescription));
            raw.MagicId = guardian.MagicId;
            raw.AttackType = guardian.AttackType;
            raw.Power = guardian.Power;
            raw.Flags = guardian.Flags;
            raw.AttackElement = guardian.AttackElement;
            raw.AttackFlags = guardian.AttackFlags;
            raw.Unknown2 = guardian.Unknown2;
            raw.Unknown3 = guardian.Unknown3;
            raw.SecondaryElement = guardian.SecondaryElement;
            raw.Statuses0 = guardian.Statuses0;
            raw.Statuses1 = guardian.Statuses1;
            raw.HpModifier = guardian.HpModifier;
            raw.Unknown4 = guardian.Unknown4;
            raw.Unknown5 = guardian.Unknown5;
            raw.Unknown6 = guardian.Unknown6;
            raw.ExpPerLevel = guardian.ExpPerLevel;
            raw.Unknown7 = guardian.Unknown7;
            raw.Unknown8 = guardian.Unknown8;
            raw.StatusAttack = guardian.StatusAttack;

            if (guardian.Abilities.Length != 21)
                throw new InvalidOperationException($"Guardian.Abilities must contain exactly 21 entries, but found {guardian.Abilities.Length}.");
            if (guardian.MagicCompatibility.Length != 16)
                throw new InvalidOperationException($"Guardian.MagicCompatibility must contain exactly 16 entries, but found {guardian.MagicCompatibility.Length}.");

            unsafe
            {
                for (Int32 j = 0; j < guardian.Abilities.Length; j++)
                    raw.Abilities[j] = guardian.Abilities[j];
                for (Int32 j = 0; j < guardian.MagicCompatibility.Length; j++)
                    raw.MagicCompatibility[j] = guardian.MagicCompatibility[j];
            }

            raw.Unknown9 = guardian.Unknown9;
            raw.Unknown10 = guardian.Unknown10;
            raw.PowerModifier = guardian.PowerModifier;
            raw.LevelModifier = guardian.LevelModifier;
            result[i] = raw;
        }

        return result;
    }

    public static List<EnemyAttack> ReadEnemyAttacks(EnemyAttackRaw[] raw, KernelTextBlobReader text)
    {
        List<EnemyAttack> result = new List<EnemyAttack>(raw.Length);
        foreach (EnemyAttackRaw item in raw)
        {
            EnemyAttack attack = new();
            attack.Name = text.ReadString(item.OffsetName);
            attack.MagicId = item.MagicId;
            attack.CameraChange = item.CameraChange;
            attack.Unknown0 = item.Unknown0;
            attack.AttackType = item.AttackType;
            attack.AttackPower = item.AttackPower;
            attack.AttackFlags = item.AttackFlags;
            attack.Unknown1 = item.Unknown1;
            attack.Element = item.Element;
            attack.Unknown2 = item.Unknown2;
            attack.StatusAttack = item.StatusAttack;
            attack.AttackParam = item.AttackParam;
            attack.Statuses0 = item.Statuses0;
            attack.Statuses1 = item.Statuses1;
            result.Add(attack);
        }

        return result;
    }

    public static EnemyAttackRaw[] WriteEnemyAttacks(List<EnemyAttack> attacks, KernelTextBlobWriter text)
    {
        EnemyAttackRaw[] result = new EnemyAttackRaw[attacks.Count];
        for (Int32 i = 0; i < attacks.Count; i++)
        {
            EnemyAttack attack = attacks[i];
            EnemyAttackRaw raw = new();
            raw.OffsetName = text.Write(attack.Name);
            raw.MagicId = attack.MagicId;
            raw.CameraChange = attack.CameraChange;
            raw.Unknown0 = attack.Unknown0;
            raw.AttackType = attack.AttackType;
            raw.AttackPower = attack.AttackPower;
            raw.AttackFlags = attack.AttackFlags;
            raw.Unknown1 = attack.Unknown1;
            raw.Element = attack.Element;
            raw.Unknown2 = attack.Unknown2;
            raw.StatusAttack = attack.StatusAttack;
            raw.AttackParam = attack.AttackParam;
            raw.Statuses0 = attack.Statuses0;
            raw.Statuses1 = attack.Statuses1;
            result[i] = raw;
        }

        return result;
    }

    public static List<Weapon> ReadWeapons(WeaponRaw[] raw, KernelTextBlobReader text)
    {
        List<Weapon> result = new List<Weapon>(raw.Length);
        foreach (WeaponRaw item in raw)
        {
            Weapon weapon = new();
            weapon.Name = text.ReadString(item.OffsetName);
            weapon.RenzokukenFinishers = item.RenzokukenFinishers;
            weapon.CharacterId = item.CharacterId;
            weapon.AttackType = item.AttackType;
            weapon.AttackPower = item.AttackPower;
            weapon.AttackParam = item.AttackParam;
            weapon.StrBonus = item.StrBonus;
            weapon.Tier = item.Tier;
            weapon.CritBonus = item.CritBonus;
            weapon.Melee = item.Melee;
            weapon.Unknown0 = item.Unknown0;
            result.Add(weapon);
        }

        return result;
    }

    public static WeaponRaw[] WriteWeapons(List<Weapon> weapons, KernelTextBlobWriter text)
    {
        WeaponRaw[] result = new WeaponRaw[weapons.Count];
        for (Int32 i = 0; i < weapons.Count; i++)
        {
            Weapon weapon = weapons[i];
            WeaponRaw raw = new();
            raw.OffsetName = text.Write(weapon.Name);
            raw.RenzokukenFinishers = weapon.RenzokukenFinishers;
            raw.CharacterId = weapon.CharacterId;
            raw.AttackType = weapon.AttackType;
            raw.AttackPower = weapon.AttackPower;
            raw.AttackParam = weapon.AttackParam;
            raw.StrBonus = weapon.StrBonus;
            raw.Tier = weapon.Tier;
            raw.CritBonus = weapon.CritBonus;
            raw.Melee = weapon.Melee;
            raw.Unknown0 = weapon.Unknown0;
            result[i] = raw;
        }

        return result;
    }

    public static List<BattleItem> ReadBattleItems(BattleItemRaw[] raw, KernelTextBlobReader text)
    {
        List<BattleItem> result = new List<BattleItem>(raw.Length);
        foreach (BattleItemRaw item in raw)
        {
            BattleItem battleItem = new();
            battleItem.Name = text.ReadString(item.OffsetName);
            battleItem.Description = text.ReadString(item.OffsetDescription);
            battleItem.MagicId = item.MagicId;
            battleItem.AttackType = item.AttackType;
            battleItem.AttackPower = item.AttackPower;
            battleItem.Unknown0 = item.Unknown0;
            battleItem.Target = item.Target;
            battleItem.Unknown1 = item.Unknown1;
            battleItem.AttackFlags = item.AttackFlags;
            battleItem.Unknown2 = item.Unknown2;
            battleItem.StatusAttack = item.StatusAttack;
            battleItem.Statuses0 = item.Statuses0;
            battleItem.Statuses1 = item.Statuses1;
            battleItem.AttackParam = item.AttackParam;
            battleItem.Unknown3 = item.Unknown3;
            battleItem.HitCount = item.HitCount;
            battleItem.Element = item.Element;
            result.Add(battleItem);
        }

        return result;
    }

    public static BattleItemRaw[] WriteBattleItems(List<BattleItem> items, KernelTextBlobWriter text)
    {
        BattleItemRaw[] result = new BattleItemRaw[items.Count];
        for (Int32 i = 0; i < items.Count; i++)
        {
            BattleItem battleItem = items[i];
            BattleItemRaw raw = new();
            raw.OffsetName = text.Write(battleItem.Name);
            raw.OffsetDescription = text.Write(battleItem.Description);
            raw.MagicId = battleItem.MagicId;
            raw.AttackType = battleItem.AttackType;
            raw.AttackPower = battleItem.AttackPower;
            raw.Unknown0 = battleItem.Unknown0;
            raw.Target = battleItem.Target;
            raw.Unknown1 = battleItem.Unknown1;
            raw.AttackFlags = battleItem.AttackFlags;
            raw.Unknown2 = battleItem.Unknown2;
            raw.StatusAttack = battleItem.StatusAttack;
            raw.Statuses0 = battleItem.Statuses0;
            raw.Statuses1 = battleItem.Statuses1;
            raw.AttackParam = battleItem.AttackParam;
            raw.Unknown3 = battleItem.Unknown3;
            raw.HitCount = battleItem.HitCount;
            raw.Element = battleItem.Element;
            result[i] = raw;
        }

        return result;
    }

    public static List<FieldItem> ReadFieldItems(FieldItemRaw[] raw, KernelTextBlobReader text)
    {
        List<FieldItem> result = new List<FieldItem>(raw.Length);
        foreach (FieldItemRaw item in raw)
        {
            FieldItem fieldItem = new();
            fieldItem.Name = text.ReadString(item.OffsetName);
            fieldItem.Description = text.ReadString(item.OffsetDescription);
            result.Add(fieldItem);
        }

        return result;
    }

    public static FieldItemRaw[] WriteFieldItems(List<FieldItem> items, KernelTextBlobWriter text)
    {
        FieldItemRaw[] result = new FieldItemRaw[items.Count];
        for (Int32 i = 0; i < items.Count; i++)
        {
            FieldItem fieldItem = items[i];
            FieldItemRaw raw = new();
            raw.OffsetName = text.Write(fieldItem.Name);
            raw.OffsetDescription = text.Write(fieldItem.Description);
            result[i] = raw;
        }

        return result;
    }

    public static List<IndependentGuardianAttack> ReadIndependentGuardianAttacks(IndependentGuardianAttackRaw[] raw, KernelTextBlobReader text)
    {
        List<IndependentGuardianAttack> result = new List<IndependentGuardianAttack>(raw.Length);
        foreach (IndependentGuardianAttackRaw item in raw)
        {
            IndependentGuardianAttack attack = new();
            attack.AttackName = text.ReadString(item.OffsetAttackName);
            attack.MagicId = item.MagicId;
            attack.AttackType = item.AttackType;
            attack.Power = item.Power;
            attack.Status = item.Status;
            attack.Unknown0 = item.Unknown0;
            attack.Flags = item.Flags;
            attack.Unknown1 = item.Unknown1;
            attack.Element = item.Element;
            attack.Unknown2 = item.Unknown2;
            attack.Statuses1 = item.Statuses1;
            attack.Statuses0 = item.Statuses0;
            attack.PowerModifier = item.PowerModifier;
            attack.LevelModifier = item.LevelModifier;
            result.Add(attack);
        }

        return result;
    }

    public static IndependentGuardianAttackRaw[] WriteIndependentGuardianAttacks(List<IndependentGuardianAttack> attacks, KernelTextBlobWriter text)
    {
        IndependentGuardianAttackRaw[] result = new IndependentGuardianAttackRaw[attacks.Count];
        for (Int32 i = 0; i < attacks.Count; i++)
        {
            IndependentGuardianAttack attack = attacks[i];
            IndependentGuardianAttackRaw raw = new();
            raw.OffsetAttackName = text.Write(attack.AttackName);
            raw.MagicId = attack.MagicId;
            raw.AttackType = attack.AttackType;
            raw.Power = attack.Power;
            raw.Status = attack.Status;
            raw.Unknown0 = attack.Unknown0;
            raw.Flags = attack.Flags;
            raw.Unknown1 = attack.Unknown1;
            raw.Element = attack.Element;
            raw.Unknown2 = attack.Unknown2;
            raw.Statuses1 = attack.Statuses1;
            raw.Statuses0 = attack.Statuses0;
            raw.PowerModifier = attack.PowerModifier;
            raw.LevelModifier = attack.LevelModifier;
            result[i] = raw;
        }

        return result;
    }

    public static List<AdditionalCommand> ReadAdditionalCommands(AdditionalCommandRaw[] raw)
    {
        List<AdditionalCommand> result = new List<AdditionalCommand>(raw.Length);
        foreach (AdditionalCommandRaw item in raw)
        {
            AdditionalCommand command = new();
            command.MagicId = item.MagicId;
            command.Unknown = item.Unknown;
            command.AttackType = item.AttackType;
            command.AttackPower = item.AttackPower;
            command.AttackFlags = item.AttackFlags;
            command.HitCount = item.HitCount;
            command.Element = item.Element;
            command.StatusAttack = item.StatusAttack;
            command.Status1 = item.Status1;
            command.Status2 = item.Status2;
            result.Add(command);
        }

        return result;
    }

    public static AdditionalCommandRaw[] WriteAdditionalCommands(List<AdditionalCommand> commands)
    {
        AdditionalCommandRaw[] result = new AdditionalCommandRaw[commands.Count];
        for (Int32 i = 0; i < commands.Count; i++)
        {
            AdditionalCommand command = commands[i];
            AdditionalCommandRaw raw = new();
            raw.MagicId = command.MagicId;
            raw.Unknown = command.Unknown;
            raw.AttackType = command.AttackType;
            raw.AttackPower = command.AttackPower;
            raw.AttackFlags = command.AttackFlags;
            raw.HitCount = command.HitCount;
            raw.Element = command.Element;
            raw.StatusAttack = command.StatusAttack;
            raw.Status1 = command.Status1;
            raw.Status2 = command.Status2;
            result[i] = raw;
        }

        return result;
    }
}
