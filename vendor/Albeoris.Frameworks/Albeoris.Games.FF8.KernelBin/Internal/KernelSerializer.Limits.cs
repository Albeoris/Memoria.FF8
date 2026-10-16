using Albeoris.Games.FF8.KernelBin.Abstractions;
using Albeoris.Games.FF8.KernelBin.Internal.Raw;

namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Maps between the public model types and the raw binary sections for playable characters
/// and their limit breaks.
/// </summary>
internal static partial class KernelSerializer
{
    public static List<SquallLimit> ReadSquallLimits(SquallLimitRaw[] raw, KernelTextBlobReader text)
    {
        List<SquallLimit> result = new List<SquallLimit>(raw.Length);
        foreach (SquallLimitRaw item in raw)
        {
            SquallLimit limit = new();
            limit.Name = text.ReadString(item.OffsetName);
            limit.Description = text.ReadString(item.OffsetDescription);
            limit.MagicId = item.MagicId;
            limit.AttackType = item.AttackType;
            limit.Unknown0 = item.Unknown0;
            limit.AttackPower = item.AttackPower;
            limit.Unknown1 = item.Unknown1;
            limit.Target = item.Target;
            limit.AttackFlags = item.AttackFlags;
            limit.HitCount = item.HitCount;
            limit.Element = item.Element;
            limit.ElementPercent = item.ElementPercent;
            limit.StatusAttack = item.StatusAttack;
            limit.Unknown2 = item.Unknown2;
            limit.Unknown3 = item.Unknown3;
            limit.Statuses0 = item.Statuses0;
            limit.Statuses1 = item.Statuses1;
            result.Add(limit);
        }

        return result;
    }

    public static SquallLimitRaw[] WriteSquallLimits(List<SquallLimit> limits, KernelTextBlobWriter text)
    {
        SquallLimitRaw[] result = new SquallLimitRaw[limits.Count];
        for (Int32 i = 0; i < limits.Count; i++)
        {
            SquallLimit limit = limits[i];
            SquallLimitRaw raw = new();
            raw.OffsetName = text.Write(limit.Name);
            raw.OffsetDescription = text.Write(limit.Description);
            raw.MagicId = limit.MagicId;
            raw.AttackType = limit.AttackType;
            raw.Unknown0 = limit.Unknown0;
            raw.AttackPower = limit.AttackPower;
            raw.Unknown1 = limit.Unknown1;
            raw.Target = limit.Target;
            raw.AttackFlags = limit.AttackFlags;
            raw.HitCount = limit.HitCount;
            raw.Element = limit.Element;
            raw.ElementPercent = limit.ElementPercent;
            raw.StatusAttack = limit.StatusAttack;
            raw.Unknown2 = limit.Unknown2;
            raw.Unknown3 = limit.Unknown3;
            raw.Statuses0 = limit.Statuses0;
            raw.Statuses1 = limit.Statuses1;
            result[i] = raw;
        }

        return result;
    }

    public static List<Character> ReadCharacters(CharacterRaw[] raw, KernelTextBlobReader text)
    {
        List<Character> result = new List<Character>(raw.Length);
        foreach (CharacterRaw item in raw)
        {
            Character character = new();
            character.Name = text.ReadString(item.OffsetName);
            character.CrisisLevel = item.CrisisLevel;
            character.Gender = item.Gender;
            character.LimitId = item.LimitId;
            character.LimitParam = item.LimitParam;
            character.Exp1 = item.Exp1;
            character.Exp2 = item.Exp2;
            character.Hp1 = item.Hp1;
            character.Hp2 = item.Hp2;
            character.Hp3 = item.Hp3;
            character.Hp4 = item.Hp4;
            character.Str1 = item.Str1;
            character.Str2 = item.Str2;
            character.Str3 = item.Str3;
            character.Str4 = item.Str4;
            character.Vit1 = item.Vit1;
            character.Vit2 = item.Vit2;
            character.Vit3 = item.Vit3;
            character.Vit4 = item.Vit4;
            character.Mag1 = item.Mag1;
            character.Mag2 = item.Mag2;
            character.Mag3 = item.Mag3;
            character.Mag4 = item.Mag4;
            character.Spr1 = item.Spr1;
            character.Spr2 = item.Spr2;
            character.Spr3 = item.Spr3;
            character.Spr4 = item.Spr4;
            character.Spd1 = item.Spd1;
            character.Spd2 = item.Spd2;
            character.Spd3 = item.Spd3;
            character.Spd4 = item.Spd4;
            character.Luck1 = item.Luck1;
            character.Luck2 = item.Luck2;
            character.Luck3 = item.Luck3;
            character.Luck4 = item.Luck4;
            result.Add(character);
        }

        return result;
    }

