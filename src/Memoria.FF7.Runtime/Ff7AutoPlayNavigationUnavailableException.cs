using System.Numerics;

namespace Memoria.FF7.Runtime;

/// <summary>A physical target is absent from the current native navigation component.</summary>
public sealed class Ff7AutoPlayNavigationUnavailableException(int entity, string reason, IReadOnlyList<int>? blockingEntities = null, Vector3? destination = null, int? destinationTriangle = null) : Exception(reason)
{
    public int Entity { get; } = entity;
    public string Reason { get; } = reason;
    public IReadOnlyList<int> BlockingEntities { get; } = blockingEntities?.Distinct().Order().ToArray() ?? [];
    public Vector3? Destination { get; } = destination;
    public int? DestinationTriangle { get; } = destinationTriangle;
}
