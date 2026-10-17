using System.Diagnostics.CodeAnalysis;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private sealed class CarPlacementRejectedException(string message) : InvalidOperationException(message)
    {
    }

    internal bool TryPrepareCurrentCarPlacement(int x, int rawZ, [NotNullWhen(true)] out WorldState? candidate)
    {
        try
        {
            candidate = PrepareCurrentCarPlacement(x, rawZ);
            return true;
        }
        catch (CarPlacementRejectedException)
        {
            candidate = null;
            return false;
        }
    }

    /// <summary>Prepares a diagnostic relocation of the current stationary car without running a world callback.</summary>
    internal WorldState PrepareCurrentCarPlacement(int x, int rawZ, int? direction = null, int? expectedNativeHeight = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        GameState departure = session.State;
        if (departure.Scene != SceneKind.WorldMap || departure.World is not { NativeCarRuntime: { EntityType: 33 } car } before || before.NativeFootRuntime is not null || before.NativeGardenRuntime is not null || before.NativeRagnarokRuntime is not null || before.NativeTrainRuntime is not null || before.NativeScriptedRuntime is not null)
        {
            throw new NotSupportedException("Diagnostic car placement requires the current ordinary type33 car owner.");
        }
        _ = new GameSession(departure);
        car.Validate();
        if (car.Speed != 0 || car.MovementMagnitude20409F4 != 0 || car.TerrainTiltBlend != 0)
        {
            throw new NotSupportedException("Diagnostic car placement requires a stationary car with no pending movement or terrain blend.");
        }
        WorldEntryVariables inputs = WorldEntryVariables.Read(departure);
        if (inputs.StoryByte != storyByte || before.ReplacementMask != WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte))
        {
            throw new InvalidOperationException("Diagnostic car placement requires the current scene's terrain selection.");
        }
        x = WrapCoordinate(x % 262144, 262144);
        rawZ = WrapCoordinate(rawZ % 196608, 196608);
        WorldGroundSelection target = assets.LookupNativeGround(x, rawZ, replacements, []) ?? throw new CarPlacementRejectedException("Diagnostic car placement requires an actual target ground polygon.");
        int height = NativeWorldTerrain.ResolveHeight(car.EntityType, -(int)target.Ground.Height, target.Ground.Triangle.GroundType, before.NativeHeight).Height;
        NativeWorldGroundCacheState fresh = NativeWorldGroundCacheState.CreateFresh(target.Identity, car.GroundCache);
        int previousGroundHeight = -unchecked((short)(height - (target.Ground.Triangle.GroundType <= 5 ? 320 : 0)));
        WorldNativeGroundResult accepted = assets.ResolveNativeGround(x, rawZ, 0, 0, previousGroundHeight, null, fresh, replacements, entityType: car.EntityType);
        if (accepted.Blocked || !accepted.GroundCommitted || accepted.X != x || accepted.Z != rawZ || accepted.Center is not { } center)
        {
            throw new CarPlacementRejectedException("Diagnostic car placement requires an accepted stationary native car footprint at the requested coordinates.");
        }
        height = NativeWorldTerrain.ResolveHeight(car.EntityType, -(int)center.Ground.Height, center.Ground.Triangle.GroundType, height).Height;
        if (expectedNativeHeight is int expected && height != expected)
        {
            throw new InvalidOperationException("Diagnostic car placement height differs from its actual native terrain height.");
        }
        NativeCarWorldRuntimeState placedCar = car with { GroundCache = accepted.Cache, GroundYaw = accepted.GroundYaw };
        WorldState candidate = before with
        {
            X = x,
            Z = rawZ,
            NativeHeight = height,
            BodyYaw = direction is int yaw ? WrapYaw(yaw) : before.BodyYaw,
            CameraVelocity = 0,
            CameraTargetX = x,
            CameraTargetZ = rawZ,
            CameraTargetNativeHeight = height - 256,
            GroundType = center.Ground.Triangle.GroundType,
            NativeCarRuntime = placedCar
        };
        (int tilt, int blend) = CarModelTilt(candidate, candidate.BodyYaw, placedCar);
        placedCar = placedCar with { ModelTilt = tilt + blend, TerrainTiltBlend = blend };
        candidate = candidate with { NativeCarRuntime = placedCar };
        assets.ValidateNativeGroundCache(placedCar.GroundCache!, replacements);
        EnsureCarContactCertificate(candidate);
        _ = new GameSession(departure with { World = candidate });
        if (!ReferenceEquals(session.State, departure))
        {
            throw new InvalidOperationException("The current world state changed while preparing diagnostic car placement.");
        }
        return candidate;
    }

    /// <summary>Relocates only the stationary car's spatial publication; ordinary callbacks retain field-entry ownership.</summary>
    internal void DebugPlaceCurrentCar(int x, int rawZ, int? direction = null, int? expectedNativeHeight = null)
    {
        GameState departure = session.State;
        WorldState candidate = PrepareCurrentCarPlacement(x, rawZ, direction, expectedNativeHeight);
        if (!ReferenceEquals(session.State, departure))
        {
            throw new InvalidOperationException("The current world state changed before diagnostic car placement publication.");
        }
        session.SetWorldState(candidate);
    }
}
