namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>The effect granted when a character devours a monster.</summary>
public sealed class DevourEffect
{
    /// <summary>The message shown after devouring, or <see langword="null"/> if this slot has none.</summary>
    public String? Description { get; set; }

    public Byte Effect { get; set; }
    public Byte Quantity { get; set; }
    public UInt32 Statuses1 { get; set; }
    public UInt16 Statuses0 { get; set; }
    public Byte StatFlags { get; set; }
    public Byte Hp { get; set; }
}