    public static CharacterRaw[] WriteCharacters(List<Character> characters, KernelTextBlobWriter text)
    {
        CharacterRaw[] result = new CharacterRaw[characters.Count];
        for (Int32 i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            CharacterRaw raw = new();
            raw.OffsetName = text.Write(character.Name);
            raw.CrisisLevel = character.CrisisLevel;
            raw.Gender = character.Gender;
            raw.LimitId = character.LimitId;
            raw.LimitParam = character.LimitParam;
            raw.Exp1 = character.Exp1;
            raw.Exp2 = character.Exp2;
            raw.Hp1 = character.Hp1;
            raw.Hp2 = character.Hp2;
            raw.Hp3 = character.Hp3;
            raw.Hp4 = character.Hp4;
            raw.Str1 = character.Str1;
            raw.Str2 = character.Str2;
            raw.Str3 = character.Str3;
            raw.Str4 = character.Str4;
            raw.Vit1 = character.Vit1;
            raw.Vit2 = character.Vit2;
            raw.Vit3 = character.Vit3;
            raw.Vit4 = character.Vit4;
            raw.Mag1 = character.Mag1;
            raw.Mag2 = character.Mag2;
            raw.Mag3 = character.Mag3;
            raw.Mag4 = character.Mag4;
            raw.Spr1 = character.Spr1;
            raw.Spr2 = character.Spr2;
            raw.Spr3 = character.Spr3;
            raw.Spr4 = character.Spr4;
            raw.Spd1 = character.Spd1;
            raw.Spd2 = character.Spd2;
            raw.Spd3 = character.Spd3;
            raw.Spd4 = character.Spd4;
            raw.Luck1 = character.Luck1;
            raw.Luck2 = character.Luck2;
            raw.Luck3 = character.Luck3;
            raw.Luck4 = character.Luck4;
            result[i] = raw;
        }

        return result;
    }

    public static List<NpcLimit> ReadNpcLimits(NpcLimitRaw[] raw, KernelTextBlobReader text)
    {
        List<NpcLimit> result = new List<NpcLimit>(raw.Length);
        foreach (NpcLimitRaw item in raw)
        {
            NpcLimit limit = new();
            limit.Name = text.ReadString(item.OffsetName);
            limit.Description = text.ReadString(item.OffsetDescription);
            limit.MagicId = item.MagicId;
            limit.AttackType = item.AttackType;
            limit.AttackPower = item.AttackPower;
            limit.Unknown0 = item.Unknown0;
            limit.Target = item.Target;
            limit.AttackFlags = item.AttackFlags;
            limit.HitCount = item.HitCount;
            limit.Element = (Element)item.Element;
            limit.ElementPercent = item.ElementPercent;
            limit.StatusAttack = item.StatusAttack;
            limit.Statuses0 = item.Statuses0;
            limit.Unknown1 = item.Unknown1;
            limit.Statuses1 = item.Statuses1;
            result.Add(limit);
        }

        return result;
    }

    public static NpcLimitRaw[] WriteNpcLimits(List<NpcLimit> limits, KernelTextBlobWriter text)
    {
        NpcLimitRaw[] result = new NpcLimitRaw[limits.Count];
        for (Int32 i = 0; i < limits.Count; i++)
        {
            NpcLimit limit = limits[i];
            NpcLimitRaw raw = new();
            raw.OffsetName = text.Write(limit.Name);
            raw.OffsetDescription = text.Write(limit.Description);
            raw.MagicId = limit.MagicId;
            raw.AttackType = limit.AttackType;
            raw.AttackPower = limit.AttackPower;
            raw.Unknown0 = limit.Unknown0;
            raw.Target = limit.Target;
            raw.AttackFlags = limit.AttackFlags;
            raw.HitCount = limit.HitCount;
            raw.Element = (Byte)limit.Element;
            raw.ElementPercent = limit.ElementPercent;
            raw.StatusAttack = limit.StatusAttack;
            raw.Statuses0 = limit.Statuses0;
            raw.Unknown1 = limit.Unknown1;
            raw.Statuses1 = limit.Statuses1;
            result[i] = raw;
        }

        return result;
    }

