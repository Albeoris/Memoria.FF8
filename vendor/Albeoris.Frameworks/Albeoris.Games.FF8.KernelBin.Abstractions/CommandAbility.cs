namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>An ability that unlocks a battle command (see <see cref="BattleCommand"/>).</summary>
public sealed class CommandAbility
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public Byte AbilityPoints { get; set; }
    public Byte Index { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public UInt16 Unknown0 { get; set; }
}
