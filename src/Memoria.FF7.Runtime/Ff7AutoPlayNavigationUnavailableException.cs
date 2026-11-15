using System.Numerics;

namespace Memoria.FF7.Runtime;

/// <summary>A physical target or its current local approach is unavailable under native movement admission.</summary>
public sealed class Ff7AutoPlayNavigationUnavailableException(int entity, string reason, IReadOnlyList<int>? blockingEntities = null, Vector3? destination = null, int? destinationTriangle = null, Ff7AutoPlayMovementFailure? movementFailure = null) : Exception(reason)
{
    public int Entity { get; } = entity;
    public string Reason { get; } = reason;
    public IReadOnlyList<int> BlockingEntities { get; } = blockingEntities?.Distinct().Order().ToArray() ?? [];
    public Vector3? Destination { get; } = destination;
    public int? DestinationTriangle { get; } = destinationTriangle;
    public Ff7AutoPlayMovementFailure? MovementFailure { get; } = movementFailure;
}