    public static List<BlueMagic> ReadBlueMagics(BlueMagicRaw[] raw, KernelTextBlobReader text)
    {
        List<BlueMagic> result = new List<BlueMagic>(raw.Length);
        foreach (BlueMagicRaw item in raw)
        {
            BlueMagic magic = new();
            magic.Name = text.ReadString(item.OffsetName);
            magic.Description = text.ReadString(item.OffsetDescription);
            magic.MagicId = item.MagicId;
            magic.Unknown0 = item.Unknown0;
            magic.AttackType = item.AttackType;
            magic.Unknown1 = item.Unknown1;
            magic.Target = item.Target;
            magic.AttackFlags = item.AttackFlags;
            magic.Unknown2 = item.Unknown2;
            magic.Element = item.Element;
            magic.StatusAttack = item.StatusAttack;
            magic.Crit = item.Crit;
            magic.Unknown3 = item.Unknown3;
            result.Add(magic);
        }

        return result;
    }

    public static BlueMagicRaw[] WriteBlueMagics(List<BlueMagic> magics, KernelTextBlobWriter text)
    {
        BlueMagicRaw[] result = new BlueMagicRaw[magics.Count];
        for (Int32 i = 0; i < magics.Count; i++)
        {
            BlueMagic magic = magics[i];
            BlueMagicRaw raw = new();
            raw.OffsetName = text.Write(magic.Name);
            raw.OffsetDescription = text.Write(magic.Description);
            raw.MagicId = magic.MagicId;
            raw.Unknown0 = magic.Unknown0;
            raw.AttackType = magic.AttackType;
            raw.Unknown1 = magic.Unknown1;
            raw.Target = magic.Target;
            raw.AttackFlags = magic.AttackFlags;
            raw.Unknown2 = magic.Unknown2;
            raw.Element = magic.Element;
            raw.StatusAttack = magic.StatusAttack;
            raw.Crit = magic.Crit;
            raw.Unknown3 = magic.Unknown3;
            result[i] = raw;
        }

        return result;
    }

    public static List<QuistisLimit> ReadQuistisLimits(QuistisLimitRaw[] raw)
    {
        List<QuistisLimit> result = new List<QuistisLimit>(raw.Length);
        foreach (QuistisLimitRaw item in raw)
        {
            QuistisLimit limit = new();
            limit.Statuses1 = item.Statuses1;
            limit.Statuses0 = item.Statuses0;
            limit.AttackPower = item.AttackPower;
            limit.AttackParam = item.AttackParam;
            result.Add(limit);
        }

        return result;
    }

    public static QuistisLimitRaw[] WriteQuistisLimits(List<QuistisLimit> limits)
    {
        QuistisLimitRaw[] result = new QuistisLimitRaw[limits.Count];
        for (Int32 i = 0; i < limits.Count; i++)
        {
            QuistisLimit limit = limits[i];
            QuistisLimitRaw raw = new();
            raw.Statuses1 = limit.Statuses1;
            raw.Statuses0 = limit.Statuses0;
            raw.AttackPower = limit.AttackPower;
            raw.AttackParam = limit.AttackParam;
            result[i] = raw;
        }

        return result;
    }

    public static List<IrvinLimit> ReadIrvinLimits(IrvinLimitRaw[] raw, KernelTextBlobReader text)
    {
        List<IrvinLimit> result = new List<IrvinLimit>(raw.Length);
        foreach (IrvinLimitRaw item in raw)
        {
            IrvinLimit limit = new();
            limit.Name = text.ReadString(item.OffsetName);
            limit.Description = text.ReadString(item.OffsetDescription);
            limit.MagicId = item.MagicId;
            limit.AttackType = item.AttackType;
            limit.AttackPower = item.AttackPower;
            limit.Unknown0 = item.Unknown0;
            limit.Target = item.Target;
            limit.AttackFlags = item.AttackFlags;
            limit.HitCount = item.HitCount;
            limit.Element = (Element)item.Element;
            limit.ElementPercent = item.ElementPercent;
            limit.StatusAttack = item.StatusAttack;
            limit.Statuses0 = item.Statuses0;
            limit.ItemIndex = item.ItemIndex;
            limit.Crit = item.Crit;
            limit.Statuses1 = item.Statuses1;
            result.Add(limit);
        }

        return result;
    }

