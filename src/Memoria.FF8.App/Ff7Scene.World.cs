using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7WorldRuntime? worldPresentationOwner;
    private Ff7WorldPresentation? worldPresentation;
    private readonly CommittedPresentationHistory<(Ff7WorldRuntime Owner, long Tick, long RestoreEpoch, long CameraEpoch), Ff7WorldRenderFrame> worldHistory = new(
        (first, second) => first == second,
        (first, second) => ReferenceEquals(first.Owner, second.Owner) && first.RestoreEpoch == second.RestoreEpoch && first.CameraEpoch == second.CameraEpoch && second.Tick == first.Tick + 1,
        Ff7WorldRenderInterpolation.Continuous);

    private static (Ff7WorldRuntime Owner, long Tick, long RestoreEpoch, long CameraEpoch) WorldHistoryState(Ff7WorldRuntime world)
    {
        return (world, world.PresentationTick, world.PresentationRestoreEpoch, world.PresentationCameraEpoch);
    }

    private void CommitWorldPresentation(Ff7WorldRuntime world)
    {
        worldHistory.Commit(WorldHistoryState(world), () => WorldRenderFrame(world.Process));
    }

    private void ResetWorldPresentation()
    {
        worldPresentationOwner = null;
        worldPresentation = null;
        worldHistory.Reset();
    }

    internal static Ff7WorldRenderFrame WorldRenderFrame(Ff7WorldProcess state)
    {
        Ff7WorldCameraState camera = state.Camera ?? new();
        return new(state.ControlledModel, state.Initialized, state.MainState, state.ViewMode, state.RadarMode, state.WorldMapState, state.PresentationCounter, state.Sky?.CloudPhase ?? 0,
            new(camera.Heading, camera.Pitch, camera.Distance, camera.GroundHeight, camera.Roll, camera.Focal),
            state.Entities.Where(entity => entity.Model != state.Chocobo?.RiderModel).Select(entity => new Ff7WorldVisual(entity.Model, new(entity.X, entity.Y, entity.Z), (entity.Direction + entity.TurnOffset) & 4095, entity.PoseAnimation, entity.PoseFrame, entity.Flags)
            {
                GroundHeight = entity.GroundHeight,
                Terrain = entity.Terrain,
                NativeAnimation = entity.Animation,
                Elevation = entity.Elevation,
                PresentationEpoch = entity.PresentationEpoch
            }).ToArray(),
            state.Lights.Select(light => new Memoria.FF7.Resources.Ff7WorldSkyZone(light.Index, light.X, light.Z, light.Enabled, light.Radius, light.Radius2, new(light.Red, light.Green, light.Blue), new(light.Red2, light.Green2, light.Blue2))).ToArray())
        {
            BridgeDeformation = state.Input?.Bridge?.Deformation,
            HeightSmoothVelocity = state.Input?.HeightSmoothVelocity ?? 0,
            ShakeX = state.ScreenShake?.X ?? 0,
            ShakeY = state.ScreenShake?.Y ?? 0
        };
    }

    private void RenderWorld(Ff7WorldRuntime world, Rectangle bounds)
    {
        if (!ReferenceEquals(worldPresentationOwner, world))
        {
            worldPresentation = new(installation, world.Resources, appearance);
            worldPresentationOwner = world;
        }
        float fraction = PresentationFraction(PresentationPreferences.WorldFps);
        var (previous, current) = worldHistory.Sample(WorldHistoryState(world), fraction, () => WorldRenderFrame(world.Process));
        Ff7WorldRenderFrame frame = previous is null ? current : Ff7WorldRenderInterpolation.Interpolate(previous, current, fraction);
        worldPresentation!.Render(triangles, bounds.Width, bounds.Height, frame);
    }
}

internal static class Ff7WorldRenderInterpolation
{
    public static bool Continuous(Ff7WorldRenderFrame previous, Ff7WorldRenderFrame current)
    {
        return previous.Initialized && current.Initialized && previous.ControlledModel == current.ControlledModel && previous.ViewMode == current.ViewMode && previous.WorldMapState == current.WorldMapState && previous.Models.SingleOrDefault(model => model.Model == previous.ControlledModel) is { } before && current.Models.SingleOrDefault(model => model.Model == current.ControlledModel) is { } after && Compatible(before, after);
    }

    public static Ff7WorldRenderFrame Interpolate(Ff7WorldRenderFrame previous, Ff7WorldRenderFrame current, float fraction)
    {
        Dictionary<int, Ff7WorldVisual> oldModels = previous.Models.ToDictionary(model => model.Model);
        Ff7WorldVisual[] models = current.Models.Select(model => oldModels.TryGetValue(model.Model, out Ff7WorldVisual? old) && Compatible(old, model) ? InterpolateModel(old, model, fraction) : model).ToArray();
        Ff7WorldCameraView first = previous.Camera;
        Ff7WorldCameraView second = current.Camera;
        Ff7WorldCameraView camera = new(
            InterpolateAngle(first.Heading, second.Heading, fraction),
            Lerp(first.Pitch, second.Pitch, fraction),
            Lerp(first.Distance, second.Distance, fraction),
            Lerp(first.GroundHeight, second.GroundHeight, fraction),
            Lerp(first.Roll, second.Roll, fraction),
            Lerp(first.Focal, second.Focal, fraction));
        return current with { Camera = camera, Models = models };
    }

    private static bool Compatible(Ff7WorldVisual previous, Ff7WorldVisual current)
    {
        const int VisibilityFlags = 2 | 8;
        return previous.PresentationEpoch == current.PresentationEpoch && (previous.Flags & VisibilityFlags) == (current.Flags & VisibilityFlags);
    }

    private static Ff7WorldVisual InterpolateModel(Ff7WorldVisual previous, Ff7WorldVisual current, float fraction)
    {
        return current with
        {
            Position = new System.Numerics.Vector3(
                Ff7WorldRuntime.InterpolateWorldCoordinate((int)previous.Position.X, (int)current.Position.X, 0x48000, fraction),
                previous.Position.Y + (current.Position.Y - previous.Position.Y) * fraction,
                Ff7WorldRuntime.InterpolateWorldCoordinate((int)previous.Position.Z, (int)current.Position.Z, 0x38000, fraction)),
            Direction = InterpolateAngle(previous.Direction, current.Direction, fraction),
            Elevation = Lerp(previous.Elevation, current.Elevation, fraction),
            GroundHeight = previous.GroundHeight is int before && current.GroundHeight is int after ? Lerp(before, after, fraction) : current.GroundHeight
        };
    }

    private static int Lerp(int previous, int current, float fraction)
    {
        return (int)MathF.Round(previous + (current - previous) * fraction);
    }

    private static int InterpolateAngle(int previous, int current, float fraction)
    {
        int difference = current - previous;
        if (difference > 2048)
        {
            difference -= 4096;
        }
        else if (difference < -2048)
        {
            difference += 4096;
        }
        return Lerp(previous, previous + difference, fraction) & 4095;
    }
}
