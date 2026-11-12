using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The mounted motor and its shared world object allocation.</summary>
public sealed record NativeChocoboWorldRuntimeState(
    [property: JsonRequired] NativeGardenWorldRuntimeState Objects,
    [property: JsonRequired] NativeChocoboWorldAnimation Animation,
    [property: JsonRequired] int? PreviousHorizontalInput)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFootCameraTransition? CameraTransition { get; init; }

    [JsonIgnore]
    public NativeWorldGroundCacheState GroundCache => Objects.GroundCache;

    [JsonIgnore]
    public byte MapDisplayMode => Objects.MapDisplayMode;

    public void Validate(WorldState world)
    {
        Objects.Validate();
        Animation.Validate();
        CameraTransition?.Validate();
        if (world.ChocoboDismount is null && (Animation.RiderAnimation == 4 || Animation.MountAnimation == 3))
        {
            throw new InvalidDataException("Chocobo dismount animation requires its active transfer.");
        }
        NativeGardenWorldObject rider = Objects.Objects[Objects.Allocation.FootObjectIndex];
        if (unchecked((byte)Objects.Allocation.Packet.Mode) != 49 || Objects.ProjectionCamera is null || Objects.BackgroundRails is null || Objects.PriorTerrainOcclusion is null || world.GroundType is null || PreviousHorizontalInput is < -1 or > 1 || world.AnimationId != Animation.RiderAnimation || world.AnimationCounter != Animation.RiderCounter || rider.Position != new NativeWorldPathPosition(world.X, world.NativeHeight, -world.Z) || rider.RotationY != world.BodyYaw)
        {
            throw new InvalidDataException("Native mounted Chocobo state disagrees with its world packet, ground, body or animation.");
        }
    }
}
