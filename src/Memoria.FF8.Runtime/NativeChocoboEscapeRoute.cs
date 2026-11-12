using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeChocoboEscapeWaypoint([property: JsonRequired] NativeWorldPathPosition Position, [property: JsonRequired] NativeWorldGroundIdentity Ground);

public sealed record NativeChocoboEscapeRoute(
    [property: JsonRequired] NativeWorldPathPosition StartPosition,
    [property: JsonRequired] short StartYaw,
    [property: JsonRequired] short SearchYaw,
    [property: JsonRequired] short SearchPitch,
    [property: JsonRequired] int DirectionAttempt,
    [property: JsonRequired] int Depth,
    [property: JsonRequired] bool RetryingEdge,
    [property: JsonRequired] ImmutableArray<NativeChocoboEscapeWaypoint> SearchPath,
    [property: JsonRequired] ImmutableArray<NativeChocoboEscapeWaypoint> BestPath,
    [property: JsonRequired] bool Finished)
{
    public static NativeChocoboEscapeRoute Start(NativeWorldPathPosition position, short yaw, NativeWorldGroundIdentity ground)
    {
        ground.Validate();
        return new(position, yaw, yaw, 0, 0, 0, false, [new(position, ground)], [], false);
    }

    public void Validate()
    {
        if (DirectionAttempt is < 0 or > 8 || Depth is < 0 or > 63 || SearchPath.IsDefaultOrEmpty || SearchPath.Length > 64 || Depth >= SearchPath.Length || BestPath.IsDefault || BestPath.Length > 63)
        {
            throw new InvalidDataException("Invalid native Chocobo escape search state.");
        }
        foreach (NativeChocoboEscapeWaypoint waypoint in SearchPath)
        {
            waypoint.Ground.Validate();
        }
        foreach (NativeChocoboEscapeWaypoint waypoint in BestPath)
        {
            waypoint.Ground.Validate();
        }
    }
}
