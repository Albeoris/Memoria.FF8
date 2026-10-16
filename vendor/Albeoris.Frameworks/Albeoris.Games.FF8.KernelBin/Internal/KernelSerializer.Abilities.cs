using Albeoris.Games.FF8.KernelBin.Abstractions;
using Albeoris.Games.FF8.KernelBin.Internal.Raw;

namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>Maps between the public model types and the raw binary ability sections.</summary>
internal static partial class KernelSerializer
{
    public static List<JunctionAbility> ReadJunctionAbilities(JunctionAbilityRaw[] raw, KernelTextBlobReader text)
    {
        List<JunctionAbility> result = new List<JunctionAbility>(raw.Length);
        foreach (JunctionAbilityRaw item in raw)
        {
            JunctionAbility ability = new();
            ability.Name = text.ReadString(item.OffsetName);
            ability.Description = text.ReadString(item.OffsetDescription);
            ability.AbilityPoints = item.AbilityPoints;
            ability.Flag1 = item.Flag1;
            ability.Flag2 = item.Flag2;
            ability.Flag3 = item.Flag3;
            result.Add(ability);
        }

        return result;
    }

    public static JunctionAbilityRaw[] WriteJunctionAbilities(List<JunctionAbility> abilities, KernelTextBlobWriter text)
    {
        JunctionAbilityRaw[] result = new JunctionAbilityRaw[abilities.Count];
        for (Int32 i = 0; i < abilities.Count; i++)
        {
            JunctionAbility ability = abilities[i];
            JunctionAbilityRaw raw = new();
            raw.OffsetName = text.Write(ability.Name);
            raw.OffsetDescription = text.Write(ability.Description);
            raw.AbilityPoints = ability.AbilityPoints;
            raw.Flag1 = ability.Flag1;
            raw.Flag2 = ability.Flag2;
            raw.Flag3 = ability.Flag3;
            result[i] = raw;
        }

        return result;
    }

    public static List<CommandAbility> ReadCommandAbilities(CommandAbilityRaw[] raw, KernelTextBlobReader text)
    {
        List<CommandAbility> result = new List<CommandAbility>(raw.Length);
        foreach (CommandAbilityRaw item in raw)
        {
            CommandAbility ability = new();
            ability.Name = text.ReadString(item.OffsetName);
            ability.Description = text.ReadString(item.OffsetDescription);
            ability.AbilityPoints = item.AbilityPoints;
            ability.Index = item.Index;
            ability.Unknown0 = item.Unknown0;
            result.Add(ability);
        }

        return result;
    }

    public static CommandAbilityRaw[] WriteCommandAbilities(List<CommandAbility> abilities, KernelTextBlobWriter text)
    {
        CommandAbilityRaw[] result = new CommandAbilityRaw[abilities.Count];
        for (Int32 i = 0; i < abilities.Count; i++)
        {
            CommandAbility ability = abilities[i];
            CommandAbilityRaw raw = new();
            raw.OffsetName = text.Write(ability.Name);
            raw.OffsetDescription = text.Write(ability.Description);
            raw.AbilityPoints = ability.AbilityPoints;
            raw.Index = ability.Index;
            raw.Unknown0 = ability.Unknown0;
            result[i] = raw;
        }

        return result;
    }

    public static List<CharacterStatAbility> ReadCharacterStatAbilities(CharacterStatAbilityRaw[] raw, KernelTextBlobReader text)
    {
        List<CharacterStatAbility> result = new List<CharacterStatAbility>(raw.Length);
        foreach (CharacterStatAbilityRaw item in raw)
        {
            CharacterStatAbility ability = new();
            ability.Name = text.ReadString(item.OffsetName);
            ability.Description = text.ReadString(item.OffsetDescription);
            ability.AbilityPoints = item.AbilityPoints;
            ability.Stat = item.Stat;
            ability.Value = item.Value;
            ability.Unknown0 = item.Unknown0;
            result.Add(ability);
        }

        return result;
    }

    public static CharacterStatAbilityRaw[] WriteCharacterStatAbilities(List<CharacterStatAbility> abilities, KernelTextBlobWriter text)
    {
        CharacterStatAbilityRaw[] result = new CharacterStatAbilityRaw[abilities.Count];
        for (Int32 i = 0; i < abilities.Count; i++)
        {
            CharacterStatAbility ability = abilities[i];
            CharacterStatAbilityRaw raw = new();
            raw.OffsetName = text.Write(ability.Name);
            raw.OffsetDescription = text.Write(ability.Description);
            raw.AbilityPoints = ability.AbilityPoints;
            raw.Stat = ability.Stat;
            raw.Value = ability.Value;
            raw.Unknown0 = ability.Unknown0;
            result[i] = raw;
        }

        return result;
    }

    public static List<CharacterAbility> ReadCharacterAbilities(CharacterAbilityRaw[] raw, KernelTextBlobReader text)
    {
        List<CharacterAbility> result = new List<CharacterAbility>(raw.Length);
        foreach (CharacterAbilityRaw item in raw)
        {
            CharacterAbility ability = new();
            ability.Name = text.ReadString(item.OffsetName);
            ability.Description = text.ReadString(item.OffsetDescription);
            ability.AbilityPoints = item.AbilityPoints;
            ability.Flag1 = item.Flag1;
            ability.Flag2 = item.Flag2;
            ability.Flag3 = item.Flag3;
            result.Add(ability);
        }

        return result;
    }

