namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldLandingPreview(bool Supported, string? Reason, Ff7WorldEntity? Rider = null, Ff7WorldTerrainReference? Ground = null);

public sealed partial class Ff7WorldRuntime
{
    internal Memoria.FF7.Events.WorldScriptAnalysis ReadAutoPlayMeshEntryForModel(Ff7AutoPlayWorldScriptObservation target, int model)
    {
        Ff7WorldTerrainReference source = target.Faces.First().Source;
        return ReadAutoPlayMeshAnalysis(source.ChunkX, source.ChunkZ, target.Script, 2048, true, model);
    }

    internal Ff7AutoPlayWorldLandingPreview PreviewAutoPlaySurfaceLandingAtFace(Ff7AutoPlayWorldGuideFace face, Ff7AutoPlayWorldGuideFace? predecessor)
    {
        Ff7AutoPlayWorldMovementState source = CaptureAutoPlayMovementState();
        if (predecessor is null && source.Input.GroundTriangle == face.Source)
        {
            return PreviewAutoPlaySurfaceLanding(source);
        }
        System.Numerics.Vector3 center = face.Center;
        int direction = predecessor is null ? source.Player.Direction : inputMath.Direction(WrappedDifference((int)(center.X - predecessor.Center.X), 0x48000), WrappedDifference((int)(center.Z - predecessor.Center.Z), 0x38000));
        Ff7WorldEntity transport = source.Player with { X = Wrap((int)center.X, 0x48000), Y = (int)center.Y + WorldGroundOffset(face.Terrain & 31, source.Player.Model), Z = Wrap((int)center.Z, 0x38000), Terrain = face.Terrain, GroundHeight = (int)center.Y, Direction = direction, TargetDirection = direction, TerrainCache = null };
        return PreviewAutoPlaySurfaceLanding(source with { Player = transport, Input = source.Input with { GroundTriangle = face.Source, TerrainCache = null } });
    }

    /// <summary>Advises the native phase-two landing from a transport pose. Intervening VM work and the actual control transition remain execution boundaries.</summary>
    internal Ff7AutoPlayWorldLandingPreview PreviewAutoPlaySurfaceLanding(Ff7AutoPlayWorldMovementState? source = null)
    {
        EnsureHealthy();
        source ??= CaptureAutoPlayMovementState();
        if (source.WorldId != Resources.World || source.WorldMapState != WorldMapState || source.Player.Model != controlledModel || controlledModel < 0)
        {
            return new(false, "The advised transport pose belongs to another native allocation.");
        }
        if (vehicle is { FlightState: not 0 } || (source.Player.Flags & 128) != 0)
        {
            return new(false, "The current flight phase has no surface-landing advice contract.");
        }
        if (!CanStartSurfaceVehicleDismount(source.Player) && vehicle is not { DismountPhase: 2 })
        {
            return new(true, "The native transport cannot start a surface exit on this terrain.");
        }
        Ff7WorldInputState retainedInput = input;
        Ff7WorldVehicleState? retainedVehicle = vehicle;
        Ff7WorldCameraState retainedCamera = camera;
        bool retainedMovement = movementEnabled;
        int retainedLock = controlLock;
        int retainedView = viewMode;
        int retainedRadar = radarMode;
        bool retainedConfirm = HeldConfirm;
        Ff7WorldEncounterRandom? retainedRandom = worldRandom;
        var retainedEntities = entities.ToArray();
        var retainedMeshes = collisionMeshes.ToArray();
        var retainedDeformations = deformedCollisionMeshes.ToArray();
        try
        {
            input = source.Input with { TerrainCache = source.Input.TerrainCache?.ToArray() };
            camera = source.Camera;
            viewMode = source.ViewMode;
            radarMode = source.RadarMode;
            worldRandom = source.Random is null ? null : new(source.Random, source.RandomIndex);
            UpdateEntity(source.Player with { TerrainCache = source.Player.TerrainCache?.ToArray() });
            vehicle ??= new([]);
            HashSet<(int Phase, int Hold)> protocol = [];
            while (true)
            {
                if (!protocol.Add((vehicle.DismountPhase, vehicle.CancelHeldTicks)))
                {
                    return new(true, "The native exit protocol cannot progress from the advised source.");
                }
                bool landingPhase = vehicle.DismountPhase == 2;
                ushort held = vehicle.DismountPhase == 0 && vehicle.CancelHeldTicks == 0 ? (ushort)0x40 : (ushort)0;
                input = input with { Held = held, Pressed = (ushort)(held & ~input.Held), Horizontal = 0, Vertical = 0 };
                AdvancePlayerInputCore(autoPlayPreview: true);
                AdvanceWorldCameraHeading();
                AdvanceWorldScriptFacing();
                ResolvePlayerTerrain(autoPlayPreview: true, surfaceDismountPreview: true);
                if (landingPhase)
                {
                    Ff7WorldEntity transport = RequireEntity(controlledModel);
                    if (transport.X == input.PreviousX && transport.Z == input.PreviousZ || input.GroundTriangle is null)
                    {
                        return new(true, "The native collision phase does not place a rider away from the transport source.");
                    }
                    int riderModel = ReadSurfaceVehicleRider();
                    Ff7WorldEntity rider = entities.GetValueOrDefault(riderModel) ?? new(riderModel);
                    rider = rider with { X = transport.X, Y = transport.Y, Z = transport.Z, PreviousX = transport.X, PreviousY = transport.Y, PreviousZ = transport.Z, Flags = rider.Flags | 2, Terrain = transport.Terrain, TerrainResolved = transport.TerrainResolved, GroundHeight = transport.GroundHeight };
                    return new(true, null, rider, input.GroundTriangle);
                }
                AdvanceSurfaceVehicleTransport(autoPlayPreview: true);
            }
        }
        catch (AutoPlayMovementUnsupportedException error)
        {
            return new(false, error.Message);
        }
        finally
        {
            input = retainedInput;
            vehicle = retainedVehicle;
            camera = retainedCamera;
            movementEnabled = retainedMovement;
            controlLock = retainedLock;
            viewMode = retainedView;
            radarMode = retainedRadar;
            HeldConfirm = retainedConfirm;
            worldRandom = retainedRandom;
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
