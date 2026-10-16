namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>An item usable from the field menu.</summary>
public sealed class FieldItem
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }
}
