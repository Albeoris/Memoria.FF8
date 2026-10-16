namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>An ability that grants a character a passive perk (e.g. Auto-Potion).</summary>
public sealed class CharacterAbility
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public Byte AbilityPoints { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Flag1 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Flag2 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Flag3 { get; set; }
}
