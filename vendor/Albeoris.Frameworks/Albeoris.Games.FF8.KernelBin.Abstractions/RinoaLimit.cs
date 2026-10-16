namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>One of Rinoa's Combine limit break attacks.</summary>
public sealed class RinoaLimit
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown { get; set; }

    public Byte Target { get; set; }
    public Byte AbilityId { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown1 { get; set; }
}
