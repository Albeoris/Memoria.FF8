namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldMovementState(int WorldId, int WorldMapState, Ff7WorldEntity Player, Ff7WorldInputState Input, Ff7WorldCameraState Camera, int ViewMode, int RadarMode, byte[]? Random, int RandomIndex)
{
    public Ff7WorldSnowState? Snow { get; init; }
    public Ff7WorldEntity[]? SnowPoles { get; init; }
    public Ff7WorldScriptCameraState? ScriptCamera { get; init; }
}
internal sealed record Ff7AutoPlayWorldTerrainBoundary(int ChunkX, int ChunkZ, int Script, bool RestoresPreviousPosition, bool Inert = false);
internal sealed record Ff7AutoPlayWorldModelBoundary(int Model, int Event, bool Inert = false);
internal sealed record Ff7AutoPlayWorldMovementPreview(bool Supported, string? Reason, Ff7AutoPlayWorldMovementState? State = null, bool Moved = false, Ff7AutoPlayWorldTerrainBoundary? TerrainBoundary = null, Ff7AutoPlayWorldModelBoundary[]? ModelBoundaries = null)
{
    public bool RequiresVmPhase => Supported;
}

public sealed partial class Ff7WorldRuntime
{
    private sealed class AutoPlayMovementUnsupportedException(string message) : Exception(message)
    {
    }

    private static void RejectAutoPlayMovement(bool preview, string reason)
    {
        if (preview)
        {
            throw new AutoPlayMovementUnsupportedException(reason);
        }
    }

    internal Ff7AutoPlayWorldMovementState CaptureAutoPlayMovementState()
    {
        EnsureHealthy();
        Ff7WorldEntity player = RequireEntity(controlledModel);
        return new(Resources.World, WorldMapState, player with { TerrainCache = player.TerrainCache?.ToArray() }, input with { TerrainCache = input.TerrainCache?.ToArray() }, camera, viewMode, radarMode, worldRandom?.Capture(), worldRandom?.Index ?? 0)
        {
            Snow = CopySnowState(snow),
            SnowPoles = Resources.World == 3 ? entities.Values.Where(entity => entity.Model is >= 21 and <= 23 && Machine.IsModelAttached(entity.Model)).OrderBy(entity => entity.Model).Select(entity => entity with { TerrainCache = entity.TerrainCache?.ToArray() }).ToArray() : null,
            ScriptCamera = Resources.World == 3 ? scriptCamera : null
        };
    }

    /// <summary>Previews the native input and complete foot-collision phases against current scene facts. The intervening world VM phase remains an explicit execution boundary.</summary>
    internal Ff7AutoPlayWorldMovementPreview PreviewAutoPlayInput(Ff7AutoplayInput action, Ff7AutoPlayWorldMovementState? source = null)
    {
        EnsureHealthy();
        if (!initialized || mainState != 1 || !movementEnabled || controlledModel < 0 || mapTransfer is not null || fieldDeparture is not null || pendingWorldBattle is not null)
        {
            return new(false, "The current world phase cannot admit a physical input preview.");
        }
        source ??= CaptureAutoPlayMovementState();
        if (source.WorldId != Resources.World || source.WorldMapState != WorldMapState || source.Player.Model != controlledModel)
        {
            return new(false, "The projected world allocation differs from the current native allocation.");
        }
        if (Resources.World == 3 && (source.SnowPoles is null || source.SnowPoles.Length > 3 || source.SnowPoles.Any(entity => entity.Model is < 21 or > 23) || !source.SnowPoles.Select(entity => entity.Model).Order().SequenceEqual(Enumerable.Range(21, 3).Where(Machine.IsModelAttached))))
        {
            return new(false, "The projected snow markers differ from the current native presentation allocation.");
        }
        ValidateSnowState(source.Snow, Resources.World);
        ArgumentOutOfRangeException.ThrowIfLessThan(action.Horizontal, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(action.Horizontal, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(action.Vertical, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(action.Vertical, 1);
        Ff7WorldInputState retainedInput = input;
        Ff7WorldCameraState retainedCamera = camera;
        int retainedView = viewMode;
        int retainedRadar = radarMode;
        bool retainedConfirm = HeldConfirm;
        Ff7WorldEncounterRandom? retainedRandom = worldRandom;
        Ff7WorldSnowState? retainedSnow = snow;
        Ff7WorldScriptCameraState? retainedScriptCamera = scriptCamera;
        var retainedEntities = entities.ToArray();
        var retainedMeshes = collisionMeshes.ToArray();
        var retainedDeformations = deformedCollisionMeshes.ToArray();
        try
        {
            input = source.Input with { Held = action.Held, Pressed = (ushort)(action.Held & ~source.Input.Held), Horizontal = action.Horizontal, Vertical = action.Vertical, Run = action.Run, TerrainCache = source.Input.TerrainCache?.ToArray() };
            camera = source.Camera;
            viewMode = source.ViewMode;
            radarMode = source.RadarMode;
            worldRandom = source.Random is null ? null : new(source.Random, source.RandomIndex);
            snow = CopySnowState(source.Snow);
            if (Resources.World == 3)
            {
                scriptCamera = source.ScriptCamera;
                foreach (int model in entities.Keys.Where(model => model is >= 21 and <= 23).ToArray())
                {
                    entities.Remove(model);
                }
                foreach (Ff7WorldEntity pole in source.SnowPoles!)
                {
                    entities[pole.Model] = pole with { TerrainCache = pole.TerrainCache?.ToArray() };
                }
            }
            UpdateEntity(source.Player with { TerrainCache = source.Player.TerrainCache?.ToArray() });
            Ff7AutoPlayWorldTerrainBoundary? boundary = null;
            AdvancePlayerInputCore(autoPlayPreview: true);
            AdvanceWorldCameraHeading();
            if (Resources.World == 3)
            {
                AdvanceScriptCamera();
            }
            AdvanceWorldScriptFacing();
            ResolvePlayerTerrain(autoPlayPreview: true, observeBoundary: value => boundary = value);
            Ff7AutoPlayWorldMovementState result = CaptureAutoPlayMovementState();
            bool moved = result.Player.X != source.Player.X || result.Player.Y != source.Player.Y || result.Player.Z != source.Player.Z;
            Ff7AutoPlayWorldModelBoundary[] modelBoundaries = ReadAutoPlayModelContactSources().Where(entity => result.Player.ContactModel == entity.Model).Select(entity => new Ff7AutoPlayWorldModelBoundary(entity.Model, (input.Held & 32) != 0 ? 4 : 3, IsAutoPlayModelContactInert(entity.Model, (input.Held & 32) != 0 ? 4 : 3))).ToArray();
            return new(true, null, result, moved, boundary, modelBoundaries);
        }
        catch (Exception error) when (error is AutoPlayMovementUnsupportedException or NotSupportedException)
        {
            return new(false, error.Message);
        }
        finally
        {
            input = retainedInput;
            camera = retainedCamera;
            viewMode = retainedView;
            radarMode = retainedRadar;
            HeldConfirm = retainedConfirm;
            worldRandom = retainedRandom;
            snow = retainedSnow;
            scriptCamera = retainedScriptCamera;
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
