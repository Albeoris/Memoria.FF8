namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>An ability that improves a Guardian Force (e.g. boosting a junctioned stat).</summary>
public sealed class GuardianAbility
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public Byte AbilityPoints { get; set; }
    public Byte Boost { get; set; }
    public Byte Stat { get; set; }
    public Byte Value { get; set; }
}
