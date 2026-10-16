namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// A battle command available from the main battle menu (e.g. Attack, Magic, GF).
/// </summary>
public sealed class BattleCommand
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    /// <summary>The identifier of the ability that implements this command.</summary>
    public Byte AbilityId { get; set; }

    /// <summary>The default targeting mode of this command.</summary>
    public Byte Target { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown1 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown2 { get; set; }
}
