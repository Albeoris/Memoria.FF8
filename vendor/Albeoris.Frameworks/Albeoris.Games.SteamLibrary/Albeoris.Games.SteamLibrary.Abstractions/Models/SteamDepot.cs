namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Describes an installed Steam content depot.
/// </summary>
public sealed class SteamDepot
{
    /// <summary>
    /// Initializes a depot description.
    /// </summary>
    /// <param name="manifestId">The depot manifest identifier.</param>
    /// <param name="size">The installed size in bytes.</param>
    public SteamDepot(UInt64 manifestId, UInt64 size)
    {
        ManifestId = manifestId;
        Size = size;
    }

    /// <summary>Gets the depot manifest identifier.</summary>
    public UInt64 ManifestId { get; }

    /// <summary>Gets the installed size in bytes.</summary>
    public UInt64 Size { get; }
}
