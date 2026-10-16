namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>An ability that unlocks a menu feature (e.g. Item Refine).</summary>
public sealed class MenuAbility
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public Byte AbilityPoints { get; set; }
    public Byte Index { get; set; }
    public Byte Start { get; set; }
    public Byte End { get; set; }
}
