using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeChocoboEscapePhase
{
    Initialize = 1,
    Search,
    Wait,
    Run,
    Finish
}

public sealed record NativeChocoboDismountState(
    [property: JsonRequired] NativeWorldPathPosition Destination,
    [property: JsonRequired] long StartFrame,
    [property: JsonRequired] int Counter,
    [property: JsonRequired] NativeWorldGroundIdentity MountGround)
{
    public const int CallbackCount = 64;

    public NativeChocoboEscapePhase Phase { get; init; } = NativeChocoboEscapePhase.Initialize;
    public NativeChocoboEscapeRoute? Route { get; init; }
    public int Waypoint { get; init; }
    public int? CompanionStopIndex { get; init; }
    public NativeChocoboWorldAnimation? ParkedAnimation { get; init; }

    [JsonIgnore]
    public int AnimationProgress => Counter * 12;

    public void Validate(WorldState world)
    {
        bool mounted = world.NativeChocoboRuntime is not null;
        if (MountGround is null || StartFrame < 24 || Counter is < 1 or > CallbackCount || world.Frame < StartFrame + Counter - 1 ||
            Destination.X is < -131072 or >= 131072 || Destination.Z is < -98303 or > 98304 ||
            Phase is < NativeChocoboEscapePhase.Initialize or > NativeChocoboEscapePhase.Finish || (Phase == NativeChocoboEscapePhase.Initialize) != (Route is null) ||
            Waypoint < 0 || Waypoint > (Route?.BestPath.Length ?? 0) || CompanionStopIndex is < -1 or > 15 ||
            (mounted ? Counter == CallbackCount || ParkedAnimation is not null || world.Frame != StartFrame + Counter - 1 || world.EventUi?.ScriptMode != 7
                : Counter != CallbackCount || ParkedAnimation is null || (world.NativeGardenRuntime ?? world.NativeCarRuntime?.GardenObjects)?.Allocation.ControlledEntityType != 49))
        {
            throw new InvalidDataException("Chocobo dismount lost its native phase, route or controlled body.");
        }
        MountGround.Validate();
        Route?.Validate();
        ParkedAnimation?.Validate();
    }
}