    public static CharacterAbilityRaw[] WriteCharacterAbilities(List<CharacterAbility> abilities, KernelTextBlobWriter text)
    {
        CharacterAbilityRaw[] result = new CharacterAbilityRaw[abilities.Count];
        for (Int32 i = 0; i < abilities.Count; i++)
        {
            CharacterAbility ability = abilities[i];
            CharacterAbilityRaw raw = new();
            raw.OffsetName = text.Write(ability.Name);
            raw.OffsetDescription = text.Write(ability.Description);
            raw.AbilityPoints = ability.AbilityPoints;
            raw.Flag1 = ability.Flag1;
            raw.Flag2 = ability.Flag2;
            raw.Flag3 = ability.Flag3;
            result[i] = raw;
        }

        return result;
    }

    public static List<PartyAbility> ReadPartyAbilities(PartyAbilityRaw[] raw, KernelTextBlobReader text)
    {
        List<PartyAbility> result = new List<PartyAbility>(raw.Length);
        foreach (PartyAbilityRaw item in raw)
        {
            PartyAbility ability = new();
            ability.Name = text.ReadString(item.OffsetName);
            ability.Description = text.ReadString(item.OffsetDescription);
            ability.AbilityPoints = item.AbilityPoints;
            ability.Flag1 = item.Flag1;
            ability.Flag2 = item.Flag2;
            result.Add(ability);
        }

        return result;
    }

    public static PartyAbilityRaw[] WritePartyAbilities(List<PartyAbility> abilities, KernelTextBlobWriter text)
    {
        PartyAbilityRaw[] result = new PartyAbilityRaw[abilities.Count];
        for (Int32 i = 0; i < abilities.Count; i++)
        {
            PartyAbility ability = abilities[i];
            PartyAbilityRaw raw = new();
            raw.OffsetName = text.Write(ability.Name);
            raw.OffsetDescription = text.Write(ability.Description);
            raw.AbilityPoints = ability.AbilityPoints;
            raw.Flag1 = ability.Flag1;
            raw.Flag2 = ability.Flag2;
            result[i] = raw;
        }

        return result;
    }

    public static List<GuardianAbility> ReadGuardianAbilities(GuardianAbilityRaw[] raw, KernelTextBlobReader text)
    {
        List<GuardianAbility> result = new List<GuardianAbility>(raw.Length);
        foreach (GuardianAbilityRaw item in raw)
        {
            GuardianAbility ability = new();
            ability.Name = text.ReadString(item.OffsetName);
            ability.Description = text.ReadString(item.OffsetDescription);
            ability.AbilityPoints = item.AbilityPoints;
            ability.Boost = item.Boost;
            ability.Stat = item.Stat;
            ability.Value = item.Value;
            result.Add(ability);
        }

        return result;
    }

    public static GuardianAbilityRaw[] WriteGuardianAbilities(List<GuardianAbility> abilities, KernelTextBlobWriter text)
    {
        GuardianAbilityRaw[] result = new GuardianAbilityRaw[abilities.Count];
        for (Int32 i = 0; i < abilities.Count; i++)
        {
            GuardianAbility ability = abilities[i];
            GuardianAbilityRaw raw = new();
            raw.OffsetName = text.Write(ability.Name);
            raw.OffsetDescription = text.Write(ability.Description);
            raw.AbilityPoints = ability.AbilityPoints;
            raw.Boost = ability.Boost;
            raw.Stat = ability.Stat;
            raw.Value = ability.Value;
            result[i] = raw;
        }

        return result;
    }

    public static List<MenuAbility> ReadMenuAbilities(MenuAbilityRaw[] raw, KernelTextBlobReader text)
    {
        List<MenuAbility> result = new List<MenuAbility>(raw.Length);
        foreach (MenuAbilityRaw item in raw)
        {
            MenuAbility ability = new();
            ability.Name = text.ReadString(item.OffsetName);
            ability.Description = text.ReadString(item.OffsetDescription);
            ability.AbilityPoints = item.AbilityPoints;
            ability.Index = item.Index;
            ability.Start = item.Start;
            ability.End = item.End;
            result.Add(ability);
        }

        return result;
    }

    public static MenuAbilityRaw[] WriteMenuAbilities(List<MenuAbility> abilities, KernelTextBlobWriter text)
    {
        MenuAbilityRaw[] result = new MenuAbilityRaw[abilities.Count];
        for (Int32 i = 0; i < abilities.Count; i++)
        {
            MenuAbility ability = abilities[i];
            MenuAbilityRaw raw = new();
            raw.OffsetName = text.Write(ability.Name);
            raw.OffsetDescription = text.Write(ability.Description);
            raw.AbilityPoints = ability.AbilityPoints;
            raw.Index = ability.Index;
            raw.Start = ability.Start;
            raw.End = ability.End;
            result[i] = raw;
        }

        return result;
    }
}
