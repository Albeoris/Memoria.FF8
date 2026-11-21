using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayWorldControlObservation(string Id, int Model, int RiderModel, string ConditionFingerprint);

public sealed partial class Ff7WorldRuntime
{
    /// <summary>Observes an intrinsic native control action. Landing and the subsequent ownership change remain native input effects.</summary>
    public Ff7AutoPlayWorldControlObservation[] ReadAutoPlayControlFrontiers()
    {
        if (faulted || !initialized || mainState != 1 || pendingWorldBattle is not null || dialogue is { Completed: false } || fieldDeparture is not null || vehicle is { DismountPhase: > 0 } or { FlightState: not 0 } || !entities.TryGetValue(controlledModel, out Ff7WorldEntity? transport) || !CanStartSurfaceVehicleDismount(transport))
        {
            return [];
        }
        int rider = ReadSurfaceVehicleRider();
        if (rider == controlledModel)
        {
            return [];
        }
        string id = $"control:surface-vehicle:dismount:{controlledModel}:{rider}";
        string fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Context = new
            {
                Resources.World,
                WorldMapState,
                ControlledModel = controlledModel,
                Terrain = transport.Terrain & 31,
                AircraftFlags = transport.Flags & 128,
                input.TerrainScript,
                BridgeDeformation = input.Bridge?.Deformation
            },
            Machine.ProgramIdentity,
            Rider = rider,
            LandingPermitted = true
        })));
        return [new(id, controlledModel, rider, fingerprint)];
    }

    public bool HasAutoPlayControlCompleted(Ff7AutoPlayWorldControlObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return controlledModel == observation.RiderModel && (!observation.Id.StartsWith("control:chocobo:", StringComparison.Ordinal) || movementEnabled && mainState == 1 && dialogue is not { Completed: false } && fieldDeparture is null && mapTransfer is null && pendingWorldBattle is null);
    }

    /// <summary>Returns ordinary controls only; callers must advance the native input loop to perform the action.</summary>
    public Ff7AutoplayInput ChooseAutoPlayControlInput(Ff7AutoPlayWorldControlObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (HasAutoPlayControlCompleted(observation))
        {
            return default;
        }
        if (observation.Id.StartsWith("control:chocobo:", StringComparison.Ordinal))
        {
            return ChooseAutoPlayChocoboControlInput(observation);
        }
        if (controlledModel != observation.Model || ReadSurfaceVehicleRider() != observation.RiderModel || observation.Id != $"control:surface-vehicle:dismount:{controlledModel}:{observation.RiderModel}")
        {
            throw new NotSupportedException("The native surface-vehicle control owner changed before the observed action completed.");
        }
        if (vehicle is { DismountPhase: > 0 })
        {
            return default;
        }
        Ff7AutoPlayWorldControlObservation? current = ReadAutoPlayControlFrontiers().SingleOrDefault(item => item.Id == observation.Id);
        if (current is null || current.ConditionFingerprint != observation.ConditionFingerprint)
        {
            throw new NotSupportedException("The native surface-vehicle dismount preconditions changed before its input was accepted.");
        }
        int heldTicks = vehicle?.CancelHeldTicks ?? 0;
        if (heldTicks >= 15)
        {
            throw new NotSupportedException("The native surface-vehicle Cancel hold exceeded the short-release dismount window.");
        }
        return heldTicks == 0 ? new(Held: 0x40) : default;
    }

    internal int? ReadAutoPlaySurfaceVehicleRider()
    {
        if (Resources.World != 0 || ReadSurfaceVehicleLandingMask(controlledModel) == 0 || vehicle is { FlightState: not 0 } || !entities.TryGetValue(controlledModel, out Ff7WorldEntity? transport))
        {
            return null;
        }
        int rider = ReadSurfaceVehicleRider();
        return rider == controlledModel ? null : rider;
    }

    private int ReadSurfaceVehicleRider()
    {
        return vehicle is { RideStack.Length: > 0 } ? vehicle.RideStack[^1] : 0;
    }
}
