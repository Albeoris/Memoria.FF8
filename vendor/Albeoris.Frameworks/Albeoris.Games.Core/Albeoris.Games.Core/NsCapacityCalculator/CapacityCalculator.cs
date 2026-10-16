using Albeoris.Games.Core.Abstractions.NsCapacityCalculator;

namespace Albeoris.Games.Core.NsCapacityCalculator;

/// <summary><inheritdoc cref="ICapacityCalculator"/></summary>
public sealed class CapacityCalculator : ICapacityCalculator
{
    private readonly List<Int64> _offsets;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityCalculator"/> class with a default boundary list capacity of 1024.
    /// </summary>
    public CapacityCalculator()
        : this(boundariesCount: 1024)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityCalculator"/> class with the specified capacity for the boundary list.
    /// </summary>
    /// <param name="boundariesCount">The initial capacity for storing boundary offsets.</param>
    public CapacityCalculator(Int32 boundariesCount)
    {
        _offsets = new List<Int64>(capacity: boundariesCount);
    }

    /// <inheritdoc cref="ICapacityCalculator.Count"/>
    public Int32 Count => _offsets.Count;

    /// <inheritdoc cref="ICapacityCalculator.TryRegisterBoundary"/>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the given <paramref name="boundary"/> is negative.</exception>
    public Boolean TryRegisterBoundary(Int64 boundary)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(boundary, nameof(boundary));

        Int32 index = _offsets.BinarySearch(boundary);
        if (index >= 0)
            return false;

        _offsets.Insert(~index, boundary);
        return true;
    }

    /// <inheritdoc cref="ICapacityCalculator.TryUnregisterBoundary"/>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="boundary"/> is negative.</exception>
    public Boolean TryUnregisterBoundary(Int64 boundary)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(boundary, nameof(boundary));

        Int32 index = _offsets.BinarySearch(boundary);
        if (index < 0)
            return false;

        _offsets.RemoveAt(index);
        return true;
    }

    /// <inheritdoc cref="ICapacityCalculator.TryGetCapacity"/>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the given <paramref name="offset"/> is negative.</exception>
    public Boolean TryGetCapacity(Int64 offset, out Int64 capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset, nameof(offset));

        Int32 index = _offsets.BinarySearch(offset);
        if (index < 0)
            index = ~index;
        else
            index++;

        if (index == _offsets.Count)
        {
            capacity = 0;
            return false;
        }

        capacity = _offsets[index] - offset;
        return true;
    }

    /// <inheritdoc cref="ICapacityCalculator.ClearBoundaries"/>
    public void ClearBoundaries()
    {
        _offsets.Clear();
    }

    /// <inheritdoc cref="ICapacityCalculator.EnsureSize"/>
    public void EnsureSize(Int32 boundaryCount)
    {
        _offsets.EnsureCapacity(boundaryCount);
    }
}