using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldBoardingAdvice(bool Supported, bool Connected, int? Transport, int ContactFaces, int TransportFaces, int RiderFaces, string? Reason);

public sealed partial class Ff7WorldRuntime
{
    /// <summary>Projects an offered native boarding receiver's existing physical profile. No boarding operation or VM instruction is executed.</summary>
    internal Ff7AutoPlayWorldBoardingAdvice ReadAutoPlayBoardingAdvice(Ff7AutoPlayWorldModelScriptObservation contact, Ff7AutoPlayWorldScriptObservation destination)
    {
        int[] receivers = contact.Analysis.Transitions.Where(transition => transition.Operation == WorldOperation.BoardTransport && transition.Model is not null).Select(transition => transition.Model!.Value).Distinct().ToArray();
        Ff7AutoPlayWorldBoardingAdvice Unavailable(string reason) => new(false, false, null, 0, 0, 0, reason);
        if (!contact.Complete || receivers.Length != 1 || !entities.TryGetValue(receivers[0], out Ff7WorldEntity? transport) || transport.Model == controlledModel || !Machine.IsModelScriptEnabled(transport.Model) || ReadSurfaceVehicleLandingMask(transport.Model) == 0 || (transport.Flags & 128) != 0 || IsWorldChocobo(controlledModel) || RequireEntity(controlledModel).AttachedModel is not null || vehicle is { DismountPhase: not 0 } or { FlightState: not 0 })
        {
            return Unavailable("The offered boarding entry has no supported native surface-transport projection.");
        }
        WorldScriptAnalysis riderEntry = ReadAutoPlayMeshEntryForModel(destination, controlledModel);
        if (!riderEntry.Complete || !riderEntry.MayEnterField)
        {
            return Unavailable(riderEntry.Reason ?? "The projected rider has no admitted native field-entry branch at the destination.");
        }
        Ff7AutoPlayWorldGuide contactGuide = ReadAutoPlayModelNavigationGuide(contact);
        if (contactGuide.Faces.Length == 0)
        {
            return new(true, false, transport.Model, 0, 0, 0, contactGuide.Reason);
        }
        int retainedController = controlledModel;
        Ff7WorldInputState retainedInput = input;
        Ff7WorldVehicleState? retainedVehicle = vehicle;
        Ff7WorldCameraState retainedCamera = camera;
        int retainedView = viewMode;
        var retainedEntities = entities.ToArray();
        var retainedMeshes = collisionMeshes.ToArray();
        var retainedDeformations = deformedCollisionMeshes.ToArray();
        try
        {
            controlledModel = transport.Model;
            vehicle = (vehicle ?? new([])) with { RideStack = [.. (vehicle?.RideStack ?? []), retainedController] };
            // Native boarding removes the rider from collision. Retain its allocated
            // descriptor for VM-indexed queries, with collision disabled only in this projection.
            Ff7WorldEntity rider = entities[retainedController];
            entities[retainedController] = rider with { Flags = rider.Flags | 128 };
            input = input with { Held = 0, Pressed = 0, Horizontal = 0, Vertical = 0, GroundTriangle = null, TerrainCache = null, PreviousX = transport.X, PreviousY = transport.Y, PreviousZ = transport.Z, TerrainScript = transport.Terrain >> 5 & 7 };
            Ff7AutoPlayWorldTerrainPoint? ground = ReadAutoPlayTerrain(transport.X, transport.Z);
            if (ground is null)
            {
                return Unavailable("The resident boarding receiver has no admitted native terrain source.");
            }
            input = input with { GroundTriangle = ground.Triangle };
            if (StartsFlightWhenBoarding(transport.Model))
            {
                UpdateEntity(transport with { Terrain = ground.Terrain, GroundHeight = ground.Height, TerrainResolved = true });
                BeginHighwindFlight(1);
                HashSet<(int State, int Progress, int Velocity)> phases = [];
                while (vehicle is { FlightState: not 0 })
                {
                    if (!phases.Add((vehicle.FlightState, vehicle.FlightProgress, vehicle.FlightVelocity)))
                    {
                        return Unavailable("The native boarding takeoff cannot settle from this physical source.");
                    }
                    AdvanceHighwindFlightCamera();
                }
            }
            Ff7AutoPlayWorldModeGuide guide = ReadAutoPlaySurfaceVehicleGuide(destination, (face, previous) => PreviewAutoPlaySurfaceLandingAtFace(face, previous).Ground);
            return new(true, guide.TransportGuide.Faces.Length > 0 && guide.RiderGuide.Faces.Length > 0, transport.Model, contactGuide.Faces.Length, guide.TransportGuide.Faces.Length, guide.RiderGuide.Faces.Length, guide.Reason);
        }
        finally
        {
            controlledModel = retainedController;
            input = retainedInput;
            vehicle = retainedVehicle;
            camera = retainedCamera;
            viewMode = retainedView;
            entities.Clear();
            foreach (var entry in retainedEntities)
            {
                entities.Add(entry.Key, entry.Value);
            }
            collisionMeshes.Clear();
            foreach (var entry in retainedMeshes)
            {
                collisionMeshes.Add(entry.Key, entry.Value);
            }
            deformedCollisionMeshes.Clear();
            foreach (var entry in retainedDeformations)
            {
                deformedCollisionMeshes.Add(entry.Key, entry.Value);
            }
        }
    }
}
