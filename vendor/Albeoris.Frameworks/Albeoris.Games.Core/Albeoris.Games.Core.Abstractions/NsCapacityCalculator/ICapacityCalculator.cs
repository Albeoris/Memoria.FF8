namespace Albeoris.Games.Core.Abstractions.NsCapacityCalculator;

/// <summary>
/// Defines methods to register boundaries and calculate capacity until the next boundary.
/// </summary>
public interface ICapacityCalculator
{
    /// <summary>
    /// Read-only property describing how many elements are registered.
    /// </summary>
    Int32 Count { get; }

    /// <summary>
    /// Tries to register a new boundary at the specified offset.
    /// </summary>
    /// <param name="boundary">The offset value at which to place the boundary.</param>
    /// <returns>
    /// <c>true</c> if the boundary has been registered;
    /// <c>false</c> if the boundary is already exist.
    /// </returns>
    Boolean TryRegisterBoundary(Int64 boundary);

    /// <summary>
    /// Tries to unregister the already registered boundary with the specified offset.
    /// </summary>
    /// <param name="boundary">The offset value of the registered boundary.</param>
    /// <returns>
    /// <c>true</c> if the boundary has been unregistered;
    /// <c>false</c> if the boundary doesn't exist.
    /// </returns>
    Boolean TryUnregisterBoundary(Int64 boundary);

    /// <summary>
    /// Tries to get the capacity from the given offset to the next boundary.
    /// </summary>
    /// <param name="offset">The starting offset.</param>
    /// <param name="capacity">
    /// When this method returns, contains the size available until the next boundary if found.
    /// </param>
    /// <returns>
    /// <c>true</c> if the next boundary was found and <paramref name="capacity"/> was set;
    /// otherwise, <c>false</c>.
    /// </returns>
    Boolean TryGetCapacity(Int64 offset, out Int64 capacity);

    /// <summary>
    /// Clears all registered boundaries.
    /// </summary>
    public void ClearBoundaries();

    /// <summary>
    /// Ensures that the size of the internal list is at least the specified <paramref name="boundaryCount"/>.
    /// If the current size of the list is less than specified <paramref name="boundaryCount"/>,
    /// the size is increased by continuously twice current size until it is at least the specified <paramref name="boundaryCount"/>.
    /// </summary>
    /// <param name="boundaryCount">The minimum size to ensure.</param>
    /// <returns>The new size of the internal list.</returns>
    void EnsureSize(Int32 boundaryCount);
}