    public static IrvinLimitRaw[] WriteIrvinLimits(List<IrvinLimit> limits, KernelTextBlobWriter text)
    {
        IrvinLimitRaw[] result = new IrvinLimitRaw[limits.Count];
        for (Int32 i = 0; i < limits.Count; i++)
        {
            IrvinLimit limit = limits[i];
            IrvinLimitRaw raw = new();
            raw.OffsetName = text.Write(limit.Name);
            raw.OffsetDescription = text.Write(limit.Description);
            raw.MagicId = limit.MagicId;
            raw.AttackType = limit.AttackType;
            raw.AttackPower = limit.AttackPower;
            raw.Unknown0 = limit.Unknown0;
            raw.Target = limit.Target;
            raw.AttackFlags = limit.AttackFlags;
            raw.HitCount = limit.HitCount;
            raw.Element = (Byte)limit.Element;
            raw.ElementPercent = limit.ElementPercent;
            raw.StatusAttack = limit.StatusAttack;
            raw.Statuses0 = limit.Statuses0;
            raw.ItemIndex = limit.ItemIndex;
            raw.Crit = limit.Crit;
            raw.Statuses1 = limit.Statuses1;
            result[i] = raw;
        }

        return result;
    }

    public static List<ZellLimit> ReadZellLimits(ZellLimitRaw[] raw, KernelTextBlobReader text)
    {
        List<ZellLimit> result = new List<ZellLimit>(raw.Length);
        foreach (ZellLimitRaw item in raw)
        {
            ZellLimit limit = new();
            limit.Name = text.ReadString(item.OffsetName);
            limit.Description = text.ReadString(item.OffsetDescription);
            limit.MagicId = item.MagicId;
            limit.AttackType = item.AttackType;
            limit.AttackPower = item.AttackPower;
            limit.AttackFlags = item.AttackFlags;
            limit.Unknown0 = item.Unknown0;
            limit.Target = item.Target;
            limit.Unknown1 = item.Unknown1;
            limit.HitCount = item.HitCount;
            limit.Element = item.Element;
            limit.ElementPercent = item.ElementPercent;
            limit.StatusAttack = item.StatusAttack;
            limit.Combo1 = item.Combo1;
            limit.Combo2 = item.Combo2;
            limit.Combo3 = item.Combo3;
            limit.Combo4 = item.Combo4;
            limit.Combo5 = item.Combo5;
            limit.Status0 = item.Status0;
            limit.Status1 = item.Status1;
            result.Add(limit);
        }

        return result;
    }

    public static ZellLimitRaw[] WriteZellLimits(List<ZellLimit> limits, KernelTextBlobWriter text)
    {
        ZellLimitRaw[] result = new ZellLimitRaw[limits.Count];
        for (Int32 i = 0; i < limits.Count; i++)
        {
            ZellLimit limit = limits[i];
            ZellLimitRaw raw = new();
            raw.OffsetName = text.Write(limit.Name);
            raw.OffsetDescription = text.Write(limit.Description);
            raw.MagicId = limit.MagicId;
            raw.AttackType = limit.AttackType;
            raw.AttackPower = limit.AttackPower;
            raw.AttackFlags = limit.AttackFlags;
            raw.Unknown0 = limit.Unknown0;
            raw.Target = limit.Target;
            raw.Unknown1 = limit.Unknown1;
            raw.HitCount = limit.HitCount;
            raw.Element = limit.Element;
            raw.ElementPercent = limit.ElementPercent;
            raw.StatusAttack = limit.StatusAttack;
            raw.Combo1 = limit.Combo1;
            raw.Combo2 = limit.Combo2;
            raw.Combo3 = limit.Combo3;
            raw.Combo4 = limit.Combo4;
            raw.Combo5 = limit.Combo5;
            raw.Status0 = limit.Status0;
            raw.Status1 = limit.Status1;
            result[i] = raw;
        }

        return result;
    }

    public static List<ZellDuelMove> ReadZellDuelMoves(ZellDuelMoveRaw[] raw)
    {
        List<ZellDuelMove> result = new List<ZellDuelMove>(raw.Length);
        foreach (ZellDuelMoveRaw item in raw)
        {
            ZellDuelMove move = new();
            move.StartMove = item.StartMove;
            move.NextSequence1 = item.NextSequence1;
            move.NextSequence2 = item.NextSequence2;
            move.NextSequence3 = item.NextSequence3;
            result.Add(move);
        }

        return result;
    }

