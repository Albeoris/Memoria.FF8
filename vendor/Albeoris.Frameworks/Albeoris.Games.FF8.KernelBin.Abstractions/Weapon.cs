namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>A character weapon.</summary>
public sealed class Weapon
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    public Byte RenzokukenFinishers { get; set; }
    public Byte CharacterId { get; set; }
    public Byte AttackType { get; set; }
    public Byte AttackPower { get; set; }
    public Byte AttackParam { get; set; }
    public Byte StrBonus { get; set; }
    public Byte Tier { get; set; }
    public Byte CritBonus { get; set; }
    public Byte Melee { get; set; }

    /// <summary>
    /// Undetermined trailing byte. Present in the original file layout but never assigned a
    /// known meaning by the reference implementation; preserved verbatim for a lossless round trip.
    /// </summary>
    public Byte Unknown0 { get; set; }
}
