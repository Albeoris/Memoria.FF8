namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>A junction ability (e.g. HP-Bonus, Str-Bonus junction boosters).</summary>
public sealed class JunctionAbility
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public Byte AbilityPoints { get; set; }
    public Byte Flag1 { get; set; }
    public Byte Flag2 { get; set; }
    public Byte Flag3 { get; set; }
}