    public static ZellDuelMoveRaw[] WriteZellDuelMoves(List<ZellDuelMove> moves)
    {
        ZellDuelMoveRaw[] result = new ZellDuelMoveRaw[moves.Count];
        for (Int32 i = 0; i < moves.Count; i++)
        {
            ZellDuelMove move = moves[i];
            ZellDuelMoveRaw raw = new();
            raw.StartMove = move.StartMove;
            raw.NextSequence1 = move.NextSequence1;
            raw.NextSequence2 = move.NextSequence2;
            raw.NextSequence3 = move.NextSequence3;
            result[i] = raw;
        }

        return result;
    }

    public static List<RinoaLimit> ReadRinoaLimits(RinoaLimitRaw[] raw, KernelTextBlobReader text)
    {
        List<RinoaLimit> result = new List<RinoaLimit>(raw.Length);
        foreach (RinoaLimitRaw item in raw)
        {
            RinoaLimit limit = new();
            limit.Name = text.ReadString(item.OffsetName);
            limit.Description = text.ReadString(item.OffsetDescription);
            limit.Unknown = item.Unknown;
            limit.Target = item.Target;
            limit.AbilityId = item.AbilityId;
            limit.Unknown1 = item.Unknown1;
            result.Add(limit);
        }

        return result;
    }

    public static RinoaLimitRaw[] WriteRinoaLimits(List<RinoaLimit> limits, KernelTextBlobWriter text)
    {
        RinoaLimitRaw[] result = new RinoaLimitRaw[limits.Count];
        for (Int32 i = 0; i < limits.Count; i++)
        {
            RinoaLimit limit = limits[i];
            RinoaLimitRaw raw = new();
            raw.OffsetName = text.Write(limit.Name);
            raw.OffsetDescription = text.Write(limit.Description);
            raw.Unknown = limit.Unknown;
            raw.Target = limit.Target;
            raw.AbilityId = limit.AbilityId;
            raw.Unknown1 = limit.Unknown1;
            result[i] = raw;
        }

        return result;
    }

    public static List<RinoaAngeloAttack> ReadRinoaAngeloAttacks(RinoaAngeloAttackRaw[] raw, KernelTextBlobReader text)
    {
        List<RinoaAngeloAttack> result = new List<RinoaAngeloAttack>(raw.Length);
        foreach (RinoaAngeloAttackRaw item in raw)
        {
            RinoaAngeloAttack attack = new();
            attack.Name = text.ReadString(item.OffsetName);
            attack.MagicId = item.MagicId;
            attack.AttackType = item.AttackType;
            attack.AttackPower = item.AttackPower;
            attack.AttackFlags = item.AttackFlags;
            attack.Unknown0 = item.Unknown0;
            attack.Target = item.Target;
            attack.Unknown1 = item.Unknown1;
            attack.HitCount = item.HitCount;
            attack.Element = (Element)item.Element;
            attack.ElementPercent = item.ElementPercent;
            attack.StatusAttack = item.StatusAttack;
            attack.Statuses0 = item.Statuses0;
            attack.Statuses1 = item.Statuses1;
            result.Add(attack);
        }

        return result;
    }

    public static RinoaAngeloAttackRaw[] WriteRinoaAngeloAttacks(List<RinoaAngeloAttack> attacks, KernelTextBlobWriter text)
    {
        RinoaAngeloAttackRaw[] result = new RinoaAngeloAttackRaw[attacks.Count];
        for (Int32 i = 0; i < attacks.Count; i++)
        {
            RinoaAngeloAttack attack = attacks[i];
            RinoaAngeloAttackRaw raw = new();
            raw.OffsetName = text.Write(attack.Name);
            raw.MagicId = attack.MagicId;
            raw.AttackType = attack.AttackType;
            raw.AttackPower = attack.AttackPower;
            raw.AttackFlags = attack.AttackFlags;
            raw.Unknown0 = attack.Unknown0;
            raw.Target = attack.Target;
            raw.Unknown1 = attack.Unknown1;
            raw.HitCount = attack.HitCount;
            raw.Element = (Byte)attack.Element;
            raw.ElementPercent = attack.ElementPercent;
            raw.StatusAttack = attack.StatusAttack;
            raw.Statuses0 = attack.Statuses0;
            raw.Statuses1 = attack.Statuses1;
            result[i] = raw;
        }

        return result;
    }
}
