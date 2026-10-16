using Albeoris.Games.Core.Abstractions.NsCapacityCalculator;

namespace Albeoris.Games.Core.NsCapacityCalculator;

/// <summary>
/// Contains extension methods for <see cref="ICapacityCalculator"/>
/// </summary>
public static class ExtensionsForICapacityCalculator
{
    /// <summary>
    /// Registers a new boundary at the specified offset.
    /// </summary>
    /// <param name="calculator">The instance of <see cref="ICapacityCalculator"/></param>
    /// <param name="boundary">The offset value at which to place the boundary.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="calculator"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the given <paramref name="boundary"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the <paramref name="boundary"/> is already in the list.</exception>
    public static void RegisterBoundary(this ICapacityCalculator calculator, Int64 boundary)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentOutOfRangeException.ThrowIfNegative(boundary, nameof(boundary));

        if (!calculator.TryRegisterBoundary(boundary))
            throw new InvalidOperationException($"Boundary at {boundary} is already registered.");
    }

    /// <summary>
    /// Removes the specified boundary from the list.
    /// Throws an exception if the boundary doesn't exist.
    /// </summary>
    /// <param name="calculator">The instance of <see cref="ICapacityCalculator"/></param>
    /// <param name="boundary">The offset value of the boundary to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="calculator"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="boundary"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the boundary is not found in the list.</exception>
    public static void UnregisterBoundary(this ICapacityCalculator calculator, Int64 boundary)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentOutOfRangeException.ThrowIfNegative(boundary, nameof(boundary));

        if (!calculator.TryUnregisterBoundary(boundary))
            throw new InvalidOperationException($"Boundary {boundary} does not exist.");
    }

    /// <summary>
    /// Gets the capacity from the given offset to the next boundary.
    /// </summary>
    /// <param name="calculator">The instance of <see cref="ICapacityCalculator"/></param>
    /// <param name="offset">The starting offset.</param>
    /// <returns>
    /// Size available until the next boundary.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="calculator"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the offset is outside the registered boundaries.</exception>
    public static Int64 GetCapacity(this ICapacityCalculator calculator, Int64 offset)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        return calculator.TryGetCapacity(offset, out Int64 boundary)
            ? boundary
            : throw new InvalidOperationException($"Offset {offset} is outside the registered boundaries.");
    }
}