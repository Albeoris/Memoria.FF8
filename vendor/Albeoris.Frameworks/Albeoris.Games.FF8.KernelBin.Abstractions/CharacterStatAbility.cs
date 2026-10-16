namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>An ability that permanently increases a character stat (e.g. Str+20%).</summary>
public sealed class CharacterStatAbility
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public Byte AbilityPoints { get; set; }
    public Byte Stat { get; set; }
    public Byte Value { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown0 { get; set; }
}
