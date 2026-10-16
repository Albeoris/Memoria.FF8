namespace Albeoris.Games.FF8.FlArchives;

/// <summary>
/// Specifies the space-reservation parameters used by <see cref="FlArchive.Optimize"/>.
/// </summary>
public sealed class FlOptimizeSpec
{
    /// <summary>
    /// Gets or sets the fixed number of extra bytes to reserve after the content of each entry.
    /// Combined with <see cref="RelativeReserveFraction"/> to compute the total reserved slot size.
    /// </summary>
    public Int32 AbsoluteReserveBytes { get; init; }

    /// <summary>
    /// Gets or sets the relative reservation as a fraction of each entry's uncompressed size
    /// (e.g. <c>0.1f</c> reserves an additional 10 % on top of the entry size).
    /// Combined with <see cref="AbsoluteReserveBytes"/>.
    /// </summary>
    public Single RelativeReserveFraction { get; init; }

    /// <summary>
    /// Gets or sets the number of new entries expected to be added after optimization.
    /// Used to pre-allocate space in the metrics (<c>.fi</c>) file: exactly
    /// <c>ExpectedNewEntries × 12</c> zero-bytes are appended to the metrics file so that
    /// future <see cref="IFlArchive.AddEntry"/> calls can overwrite them in-place rather than
    /// extending the file. The listing (<c>.fl</c>) file is not pre-allocated because its
    /// variable-length text format requires appending, which is already efficient.
    /// </summary>
    public Int32 ExpectedNewEntries { get; init; }
}
