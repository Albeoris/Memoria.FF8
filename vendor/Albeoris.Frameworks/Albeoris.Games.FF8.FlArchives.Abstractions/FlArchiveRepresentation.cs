namespace Albeoris.Games.FF8.FlArchives.Abstractions;

/// <summary>
/// Determines how a <see cref="IFlArchive"/> is opened and how its entries are presented.
/// </summary>
public enum FlArchiveRepresentation
{
    /// <summary>
    /// Entries are exposed as-is, exactly as stored in the listing file.
    /// Sub-archives contained within the archive are treated as ordinary file entries.
    /// </summary>
    Files = 1,

    /// <summary>
    /// Sub-archives (triplets of <c>.fl</c>, <c>.fi</c>, and <c>.fs</c> entries sharing the
    /// same base name) are transparently expanded. Their contents appear as direct children of
    /// the parent archive, with the sub-archive's <c>.fl</c> relative path used as a directory
    /// prefix so that it can be distinguished from a real folder. For example, an entry
    /// <c>field/mapdata/bc/bccent12.fl</c> in the parent would cause inner entries to be
    /// presented as <c>field/mapdata/bc/bccent12.fl/&lt;innerRelativePath&gt;</c>.
    /// </summary>
    Folder = 2,
}
